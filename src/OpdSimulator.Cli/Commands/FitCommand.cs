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
        "Usage: dotnet run --project src/OpdSimulator.Cli -- fit --file <path> [--distribution <family>] [--stage all|screening|doctor] [--alpha 0.05] [--mode rate|mean]\n" +
        "  --file          .xlsx or .csv data file (required, must pass validation)\n" +
        "  --distribution  " + DistributionFitterFactory.SupportedNamesText() + " (default exponential)\n" +
        "  --stage         stage to fit, or 'all' (default screening)\n" +
        "  --alpha         chi-square significance level (default 0.05)\n" +
        "  --mode          display rate or mean on screen (default rate)\n" +
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
            FittedDistribution iaFit = fitter!.Fit(interArrival);
            var iaChi = ChiSquareTest.Run(interArrival, iaFit, alpha);

            stdout.WriteLine($"Fitting distribution: {fitter.Name}  (α = {alpha.ToString("0.##", CultureInfo.InvariantCulture)})");
            PrintFit(stdout, "Inter-arrival time", interArrival, iaFit, iaChi, mode);

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

                FittedDistribution svcFit = fitter.Fit(samples);
                var svcChi = ChiSquareTest.Run(samples, svcFit, alpha);
                PrintFit(stdout, $"Service stage '{pair.Stage}'", samples, svcFit, svcChi, mode);
                serviceJson[pair.Stage] = ToJson(samples, svcFit, svcChi);
            }

            var pExit = PExitCalculator.Compute(dataSet!);
            stdout.WriteLine("─────────────────────────────────────────────────────");
            stdout.WriteLine($"p_exit = {pExit.ExitProbability:0.###}  (Screening {pExit.ScreeningExits}, Doctor {pExit.DoctorExits}, Reception excluded {pExit.ReceptionExcluded})");

            string jsonPath = WriteJson(file!, interArrival, iaFit, iaChi, serviceJson, pExit, distribution, alpha, mode);
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

    private static void PrintFit(TextWriter stdout, string label, double[] samples, FittedDistribution fit, ChiSquareResult chi, string mode)
    {
        stdout.WriteLine($"─────────────────────────────────────────────────────");
        stdout.WriteLine($"{label}   (n = {samples.Length})");
        stdout.WriteLine($"  fitted: {fit.Name}  {fit.ParametersText()}");
        stdout.WriteLine($"  sample mean = {samples.Average():0.###} min"
                         + (mode == "mean"
                             ? $"  (implied mean-mode view)"
                             : $"  → implied rate 1/mean = {1.0 / samples.Average():0.###}/min"));
        stdout.WriteLine($"  log-likelihood = {fit.LogLikelihood:0.###}   AIC = {fit.AIC:0.###}");
        stdout.WriteLine($"  χ² = {chi.Statistic:0.###}, df = {chi.DegreesOfFreedom}, p = {chi.PValue:0.####}"
                         + $" → {chi.Decision} at α = {chi.Alpha:0.##}");
    }

    private static Dictionary<string, object?> ToJson(double[] samples, FittedDistribution fit, ChiSquareResult chi)
        => new()
        {
            ["sampleSize"] = samples.Length,
            ["sampleMeanMinutes"] = samples.Average(),
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
        double[] interArrival,
        FittedDistribution iaFit,
        ChiSquareResult iaChi,
        IReadOnlyDictionary<string, object?> stages,
        PExitResult pExit,
        string distribution,
        double alpha,
        string mode)
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
            ["interArrival"] = ToJson(interArrival, iaFit, iaChi),
            ["stages"] = stages,
            ["pExit"] = new Dictionary<string, object?>
            {
                ["probability"] = pExit.ExitProbability,
                ["candidates"] = pExit.TotalCandidates,
                ["screeningExits"] = pExit.ScreeningExits,
                ["doctorExits"] = pExit.DoctorExits,
                ["receptionExcluded"] = pExit.ReceptionExcluded,
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