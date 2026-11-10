namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using System.Text.Json;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using Serilog;

/// <summary>
/// <c>fit</c>: fit a chosen distribution family to inter-arrival times and the
/// selected service stage's times, run the chi-square goodness-of-fit test, and
/// write a JSON report to <c>logs/fit-*.json</c> for SPSS cross-checking.
/// </summary>
/// <remarks>
/// Stage selection (<c>--stage all|screening|doctor</c>) is generic N-stage
/// detection (FR-DATA-9); the <c>--mode rate|mean</c> flag only changes how the
/// implied rate/mean is labelled on screen — the JSON always carries the fitted
/// parameters verbatim. p_exit is always part of the report (FR-DATA-6).
/// </remarks>
internal static class FitCommand
{
    private static readonly string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- fit --file <path> [--distribution <family>] [--stage all|screening|doctor] [--alpha 0.05] [--mode rate|mean] [--fit-unit minutes|seconds|hours|days|sessions]\n" +
        "  --file          .xlsx or .csv data file (required, must pass validation)\n" +
        "  --distribution  " + DistributionFitterFactory.SupportedNamesText() + " (default exponential)\n" +
        "  --stage         stage to fit, or 'all' (default screening)\n" +
        "  --alpha         chi-square significance level (default 0.05)\n" +
        "  --mode          display rate or mean on screen (default rate)\n" +
        "  --fit-unit      unit the fit is reported in (default minutes); days = 24 h,\n" +
        "                  sessions = 165 operating minutes. Scaling does not change the verdict\n" +
        "Writes logs/fit-YYYYMMDD-HHMMSS.json with parameters + chi-square for SPSS.";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (args.Contains("--help"))
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        string? file = GetOption(args, "--file");
        if (file is null)
        {
            stderr.WriteLine("Missing required argument --file <path>.");
            stderr.WriteLine(Usage);
            return 2;
        }

        string distribution = GetOption(args, "--distribution") ?? "exponential";
        if (!DistributionFitterFactory.TryCreate(distribution, out var fitter))
        {
            stderr.WriteLine($"Unknown distribution '{distribution}'. Supported: {DistributionFitterFactory.SupportedNamesText()}.");
            return 2;
        }

        string stageFilter = (GetOption(args, "--stage") ?? "screening").ToLowerInvariant();
        if (stageFilter is not ("all" or "screening" or "doctor"))
        {
            stderr.WriteLine($"--stage must be 'all', 'screening' or 'doctor', got '{stageFilter}'.");
            return 2;
        }

        if (!TryParseAlpha(GetOption(args, "--alpha"), out double alpha))
        {
            stderr.WriteLine("--alpha must be a number strictly between 0 and 1 (default 0.05).");
            return 2;
        }

        string mode = (GetOption(args, "--mode") ?? "rate").ToLowerInvariant();
        if (mode is not ("rate" or "mean"))
        {
            stderr.WriteLine("--mode must be 'rate' or 'mean'.");
            return 2;
        }

        string? unitText = GetOption(args, "--fit-unit");
        if (unitText is not null && !FittingTimeUnits.TryParse(unitText, out _))
        {
            stderr.WriteLine($"--fit-unit must be one of {string.Join(", ", FittingTimeUnits.Options)}, got '{unitText}'.");
            return 2;
        }

        FittingTimeUnits.TryParse(unitText, out var fitUnit);

        if (!CliShared.TryLoadValidated(file, out var dataSet, out string errorLine))
        {
            stderr.WriteLine(errorLine);
            return 1;
        }

        try
        {
            var arrivals = ParseArrivals(dataSet!);
            if (arrivals.Count < 2)
                throw new InvalidOperationException(
                    $"Need at least two arrivals to compute inter-arrival times; file has {arrivals.Count}.");

            double[] interArrival = InterArrivalCalculator.Compute(arrivals);
            var iaSamples = FittingTimeUnits.Scale(interArrival, fitUnit);
            FittedDistribution iaFit = fitter!.Fit(iaSamples);
            var iaChi = ChiSquareTest.Run(iaSamples, iaFit, alpha);

            stdout.WriteLine($"Fitting distribution: {fitter.Name}  (α = {alpha.ToString("0.##", CultureInfo.InvariantCulture)}, unit = {FittingTimeUnits.Label(fitUnit)})");
            PrintFit(stdout, "Inter-arrival time", iaSamples, interArrival.Average(), iaFit, iaChi, mode, fitUnit);

            var serviceSets = ServiceTimeCalculator.Compute(dataSet!);
            var serviceJson = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var selected = SelectStages(dataSet!, stageFilter);
            foreach (var pair in selected)
            {
                if (!serviceSets.TryGetValue(pair.Stage, out double[]? samples) || samples!.Length == 0)
                {
                    stdout.WriteLine($"Stage '{pair.Stage}': no usable service rows (skipped).");
                    continue;
                }

                var svcSamples = FittingTimeUnits.Scale(samples, fitUnit);
                FittedDistribution svcFit = fitter.Fit(svcSamples);
                var svcChi = ChiSquareTest.Run(svcSamples, svcFit, alpha);
                PrintFit(stdout, $"Service stage '{pair.Stage}'", svcSamples, samples.Average(), svcFit, svcChi, mode, fitUnit);
                serviceJson[pair.Stage] = ToJson(svcSamples, samples.Average(), svcFit, svcChi, fitUnit);
            }

            var pExit = PExitCalculator.Compute(dataSet!);
            stdout.WriteLine("─────────────────────────────────────────────────────");
            stdout.WriteLine($"p_exit = {pExit.ExitProbability:0.###}  (Screening exits {pExit.ScreeningExits}, screened patients {pExit.ScreenedPatients}; Reception excluded {pExit.ReceptionExited})\n"
                             + $"p_bypass = {PExitCalculator.ComputeBypassProbability(dataSet!, pExit):0.###}  (direct-to-doctor {pExit.BypassExits} of {dataSet!.Rows.Count} arrivals)");

            string jsonPath = WriteJson(file!, iaSamples, interArrival.Average(), iaFit, iaChi, serviceJson, pExit, distribution, alpha, mode, fitUnit);
            stdout.WriteLine($"Wrote fit report to {jsonPath}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            stderr.WriteLine($"Fit failed: {ex.Message}");
            Log.Error(ex, "fit command failed for {Path}", file);
            return 1;
        }
    }

    private static void PrintFit(
        TextWriter stdout,
        string label,
        IReadOnlyList<double> samples,
        double meanMinutes,
        FittedDistribution fit,
        ChiSquareResult chi,
        string mode,
        FittingTimeUnit unit)
    {
        string unitLabel = FittingTimeUnits.Label(unit);
        string nativeMean = unit == FittingTimeUnit.Minutes ? "" : $" (native: {meanMinutes:0.###} min)";
        stdout.WriteLine($"─────────────────────────────────────────────────────");
        stdout.WriteLine($"{label}   (n = {samples.Count}, unit = {unitLabel})");
        stdout.WriteLine($"  fitted: {fit.Name}  {fit.ParametersText()}  [{unitLabel}]");
        stdout.WriteLine($"  sample mean = {samples.Average():0.###} {unitLabel}{nativeMean}"
                         + (mode == "mean"
                             ? $"  (implied mean-mode view)"
                             : $"  → implied rate 1/mean = {1.0 / samples.Average():0.###}/{FittingTimeUnits.RateUnit(unit)}"));
        stdout.WriteLine($"  log-likelihood = {fit.LogLikelihood:0.###}   AIC = {fit.AIC:0.###}");
        stdout.WriteLine($"  χ² = {chi.Statistic:0.###}, df = {chi.DegreesOfFreedom}, p = {chi.PValue:0.####}"
                         + $" → {chi.Decision} at α = {chi.Alpha:0.##}");
    }

    private static Dictionary<string, object?> ToJson(
        IReadOnlyList<double> samples,
        double meanMinutes,
        FittedDistribution fit,
        ChiSquareResult chi,
        FittingTimeUnit unit)
        => new()
        {
            ["sampleSize"] = samples.Count,
            ["sampleMean"] = samples.Average(),
            ["sampleMeanMinutes"] = meanMinutes,
            ["fitUnit"] = FittingTimeUnits.Label(unit),
            ["distribution"] = fit.Name,
            ["parameters"] = fit.Parameters,
            ["logLikelihood"] = fit.LogLikelihood,
            ["aic"] = fit.AIC,
            ["chiSquare"] = new Dictionary<string, object?>
            {
                ["statistic"] = chi.Statistic,
                ["degreesOfFreedom"] = chi.DegreesOfFreedom,
                ["pValue"] = chi.PValue,
                ["decision"] = chi.Decision,
                ["binEdges"] = chi.BinEdges,
                ["observed"] = chi.Observed,
                ["expected"] = chi.Expected,
            },
        };

    private static List<StagePair> SelectStages(DataSet dataSet, string stageFilter)
    {
        var pairs = StagePairDetector.Detect(dataSet.Columns).Pairs;
        if (stageFilter == "all")
            return pairs.ToList();

        foreach (var pair in pairs)
        {
            if (pair.Stage.Equals(stageFilter, StringComparison.OrdinalIgnoreCase))
                return new List<StagePair> { pair };
        }

        throw new InvalidOperationException(
            $"Stage '{stageFilter}' has no <{stageFilter}>_start/<{stageFilter}>_end columns in this file.");
    }

    private static List<double> ParseArrivals(DataSet dataSet)
    {
        var arrivals = new List<double>();
        foreach (var row in dataSet.Rows)
        {
            if (row.TryGetValue("arrival_time", out string? text)
                && !string.IsNullOrWhiteSpace(text)
                && TimeParser.TryParse(text, out double minutes))
                arrivals.Add(minutes);
        }
        return arrivals;
    }

    private static string WriteJson(
        string sourceFile,
        IReadOnlyList<double> interArrival,
        double interArrivalMeanMinutes,
        FittedDistribution iaFit,
        ChiSquareResult iaChi,
        IReadOnlyDictionary<string, object?> stages,
        PExitResult pExit,
        string distribution,
        double alpha,
        string mode,
        FittingTimeUnit unit)
    {
        Directory.CreateDirectory("logs");
        string path = Path.Combine("logs", $"fit-{DateTime.Now:yyyyMMdd-HHmmss}.json");

        var report = new Dictionary<string, object?>
        {
            ["tool"] = "OpdSimulator CLI fit",
            ["timestamp"] = DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
            ["sourceFile"] = sourceFile,
            ["distribution"] = distribution,
            ["alpha"] = alpha,
            ["mode"] = mode,
            ["fitUnit"] = FittingTimeUnits.Label(unit),
            ["interArrival"] = ToJson(interArrival, interArrivalMeanMinutes, iaFit, iaChi, unit),
            ["stages"] = stages,
            ["pExit"] = new Dictionary<string, object?>
            {
                ["probability"] = pExit.ExitProbability,
                ["screenedPatients"] = pExit.ScreenedPatients,
                ["screeningExits"] = pExit.ScreeningExits,
                ["doctorExits"] = pExit.DoctorExits,
                ["receptionExited"] = pExit.ReceptionExited,
                ["bypassExits"] = pExit.BypassExits,
            },
        };

        File.WriteAllText(path, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static bool TryParseAlpha(string? text, out double alpha)
    {
        alpha = 0.05;
        if (text is null)
            return true;
        if (!CliShared.TryParseDouble(text, out alpha))
            return false;
        return alpha > 0 && alpha < 1;
    }

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}