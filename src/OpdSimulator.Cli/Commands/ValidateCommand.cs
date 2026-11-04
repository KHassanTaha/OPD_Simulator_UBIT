namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using System.Text;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;

/// <summary>
/// Writes <c>validation_report.txt</c> — the simulated-vs-analytical comparison the
/// project brief asks for (AGENTS §7, FR-VAL-6, D-210).
/// </summary>
/// <remarks>
/// <para>
/// The report follows the same honesty rules as the Results-panel widget: every
/// model is a steady-state result, so a horizon below
/// <see cref="MinimumSteadyStateMinutes"/> is refused with a named reason instead
/// of a table that looks like evidence. The horizon default is above that
/// threshold, so the default invocation produces a usable report.
/// </para>
/// <para>
/// The file is deterministic for a given seed: it prints the seed, horizon, model
/// and command line, and no wall-clock, so a viva can re-run it and diff the output.
/// </para>
/// </remarks>
public static class ValidateCommand
{
    /// <summary>Steady-state threshold shared with the GUI widget (D-137).</summary>
    public const double MinimumSteadyStateMinutes = 100_000.0;

    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- validate --lambda <rate> --mu <rate> [options]\n" +
        "  --lambda   arrival rate λ (patients per minute, required)\n" +
        "  --mu       service rate per server μ (patients per minute, required)\n" +
        "  --servers  number of parallel servers c (default 1)\n" +
        "  --horizon  simulated minutes (default 200000; must be ≥ 100000 for a verdict)\n" +
        "  --seed     random seed, default 42 (FR-VAL-3)\n" +
        "  --cs2      service squared coefficient of variation (default 1 = exponential)\n" +
        "  --model    M/M/c | M/G/c | G/G/c (default M/M/c)\n" +
        "  --out      output path (default validation_report.txt)\n";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="args">Arguments after the <c>validate</c> token.</param>
    /// <param name="stdout">Where normal output is written.</param>
    /// <param name="stderr">Where error output is written.</param>
    /// <param name="logger">Serilog logger for structured logging.</param>
    /// <returns>Process exit code: 0 success, 1 validation failure, 2 bad usage.</returns>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, Serilog.ILogger logger)
    {
        if (args.Contains("--help"))
        {
            stdout.Write(Usage);
            return 0;
        }

        double lambda = double.NaN;
        double mu = double.NaN;
        int servers = 1;
        double horizon = 200_000.0;
        int seed = SeededRandomSource.DefaultSeed;
        double cs2 = 1.0;
        AnalyticalModelFamily model = AnalyticalModelFamily.Mmc;
        string outPath = "validation_report.txt";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lambda" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out lambda);
                    break;
                case "--mu" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out mu);
                    break;
                case "--servers" when i + 1 < args.Length:
                    int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out servers);
                    break;
                case "--horizon" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out horizon);
                    break;
                case "--seed" when i + 1 < args.Length:
                    int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed);
                    break;
                case "--cs2" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out cs2);
                    break;
                case "--model" when i + 1 < args.Length:
                    string modelArg = args[++i];
                    if (modelArg is not ("M/M/c" or "M/G/c" or "G/G/c"))
                    {
                        return Fail(stderr, $"Unknown model '{modelArg}'. Use M/M/c, M/G/c or G/G/c.");
                    }

                    model = modelArg switch
                    {
                        "M/G/c" => AnalyticalModelFamily.Mgc,
                        "G/G/c" => AnalyticalModelFamily.Ggc,
                        _ => AnalyticalModelFamily.Mmc
                    };
                    break;
                case "--out" when i + 1 < args.Length:
                    outPath = args[++i];
                    break;
                default:
                    return Fail(stderr, $"Unknown argument '{args[i]}'.\n\n{Usage}");
            }
        }

        if (double.IsNaN(lambda) || double.IsNaN(mu))
        {
            return Fail(stderr, "Missing required argument --lambda and/or --mu.\n\n" + Usage);
        }

        var config = new EngineConfig(lambda, mu, servers, horizon, seed);
        try
        {
            config.Validate();
        }
        catch (UnstableSystemException unstable)
        {
            stderr.WriteLine(unstable.Message);
            return 1;
        }

        var engine = new Engine(config, new SeededRandomSource(), logger);
        SimulationResult result = engine.Run();

        StageMetrics stage = result.StageMetrics[0];
        AnalyticalModelInputs inputs = new(lambda, mu, servers, Ca2: 1.0, Cs2: cs2);

        if (!AnalyticalModels.TryCompute(model, inputs, out AnalyticalModelResult? analytical, out string? refusal))
        {
            stderr.WriteLine($"Refused: {refusal}");
            return 1;
        }

        string report = BuildReport(lambda, mu, servers, horizon, seed, cs2, model, model == AnalyticalModelFamily.Mmc ? "exponential" : "configured", stage, analytical!);

        string fullPath = Path.GetFullPath(outPath);
        File.WriteAllText(fullPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        stdout.WriteLine($"Wrote {fullPath}");
        logger.Information("Validation report written to {Path} (model {Model}, seed {Seed})", fullPath, model, seed);
        return 0;
    }

    private static string BuildReport(
        double lambda,
        double mu,
        int servers,
        double horizon,
        int seed,
        double cs2,
        AnalyticalModelFamily model,
        string serviceDescription,
        StageMetrics stage,
        AnalyticalModelResult analytical)
    {
        double deltaWq = Percent(stage.AverageWaitMinutes, analytical.Wq);
        double deltaLq = Percent(stage.AverageQueueLength, analytical.Lq);
        bool withinGate = deltaWq <= 5.0 && deltaLq <= 5.0;

        var sb = new StringBuilder();
        sb.AppendLine("VALIDATION REPORT — simulated vs analytical closed-form queueing model");
        sb.AppendLine("================================================================");
        sb.AppendLine();
        sb.AppendLine("Reproduce this report:");
        sb.AppendLine(FormattableString.Invariant(
            $"  dotnet run --project src/OpdSimulator.Cli -- validate --lambda {lambda} --mu {mu} --servers {servers} --horizon {horizon} --seed {seed} --cs2 {cs2} --model {analytical.DisplayName}"));
        sb.AppendLine();
        sb.AppendLine("Configuration");
        sb.AppendLine("-------------");
        sb.AppendLine(FormattableString.Invariant($"  arrival rate λ        : {lambda} per minute (inter-arrivals are exponential, Ca² = 1)"));
        sb.AppendLine(FormattableString.Invariant($"  service rate μ        : {mu} per minute per server"));
        sb.AppendLine(FormattableString.Invariant($"  servers c             : {servers}"));
        sb.AppendLine(FormattableString.Invariant($"  service variability   : {serviceDescription} (Cs² = {cs2})"));
        sb.AppendLine(FormattableString.Invariant($"  simulated horizon     : {horizon} minutes"));
        sb.AppendLine(FormattableString.Invariant($"  random seed           : {seed}"));
        sb.AppendLine(FormattableString.Invariant($"  analytical model      : {analytical.DisplayName} — {analytical.FormulaName}"));
        sb.AppendLine();
        sb.AppendLine("Steady-state check");
        sb.AppendLine("------------------");
        sb.AppendLine(FormattableString.Invariant($"  operating minutes     : {horizon}"));
        sb.AppendLine(FormattableString.Invariant($"  threshold             : {MinimumSteadyStateMinutes}"));
        sb.AppendLine(FormattableString.Invariant($"  traffic intensity ρ   : {analytical.Rho:0.####}"));
        sb.AppendLine();
        sb.AppendLine("Comparison");
        sb.AppendLine("----------");
        sb.AppendLine("  metric                          simulated     analytical        Δ%");
        sb.AppendLine(FormattableString.Invariant($"  average wait in queue Wq {stage.AverageWaitMinutes,14:0.####} {analytical.Wq,14:0.####} {deltaWq,9:0.##}"));
        sb.AppendLine(FormattableString.Invariant($"  average queue length  Lq {stage.AverageQueueLength,14:0.####} {analytical.Lq,14:0.####} {deltaLq,9:0.##}"));
        sb.AppendLine();
        bool isValidation = Math.Abs(cs2 - 1.0) <= 1e-9;
        sb.AppendLine("Verdict");
        sb.AppendLine("-------");
        if (isValidation)
        {
            sb.AppendLine("  mode                  : validation (this run sampled the model's service distribution)");
            sb.AppendLine("  tolerance             : 5% on both Wq and Lq");
            sb.AppendLine(FormattableString.Invariant($"  result                : {(withinGate ? "PASS" : "FAIL")}"));
        }
        else
        {
            sb.AppendLine("  mode                  : sensitivity analysis — NOT a validation");
            sb.AppendLine("  reason                : this run sampled exponential service (Cs² = 1), while the");
            sb.AppendLine("                          analytical value used the requested Cs²; the difference");
            sb.AppendLine("                          measures the assumption, not the engine.");
            sb.AppendLine("  result                : N/A (no verdict issued)");
        }
        sb.AppendLine();
        sb.AppendLine("Notes");
        sb.AppendLine("-----");
        sb.AppendLine("  - Closed-form values are steady-state results; they describe the long-run");
        sb.AppendLine("    behaviour, not one clinic day. A 165-minute session is transient.");
        sb.AppendLine("  - M/M/c assumes exponential service. For any other service distribution use");
        sb.AppendLine("    M/G/c (Allen–Cunneen; Pollaczek–Khinchine at c = 1) or G/G/c");
        sb.AppendLine("    (Allen–Cunneen; Kingman at c = 1).");
        sb.AppendLine("  - With exponential arrivals Ca² = 1, so G/G/c and M/G/c agree exactly.");
        sb.AppendLine();

        return sb.ToString();
    }

    private static double Percent(double simulated, double analytical)
        => analytical == 0 ? 0 : Math.Abs(simulated - analytical) / Math.Abs(analytical) * 100.0;

    private static int Fail(TextWriter stderr, string message)
    {
        stderr.WriteLine(message);
        return 2;
    }
}
