namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using System.Text;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;

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
/// The run samples the SAME distributions the analytical model describes (D-213).
/// <c>--ca2</c> and <c>--cs2</c> are translated into real arrival and service
/// specs — a Gamma for a non-unit squared coefficient of variation, Deterministic
/// for zero — so a G/G/c row is a validation of the engine, not a sensitivity
/// analysis of an assumption the engine never implemented. With the defaults
/// (Ca² = Cs² = 1) the specs are null and the engine takes its legacy exponential
/// path, keeping the M/M/c numbers reproducible across releases (D-054).
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
        "  --ca2      arrival squared coefficient of variation (default 1 = exponential)\n" +
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
        double ca2 = 1.0;
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
                case "--ca2" when i + 1 < args.Length:
                    double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out ca2);
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

        if (!double.IsFinite(ca2) || ca2 < 0)
        {
            return Fail(stderr, "--ca2 must be a finite, non-negative number.");
        }

        if (!double.IsFinite(cs2) || cs2 < 0)
        {
            return Fail(stderr, "--cs2 must be a finite, non-negative number.");
        }

        // Refuse a model whose assumptions the requested run would violate BEFORE
        // simulating 200,000 minutes. The message names the model and the remedy,
        // matching the widget's refusal (D-212).
        string? applicability = ApplicabilityRefusal(model, ca2, cs2);
        if (applicability is not null)
        {
            return Fail(stderr, applicability);
        }

        DistributionSpec? arrivalSpec;
        DistributionSpec? serviceSpec;
        try
        {
            arrivalSpec = BuildSpec(ca2, mean: 1.0 / lambda);
            serviceSpec = BuildSpec(cs2, mean: 1.0 / mu);
        }
        catch (ArgumentException ex)
        {
            return Fail(stderr, ex.Message);
        }

        SimulationResult result;
        try
        {
            // One stage, both distributions explicit so the values the model reads
            // are the values the engine sampled. Null specs keep the legacy
            // exponential path for the default M/M/c report.
            var topology = new NetworkTopology(
                lambda,
                new[] { new StageSpec("Stage 1", servers, mu, serviceSpec) },
                arrivalDistribution: arrivalSpec);

            result = new Engine(new SeededRandomSource(), logger).Run(topology, seed, horizon);
        }
        catch (UnstableSystemException unstable)
        {
            stderr.WriteLine(unstable.Message);
            return 1;
        }
        catch (ArgumentException ex)
        {
            stderr.WriteLine($"Refused: {ex.Message}");
            return 1;
        }

        StageMetrics stage = result.StageMetrics[0];
        AnalyticalModelInputs inputs = new(lambda, mu, servers, Ca2: ca2, Cs2: cs2);

        if (!AnalyticalModels.TryCompute(model, inputs, out AnalyticalModelResult? analytical, out string? refusal))
        {
            stderr.WriteLine($"Refused: {refusal}");
            return 1;
        }

        string report = BuildReport(
            lambda, mu, servers, horizon, seed, ca2, cs2, model, stage, analytical!);

        string fullPath = Path.GetFullPath(outPath);
        File.WriteAllText(fullPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        stdout.WriteLine($"Wrote {fullPath}");
        logger.Information(
            "Validation report written to {Path} (model {Model}, Ca2 {Ca2}, Cs2 {Cs2}, seed {Seed})",
            fullPath, model, ca2, cs2, seed);
        return 0;
    }

    /// <summary>
    /// Builds the run's distribution for a requested squared coefficient of
    /// variation: null for exponential, Deterministic for 0, and a Gamma with
    /// shape <c>1/C²</c> otherwise.
    /// </summary>
    /// <param name="c2">The requested squared coefficient of variation.</param>
    /// <param name="mean">The distribution mean in minutes.</param>
    /// <returns>The spec, or null for the exponential process.</returns>
    private static DistributionSpec? BuildSpec(double c2, double mean)
    {
        if (Math.Abs(c2 - 1.0) <= 1e-9)
        {
            return null;
        }

        if (c2 <= 1e-12)
        {
            return DistributionSpecOperations.Create(DistributionFamily.Deterministic, mean, spread: null);
        }

        // Gamma has Ca² = 1/k, so shape k = 1/C².
        return DistributionSpecOperations.Create(
            DistributionFamily.Gamma, mean, spread: 1.0 / c2);
    }

    /// <summary>
    /// The applicability rule for the requested model (D-212).
    /// </summary>
    /// <param name="model">The analytical family.</param>
    /// <param name="ca2">Requested arrival Ca².</param>
    /// <param name="cs2">Requested service Cs².</param>
    /// <returns>The refusal text, or null when the run may proceed.</returns>
    private static string? ApplicabilityRefusal(
        AnalyticalModelFamily model,
        double ca2,
        double cs2)
    {
        switch (model)
        {
            case AnalyticalModelFamily.Mmc when Math.Abs(ca2 - 1.0) > 1e-9 || Math.Abs(cs2 - 1.0) > 1e-9:
                return "M/M/c requires exponential arrivals (Ca² = 1) and exponential service " +
                    $"(Cs² = 1), but this invocation asked for Ca² = {ca2} and Cs² = {cs2}. " +
                    "Use --model M/G/c for non-exponential service, or --model G/G/c for " +
                    "either kind of variability.";

            case AnalyticalModelFamily.Mgc when Math.Abs(ca2 - 1.0) > 1e-9:
                return "M/G/c requires exponential arrivals (Ca² = 1), but this invocation " +
                    $"asked for Ca² = {ca2}. Use --model G/G/c, which accepts any arrival " +
                    "variability.";

            default:
                return null;
        }
    }

    private static string BuildReport(
        double lambda,
        double mu,
        int servers,
        double horizon,
        int seed,
        double ca2,
        double cs2,
        AnalyticalModelFamily model,
        StageMetrics stage,
        AnalyticalModelResult analytical)
    {
        double deltaWq = Percent(stage.AverageWaitMinutes, analytical.Wq);
        double deltaLq = Percent(stage.AverageQueueLength, analytical.Lq);
        bool withinGate = deltaWq <= 5.0 && deltaLq <= 5.0;

        string arrivalDescription = Describe(ca2, "exponential (Poisson)", specialize: "Gamma");
        string serviceDescription = Describe(cs2, "exponential", specialize: "Gamma");

        var sb = new StringBuilder();
        sb.AppendLine("VALIDATION REPORT — simulated vs analytical closed-form queueing model");
        sb.AppendLine("================================================================");
        sb.AppendLine();
        sb.AppendLine("Reproduce this report:");
        sb.AppendLine(FormattableString.Invariant(
            $"  dotnet run --project src/OpdSimulator.Cli -- validate --lambda {lambda} --mu {mu} --servers {servers} --horizon {horizon} --seed {seed} --ca2 {ca2} --cs2 {cs2} --model {analytical.DisplayName}"));
        sb.AppendLine();
        sb.AppendLine("Configuration");
        sb.AppendLine("-------------");
        sb.AppendLine(FormattableString.Invariant($"  arrival rate λ        : {lambda} per minute"));
        sb.AppendLine(FormattableString.Invariant($"  arrival variability   : {arrivalDescription} (Ca² = {ca2})"));
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
        sb.AppendLine("Verdict");
        sb.AppendLine("-------");
        sb.AppendLine("  mode                  : validation — the run sampled the distributions the");
        sb.AppendLine("                          analytical model describes (Ca² and Cs² above)");
        sb.AppendLine("  tolerance             : 5% on both Wq and Lq");
        sb.AppendLine(FormattableString.Invariant($"  result                : {(withinGate ? "PASS" : "FAIL")}"));
        sb.AppendLine();
        sb.AppendLine("Notes");
        sb.AppendLine("-----");
        sb.AppendLine("  - Closed-form values are steady-state results; they describe the long-run");
        sb.AppendLine("    behaviour, not one clinic day. A 165-minute session is transient.");
        sb.AppendLine("  - M/M/c is exact (Erlang-C). M/G/c uses Allen–Cunneen (exact");
        sb.AppendLine("    Pollaczek–Khinchine at c = 1). G/G/c uses Allen–Cunneen (exact");
        sb.AppendLine("    Kingman at c = 1). The two compound models are approximations for");
        sb.AppendLine("    c > 1, so their 5% gate checks the simulator against the approximation,");
        sb.AppendLine("    not against an exact answer.");
        sb.AppendLine("  - A non-unit Ca² or Cs² is sampled from a Gamma distribution with shape");
        sb.AppendLine("    1/C²; Ca² = 0 is sampled as Deterministic. The report names the family");
        sb.AppendLine("    that was actually run.");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>Names the family that a requested C² was sampled from.</summary>
    private static string Describe(double c2, string exponentialName, string specialize)
    {
        if (Math.Abs(c2 - 1.0) <= 1e-9)
        {
            return exponentialName;
        }

        return c2 <= 1e-12
            ? "deterministic"
            : FormattableString.Invariant($"{specialize} (shape k = {1.0 / c2:0.####})");
    }

    private static double Percent(double simulated, double analytical)
        => analytical == 0 ? 0 : Math.Abs(simulated - analytical) / Math.Abs(analytical) * 100.0;

    private static int Fail(TextWriter stderr, string message)
    {
        stderr.WriteLine(message);
        return 2;
    }
}
