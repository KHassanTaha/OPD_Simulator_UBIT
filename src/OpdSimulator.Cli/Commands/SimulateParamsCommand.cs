namespace OpdSimulator.Cli.Commands;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Serilog;

/// <summary>
/// The renamed Milestone-1 command: simulate a single-stage M/M/c queue from
/// rate parameters (λ, μ). Bare legacy flags (e.g. <c>--lambda 3</c>) now appear
/// after the subcommand, e.g. <c>simulate-params --lambda 3 --mu 4</c>.
/// </summary>
internal static class SimulateParamsCommand
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- simulate-params --lambda <rate> --mu <rate> [--servers <int>] [--horizon <minutes>] [--seed <int>]\n" +
        "  --lambda   arrival rate λ (patients per minute, required)\n" +
        "  --mu       service rate per server μ (patients per minute, required)\n" +
        "  --servers  number of parallel servers c (default 1)\n" +
        "  --horizon  arrival-generation window in minutes (default 10000)\n" +
        "  --seed     random seed, default 42 (FR-VAL-3)";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (args.Contains("--help"))
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        if (!TryParseArgs(args, out var config, out string? error))
        {
            stderr.WriteLine(error);
            stderr.WriteLine(Usage);
            return 2;
        }

        try
        {
            var engine = new Engine(config, new SeededRandomSource(), Log.Logger);
            var result = engine.Run();
            Program.PrintMetrics(stdout, config, result);
            return 0;
        }
        catch (UnstableSystemException ex)
        {
            // One clean, intentional-looking line for the user; the full stack
            // trace stays in the file logs (D-037).
            stderr.WriteLine($"Refusing to run: {ex.Message}");
            fileLogger.Error(ex, "Refusing to run an unstable configuration");
            return 1;
        }
    }

    private static bool TryParseArgs(string[] args, out EngineConfig config, out string? error)
    {
        config = null!;
        error = null;

        double lambda = double.NaN;
        double mu = double.NaN;
        int servers = 1;
        double horizon = 10000;
        int seed = SeededRandomSource.DefaultSeed;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--lambda" when i + 1 < args.Length:
                    if (!CliShared.TryParseDouble(args[++i], out lambda))
                        return Fail($"Invalid --lambda value '{args[i]}'.", out config, out error);
                    break;
                case "--mu" when i + 1 < args.Length:
                    if (!CliShared.TryParseDouble(args[++i], out mu))
                        return Fail($"Invalid --mu value '{args[i]}'.", out config, out error);
                    break;
                case "--servers" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out servers) || servers < 1)
                        return Fail($"Invalid --servers value '{args[i]}' (must be a positive integer).", out config, out error);
                    break;
                case "--horizon" when i + 1 < args.Length:
                    if (!CliShared.TryParseDouble(args[++i], out horizon) || horizon <= 0)
                        return Fail($"Invalid --horizon value '{args[i]}' (must be positive).", out config, out error);
                    break;
                case "--seed" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out seed))
                        return Fail($"Invalid --seed value '{args[i]}'.", out config, out error);
                    break;
                default:
                    return Fail($"Unknown or malformed argument '{args[i]}'.", out config, out error);
            }
        }

        if (double.IsNaN(lambda))
            return Fail("Missing required argument --lambda (<patients per minute>).", out config, out error);
        if (double.IsNaN(mu))
            return Fail("Missing required argument --mu (<patients per minute>).", out config, out error);

        config = new EngineConfig(lambda, mu, servers, horizon, seed);
        return true;
    }

    private static bool Fail(string message, out EngineConfig config, out string? error)
    {
        config = null!;
        error = message;
        return false;
    }
}