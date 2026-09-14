namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Data;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using OpdSimulator.Data.Validation;

/// <summary>
/// Turns a data file into the <see cref="DataBindingResult"/> the config view
/// model displays and the coordinator runs with — the GUI twin of the CLI's
/// <c>simulate-data</c> loading stage (stage detection in clinic order,
/// λ = 1/mean(inter-arrival), μ per stage, p_exit from departure_stage).
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
            // A clean, user-facing reason instead of a stack trace (FR-UI-9).
            Serilog.Log.Warning(ex, "Data file could not be parsed: {Path}", path);
            return new DataBindingResult(path, null, Array.Empty<ValidationIssue>(),
                $"The file could not be read: {ex.Message}", null, Array.Empty<string>(),
                Array.Empty<double>(), null, 0, 0, 0, Array.Empty<double>(),
                new Dictionary<string, IReadOnlyList<double>>());
        }

        var issues = DataValidator.ValidateReturningIssues(dataSet);

        // Inter-arrival gaps from arrival_time; at least two are needed for a rate.
        var arrivals = new List<double>();
        foreach (var row in dataSet.Rows)
        {
            if (row.TryGetValue("arrival_time", out string? t) && TimeParser.TryParse(t ?? string.Empty, out double a))
            {
                arrivals.Add(a);
            }
        }

        double? lambda = arrivals.Count >= 2 ? 1.0 / InterArrivalCalculator.Compute(arrivals).Average() : null;

        // Detected clinic stages in flow order, with one fitted μ each.
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

        // p_exit only means something when there is a stage after Screening (Doctor).
        // A file with no usable departure_stage leaves it undefined (null).
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
        }

        return new DataBindingResult(path, dataSet, issues, null, lambda,
            orderedNames, fittedRates, pExitProbability, screeningExits,
            doctorExits, receptionExcluded, InterArrivalCalculator.Compute(arrivals),
            serviceMinutes);
    }
}