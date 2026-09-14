namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using Serilog;

/// <summary>
/// <c>simulate-data</c>: take a validated file, fit the exponential rates from
/// the observed inter-arrival/service means, and run the simulation from the
/// fitted parameters (FR-STAT-2).
/// </summary>
/// <remarks>
/// <para>
/// Single-stage data (M2 behaviour, numerically identical metrics): λ = 1/mean(inter-arrival),
/// μ = 1/mean(service) of the only service stage, and <c>--servers 1,2,3</c> is
/// a sweep of server counts — one M/M/c run per count, which validates against
/// the M/M/c formulas in one command.
/// </para>
/// <para>
/// Stage-aware data (M3): every detected stage in the clinic flow
/// (Reception → Screening → Doctor, <see cref="ClinicStageOrder"/>) is fitted
/// with its own μᵢ; <c>--servers</c> then takes exactly one count per detected
/// stage in flow order (a single network run, not a sweep). p_exit is estimated
/// from <c>departure_stage</c> (<see cref="PExitCalculator"/>) when Doctor is
/// present, and becomes the exit probability of the Screening stage — the
/// derived per-stage ρ (D-007/D-015) determines stability, so a refusal lists
/// every unstable stage (FR-VAL-1). Rows of a Screening exit may legitimately
/// leave doctor cells blank.
/// </para>
/// <para>
/// Only the exponential family is supported — the other fitters exist for the
/// fit report and GUI milestone. Any other family is refused with a clean
/// message. Exit code is 0 when at least one run happened; 1 when every
/// configuration was refused (unstable ρ ≥ 1).
/// </remarks>
internal static class SimulateDataCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- simulate-data --file <path> --servers 1,2,3 [--horizon <minutes>] [--seed <int>] [--distribution exponential]\n" +
        "  --file          .xlsx or .csv data file (required)\n" +
        "  --servers       single stage: comma-separated counts to sweep (e.g. 1,2,3);\n" +
        "                  stage-aware data: one count per detected stage in flow order (Reception, Screening, Doctor)\n" +
        "  --horizon       arrival-generation window in minutes (default 10000)\n" +
        "  --seed          random seed, default 42 (FR-VAL-3)\n" +
        "  --distribution  only 'exponential' is supported in M2 (default exponential)";

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
        if (!distribution.Equals("exponential", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine($"--distribution must be 'exponential' in M2; '{distribution}' is planned with the GUI. Use e.g. simulate-params or fit instead.");
            return 2;
        }

        string? serversText = GetOption(args, "--servers");
        if (serversText is null)
        {
            stderr.WriteLine("Missing required argument --servers.");
            stderr.WriteLine(Usage);
            return 2;
        }

        int[] serverCounts = ParseServerCounts(serversText);
        if (serverCounts.Length == 0)
        {
            stderr.WriteLine($"Invalid --servers '{serversText}' (use comma-separated positive integers, e.g. 1,2,3).");
            return 2;
        }

        double horizon = 10000;
        string? horizonText = GetOption(args, "--horizon");
        if (horizonText is not null && (!CliShared.TryParseDouble(horizonText, out horizon) || horizon <= 0))
        {
            stderr.WriteLine($"Invalid --horizon value '{horizonText}' (must be positive).");
            return 2;
        }

        int seed = SeededRandomSource.DefaultSeed;
        string? seedText = GetOption(args, "--seed");
        if (seedText is not null && !int.TryParse(seedText, out seed))
        {
            stderr.WriteLine($"Invalid --seed value '{seedText}'.");
            return 2;
        }

        if (!CliShared.TryLoadValidated(file, out var dataSet, out string errorLine))
        {
            stderr.WriteLine(errorLine);
            return 1;
        }

        // External arrival rate λ₀ from the observed inter-arrival times.
        var arrivals = new List<double>();
        foreach (var row in dataSet!.Rows)
            if (row.TryGetValue("arrival_time", out string? t) && TimeParser.TryParse(t ?? string.Empty, out double a))
                arrivals.Add(a);
        if (arrivals.Count < 2)
        {
            stderr.WriteLine("Need at least two arrivals to estimate the arrival rate.");
            return 1;
        }

        double lambda = 1.0 / InterArrivalCalculator.Compute(arrivals).Average();

        // Stage-aware fit: only stages actually present in the file, ordered by
        // the clinic flow (Reception → Screening → Doctor), with per-stage μᵢ.
        var stageSet = StagePairDetector.Detect(dataSet.Columns);
        if (stageSet.Pairs.Count == 0)
        {
            stderr.WriteLine("No <stage>_start/<stage>_end pair found; cannot estimate a service rate.");
            return 1;
        }

        var pairByName = stageSet.Pairs.ToDictionary(p => p.Stage, StringComparer.OrdinalIgnoreCase);
        var orderedNames = ClinicStageOrder.Flow.Where(n => pairByName.ContainsKey(n)).ToList();
        if (orderedNames.Count == 0)
        {
            stderr.WriteLine("No recognised clinic stage (Reception/Screening/Doctor) found in the file.");
            return 1;
        }

        var serviceSets = ServiceTimeCalculator.Compute(dataSet);
        var mu = new double[orderedNames.Count];
        for (int i = 0; i < orderedNames.Count; i++)
        {
            if (!serviceSets.TryGetValue(orderedNames[i], out double[]? times) || times!.Length == 0)
            {
                stderr.WriteLine($"Stage '{orderedNames[i]}' has no usable service rows.");
                return 1;
            }
            mu[i] = 1.0 / times.Average();
        }

        // Probabilistic exit applies only when there is a stage after Screening
        // (Doctor): ρ_doctor = λ₀·(1 − p_exit)/(c·μ) then follows the derived
        // rate (D-007). Otherwise everyone leaves at the last stage.
        int exitStageIndex = -1;
        double exitProbability = 0;
        int screeningIndex = orderedNames.FindIndex(n => n.Equals("Screening", StringComparison.OrdinalIgnoreCase));
        if (screeningIndex >= 0 && screeningIndex < orderedNames.Count - 1)
        {
            var pExit = PExitCalculator.Compute(dataSet);
            exitStageIndex = screeningIndex;
            exitProbability = pExit.ExitProbability;
            stdout.WriteLine($"Fitted from data: λ = {lambda:0.###}/min (mean inter-arrival {1 / lambda:0.###} min); "
                             + $"p_exit = {pExit.ExitProbability:0.###} (Screening {pExit.ScreeningExits} / Doctor {pExit.DoctorExits}; Reception excluded {pExit.ReceptionExcluded})");
            foreach (int i in Enumerable.Range(0, orderedNames.Count))
                stdout.WriteLine($"  Stage '{orderedNames[i]}': μ = {mu[i]:0.###}/min (mean service {1 / mu[i]:0.###} min)");
        }
        else
        {
            stdout.WriteLine($"Fitted from data: λ = {lambda:0.###}/min (mean inter-arrival {1 / lambda:0.###} min), "
                             + $"μ = {mu[0]:0.###}/min (mean service {1 / mu[0]:0.###} min, stage '{orderedNames[0]}').");
        }

        if (orderedNames.Count == 1)
        {
            // M2 metric-identical: single-stage sweep of the requested server counts.
            bool anyRan = false;
            foreach (int servers in serverCounts)
            {
                var config = new EngineConfig(lambda, mu[0], servers, horizon, seed, stageName: orderedNames[0]);
                try
                {
                    var engine = new Engine(config, new SeededRandomSource(), Log.Logger);
                    var result = engine.Run();
                    anyRan = true;
                    Program.PrintMetrics(stdout, config, result);
                }
                catch (UnstableSystemException ex)
                {
                    stderr.WriteLine($"Refusing to run (c = {servers}): {ex.Message}");
                    fileLogger.Error(ex, "Refusing to run an unstable configuration (c = {Servers})", servers);
                }
            }

            return anyRan ? 0 : 1;
        }

        // Stage-aware run: exactly one server count per detected stage.
        if (serverCounts.Length != orderedNames.Count)
        {
            stderr.WriteLine($"Found {orderedNames.Count} stage(s) ({string.Join(", ", orderedNames)}) but {serverCounts.Length} server count(s); "
                             + "pass one count per stage in clinic flow order (Reception, Screening, Doctor).");
            return 2;
        }

        var specs = Enumerable.Range(0, orderedNames.Count)
            .Select(i => new StageSpec(orderedNames[i], serverCounts[i], mu[i]))
            .ToArray();

        try
        {
            var engine = new Engine(new SeededRandomSource(), Log.Logger);
            var result = engine.Run(new NetworkTopology(lambda, specs, exitStageIndex, exitProbability),
                seed, horizon);
            Program.PrintNetworkMetrics(stdout, result);
            return 0;
        }
        catch (UnstableSystemException ex)
        {
            stderr.WriteLine($"Refusing to run: {ex.Message}");
            fileLogger.Error(ex, "Refusing to run an unstable stage-aware configuration");
            return 1;
        }
    }

    private static int[] ParseServerCounts(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : -1)
            .Where(v => v >= 1)
            .ToArray();

    private static string? GetOption(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
            if (args[i] == name && i + 1 < args.Length)
                return args[i + 1];
        return null;
    }
}