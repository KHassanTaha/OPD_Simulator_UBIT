namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using OpdSimulator.Data.Validation;

/// <summary>
/// Turns a data file into the <see cref="DataBindingResult"/> the run needs —
/// the GUI twin of the CLI's <c>simulate-data</c> loading stage (stage
/// detection in clinic order, λ = 1/mean(inter-arrival), μ per stage, p_exit
/// from departure_stage). Load errors and validation issues are reported as
/// clean user-facing reasons, never exceptions (FR-UI-9).
/// </summary>
public static class DataAnalyzer
{
    /// <summary>
    /// Loads, validates and parameter-fits <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Absolute path of an .xlsx or .csv file.</param>
    /// <returns>Binding result: valid when <see cref="DataBindingResult.IsUsable"/> is true.</returns>
    public static DataBindingResult Analyze(string path)
    {
        if (!File.Exists(path))
        {
            return new DataBindingResult(path, null, Array.Empty<ValidationIssue>(),
                "The selected file no longer exists on disk.", null, Array.Empty<string>(),
                Array.Empty<double>(), null, 0, 0, 0, Array.Empty<double>(),
                new Dictionary<string, IReadOnlyList<double>>());
        }

        DataSet? dataSet;
        try
        {
            var loader = new DataLoaderFactory().Create(path);
            dataSet = loader.Load(path);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Data file could not be parsed: {Path}", path);
            return new DataBindingResult(path, null, Array.Empty<ValidationIssue>(),
                $"The file could not be read: {ex.Message}", null, Array.Empty<string>(),
                Array.Empty<double>(), null, 0, 0, 0, Array.Empty<double>(),
                new Dictionary<string, IReadOnlyList<double>>());
        }

        var issues = DataValidator.ValidateReturningIssues(dataSet);

        // Arrivals and their session keys are read in one pass because the two
        // are needed together: the inter-arrival sample has to know which gaps
        // cross a session boundary (D-173).
        var arrivals = new List<double>();
        var sessionOfArrival = new List<string?>();
        var sessionDates = new List<DateOnly>();
        foreach (var row in dataSet.Rows)
        {
            if (row.TryGetValue("arrival_time", out string? t) && TimeParser.TryParse(t ?? string.Empty, out double a))
            {
                arrivals.Add(a);

                // A file with no session_date column yields a null key on every
                // row, so every gap counts as within-session and the pre-8O
                // behaviour is preserved exactly.
                DateOnly? date = null;
                if (row.TryGetValue("session_date", out string? sd)
                    && TimeParser.TryParseDate(sd ?? string.Empty, out DateOnly parsed))
                {
                    date = parsed;
                    sessionDates.Add(parsed);
                }
                sessionOfArrival.Add(date?.ToString("yyyy-MM-dd"));
            }
        }

        // The gaps that ENTER the MLE sample, and the reported inter-arrival
        // sample, are the same list — computed once, so the fitted λ and the
        // histogram drawn from this file can never describe different data
        // (D-173). Reporting all 59 gaps while fitting on 54 would put a
        // negative overnight gap into a histogram whose PDF never has negative
        // support, and the chi-square verdict on the Input tab would be about
        // a sample the engine did not use.
        var withinSessionGaps = ComputeWithinSessionGaps(arrivals, sessionOfArrival);
        double? lambda = withinSessionGaps.Count == 0 ? null : 1.0 / withinSessionGaps.Average();

        // Window λ₂: total arrivals over the operating time the file covers. This
        // is the estimate that matches the window a run will simulate over, which
        // is why it is the one the user is offered as the simulation's own (D-173).
        // The session dates are parsed here rather than handed over by the
        // validator because DataValidator's contract is a list of issues, and
        // widening it to carry data would couple "what is wrong" to "what was
        // read". One extra pass over the rows is not worth that coupling.
        // DISTINCT dates, in first-appearance order. One entry per ROW would
        // report 60 "days" for a six-day file, and SessionDates is published on
        // DataBindingResult and read by the window picker — the shape it
        // promises is the set of days, not the row count. LINQ Distinct yields
        // each value in the order the source first showed it, which is the
        // order the user wants to see in the picker — a HashSet would not
        // promise that.
        var distinctSessionDates = sessionDates
            .Distinct()
            .ToList();

        var observedWindow = ObservationWindowService.FromSessionDates(
            distinctSessionDates.Count == 0 ? null : distinctSessionDates);

        double? windowLambda = observedWindow is { } win && arrivals.Count >= 2
            ? arrivals.Count / win.OperatingMinutes
            : null;

        var stageSet = StagePairDetector.Detect(dataSet.Columns);
        var pairByName = stageSet.Pairs.ToDictionary(p => p.Stage, StringComparer.OrdinalIgnoreCase);
        var orderedNames = ClinicStageOrder.Flow.Where(n => pairByName.ContainsKey(n)).ToArray();

        var serviceSets = ServiceTimeCalculator.Compute(dataSet);
        var fittedRates = new List<double>();
        var serviceMinutes = new Dictionary<string, IReadOnlyList<double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in orderedNames)
        {
            var times = serviceSets.TryGetValue(name, out double[]? found) ? found : Array.Empty<double>();
            fittedRates.Add(times.Length > 0 ? 1.0 / times.Average() : double.NaN);
            serviceMinutes[name] = times;
        }

        double? pExitProbability = null;
        int screeningExits = 0;
        int doctorExits = 0;
        int receptionExcluded = 0;
        try
        {
            var pExit = PExitCalculator.Compute(dataSet);
            pExitProbability = pExit.ExitProbability;
            screeningExits = pExit.ScreeningExits;
            doctorExits = pExit.DoctorExits;
            receptionExcluded = pExit.ReceptionExcluded;
        }
        catch (DataValidationException)
        {
            // No usable departure_stage → p_exit undefined; null lets the
            // coordinator fall back to its default (D-104).
        }

        return new DataBindingResult(path, dataSet, issues, null, lambda,
            orderedNames, fittedRates, pExitProbability, screeningExits,
            doctorExits, receptionExcluded, withinSessionGaps,
            serviceMinutes)
        {
            WindowLambda = windowLambda,
            ObservedWindow = observedWindow,
            SessionDates = distinctSessionDates.Count == 0 ? null : distinctSessionDates,
        };
    }

    /// <summary>
    /// The within-session inter-arrival gaps, in file order (D-173).
    /// </summary>
    /// <param name="arrivals">Arrival times in minutes since midnight, in file order.</param>
    /// <param name="sessionOfArrival">
    /// The session key per arrival (ISO date, or null when the file has no
    /// <c>session_date</c> column). Parallel to <paramref name="arrivals"/>.
    /// </param>
    /// <returns>The gaps, in file order; empty when none qualify.</returns>
    /// <remarks>
    /// <para>
    /// The ESTIMATOR is untouched by D-173 — it is still 1/mean(gap) — but the
    /// SAMPLE changes: a gap whose two endpoints sit in different sessions is not
    /// included. Arrival times are a time of day, so Monday 09:50 followed by
    /// Tuesday 08:15 reads as a gap of about -155 minutes, and letting that into
    /// the mean would drag it towards zero or below, inflating λ or turning it
    /// negative. <b>An inter-arrival distribution is a within-session property;
    /// overnight clinic closures are not inter-arrival times.</b>
    /// </para>
    /// <para>
    /// When the file has no session column every key is null, every gap is kept,
    /// and this is byte-for-byte the pre-8O computation.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<double> ComputeWithinSessionGaps(
        IReadOnlyList<double> arrivals,
        IReadOnlyList<string?> sessionOfArrival)
    {
        var gaps = new List<double>(Math.Max(0, arrivals.Count - 1));
        for (int i = 1; i < arrivals.Count; i++)
        {
            bool sameSession = string.Equals(
                sessionOfArrival[i], sessionOfArrival[i - 1], StringComparison.Ordinal);
            if (sameSession)
                gaps.Add(arrivals[i] - arrivals[i - 1]);
        }

        return gaps;
    }
}