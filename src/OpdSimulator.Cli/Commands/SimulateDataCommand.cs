namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using Serilog;

/// <summary>
/// <c>simulate-data</c>: take a validated file, fit the M1 exponential rates from
/// the observed inter-arrival/service means, and run a single-stage M/M/c
/// simulation for each requested server count. Typical usage validates the model
/// against M/M/c theory for c = 1, 2, 3 in one command.
/// </summary>
/// <remarks>
/// <para>
/// Fitted parameters come from the data (FR-STAT-2): λ = 1/mean(inter-arrival),
/// μ = 1/mean(service time) of the first detected stage (single-stage in M2;
/// multi-stage routing is M3). <c>--servers</c> takes a comma-separated sweep
/// (e.g. <c>1,2,3</c>), one engine run per count.
/// </para>
/// <para>
/// Only the exponential family is supported in M2 — the other fitters exist for
/// the fit report and GUI milestone. Any other family is refused with a clean
/// message. Exit code is 0 when at least one count ran; 1 when every count was
/// refused (unstable ρ ≥ 1, FR-VAL-1).
/// </remarks>
internal static class SimulateDataCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- simulate-data --file <path> --servers 1,2,3 [--horizon <minutes>] [--seed <int>] [--distribution exponential]\n" +
        "  --file          .xlsx or .csv data file (required)\n" +
        "  --servers       comma-separated server counts, e.g. 1,2,3 (required)\n" +
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
            stderr.WriteLine("Missing required argument --servers 1,2,3.");
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

        int seed = OpdSimulator.Core.Distributions.SeededRandomSource.DefaultSeed;
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

        var arrivals = new List<double>();
        foreach (var row in dataSet!.Rows)
            if (row.TryGetValue("arrival_time", out string? t) && TimeParser.TryParse(t ?? string.Empty, out double a))
                arrivals.Add(a);
        if (arrivals.Count < 2)
        {
            stderr.WriteLine("Need at least two arrivals to estimate the arrival rate.");
            return 1;
        }

        double[] interArrival = InterArrivalCalculator.Compute(arrivals);
        double lambda = 1.0 / interArrival.Average();

        var stages = StagePairDetector.Detect(dataSet.Columns);
        if (stages.Pairs.Count == 0)
        {
            stderr.WriteLine("No <stage>_start/<stage>_end pair found; cannot estimate a service rate.");
            return 1;
        }
        StagePair stage = stages.Pairs[0];
        var serviceSets = ServiceTimeCalculator.Compute(dataSet);
        if (!serviceSets.TryGetValue(stage.Stage, out double[]? serviceTimes) || serviceTimes!.Length == 0)
        {
            stderr.WriteLine($"Stage '{stage.Stage}' has no usable service rows.");
            return 1;
        }

        double mu = 1.0 / serviceTimes.Average();
        stdout.WriteLine($"Fitted from data: λ = {lambda:0.###}/min (mean inter-arrival {1 / lambda:0.###} min), "
                         + $"μ = {mu:0.###}/min (mean service {1 / mu:0.###} min, stage '{stage.Stage}').");

        bool anyRan = false;
        foreach (int servers in serverCounts)
        {
            var config = new EngineConfig(lambda, mu, servers, horizon, seed, stageName: stage.Stage);
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