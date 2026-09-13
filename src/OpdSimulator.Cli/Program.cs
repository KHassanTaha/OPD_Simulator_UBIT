using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Serilog;
using Serilog.Events;

// Headless M/M/c validation entry point (Milestone 1).
//
// Usage: dotnet run --project src/OpdSimulator.Cli -- --lambda <rate> --mu <rate>
//        [--servers <int>] [--horizon <minutes>] [--seed <int>]
//
// Exit codes:
//   0  run completed and metrics printed
//   1  refused to run (unstable system, ρ ≥ 1 — FR-VAL-1)
//   2  invalid command-line usage

const string Usage =
    "Usage: dotnet run --project src/OpdSimulator.Cli -- --lambda <rate> --mu <rate> [--servers <int>] [--horizon <minutes>] [--seed <int>]\n" +
    "  --lambda   arrival rate λ (patients per minute, required)\n" +
    "  --mu       service rate per server μ (patients per minute, required)\n" +
    "  --servers  number of parallel servers c (default 1)\n" +
    "  --horizon  arrival-generation window in minutes (default 10000)\n" +
    "  --seed     random seed, default 42 (FR-VAL-3)";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Debug)
    .WriteTo.File("logs/errors-.log", rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();

try
{
    if (!TryParseArgs(args, out var config, out string? error))
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine(Usage);
        return 2;
    }

    Log.Information("CLI run requested: {Args}", string.Join(' ', args));

    var engine = new Engine(config, new SeededRandomSource(), Log.Logger);
    var result = engine.Run();

    PrintMetrics(config, result);
    return 0;
}
catch (UnstableSystemException ex)
{
    Console.Error.WriteLine(ex.Message);
    Log.Error(ex, "Refusing to run an unstable configuration");
    return 1;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unexpected failure during the simulation run");
    return 3;
}
finally
{
    Log.CloseAndFlush();
}

static void PrintMetrics(EngineConfig config, SimulationResult result)
{
    Console.WriteLine();
    Console.WriteLine($"Stage: {config.StageName}");
    Console.WriteLine("── Simulation metrics ──────────────────────────────");
    Console.WriteLine($"  Patients served          : {result.TotalPatientsServed,8}");
    Console.WriteLine($"  Average wait (min)       : {result.AverageWaitMinutes,8:F3}");
    Console.WriteLine($"  Average queue length     : {result.AverageQueueLength,8:F3}");
    Console.WriteLine($"  Average system time (min): {result.AverageSystemTimeMinutes,8:F3}");
    Console.WriteLine($"  Stage utilisation        : {result.StageUtilisation,8:P1}");
    for (int i = 0; i < result.PerServerUtilisation.Count; i++)
        Console.WriteLine($"  Server {i} utilisation    : {result.PerServerUtilisation[i],8:P1}");
    Console.WriteLine($"  Throughput (patients/min): {result.ThroughputPerMinute,8:F3}");
    Console.WriteLine($"  Operating time (min)     : {result.OperatingTimeMinutes,8:F3}");
    Console.WriteLine($"  ρ = λ/(c·μ)              : {config.Rho,8:F2}");
    Console.WriteLine("─────────────────────────────────────────────────────");
}

static bool TryParseArgs(string[] args, out EngineConfig config, out string? error)
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
                if (!TryParseDouble(args[++i], out lambda))
                    return Fail($"Invalid --lambda value '{args[i]}'.", out config, out error);
                break;
            case "--mu" when i + 1 < args.Length:
                if (!TryParseDouble(args[++i], out mu))
                    return Fail($"Invalid --mu value '{args[i]}'.", out config, out error);
                break;
            case "--servers" when i + 1 < args.Length:
                if (!int.TryParse(args[++i], out servers) || servers < 1)
                    return Fail($"Invalid --servers value '{args[i]}' (must be a positive integer).", out config, out error);
                break;
            case "--horizon" when i + 1 < args.Length:
                if (!TryParseDouble(args[++i], out horizon) || horizon <= 0)
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

static bool Fail(string message, out EngineConfig config, out string? error)
{
    config = null!;
    error = message;
    return false;
}

static bool TryParseDouble(string text, out double value)
    => double.TryParse(text, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out value);