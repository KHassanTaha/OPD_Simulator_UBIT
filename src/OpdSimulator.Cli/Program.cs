using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Headless M/M/c validation entry point (Milestone 1).
/// </summary>
/// <remarks>
/// <para>
/// Usage: <c>dotnet run --project src/OpdSimulator.Cli -- --lambda &lt;rate&gt; --mu &lt;rate&gt;
/// [--servers &lt;int&gt;] [--horizon &lt;minutes&gt;] [--seed &lt;int&gt;]</c>
/// </para>
/// <para>
/// Exit codes: <c>0</c> run completed and metrics printed; <c>1</c> refused to run
/// (unstable system, ρ ≥ 1 — FR-VAL-1); <c>2</c> invalid command-line usage.
/// </para>
/// <para>
/// Refusals print a single clean line to stderr (D-037); the full exception with
/// stack trace is written only to the file logs, so the terminal never looks like
/// a crash. <see cref="Run"/> is public so the exit code and stderr can be tested
/// in-process by OpdSimulator.Cli.Tests.
/// </para>
/// </remarks>
public static class Program
{
    private const string Usage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- --lambda <rate> --mu <rate> [--servers <int>] [--horizon <minutes>] [--seed <int>]\n" +
        "  --lambda   arrival rate λ (patients per minute, required)\n" +
        "  --mu       service rate per server μ (patients per minute, required)\n" +
        "  --servers  number of parallel servers c (default 1)\n" +
        "  --horizon  arrival-generation window in minutes (default 10000)\n" +
        "  --seed     random seed, default 42 (FR-VAL-3)";

    /// <summary>
    /// Entry point used by <c>dotnet run</c>.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    public static int Main(string[] args)
    {
        Log.Logger = CreateMainLogger();

        // Detail logger: refusal exceptions (with stack trace) go to the file logs
        // only, never to the console (D-037).
        using var fileLogger = CreateFileOnlyLogger();

        try
        {
            return Run(args, Console.Out, Console.Error, fileLogger);
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
    }

    /// <summary>
    /// Runs the CLI logic with injectable output writers and an injectable detail
    /// logger, so tests can assert exit code and console output in-process.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="stdout">Where metrics are written.</param>
    /// <param name="stderr">Where refusal/usage messages are written.</param>
    /// <param name="fileLogger">Logger that receives the full refusal exception.</param>
    /// <returns>Process exit code (0 success, 1 refused, 2 usage error).</returns>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (!TryParseArgs(args, out var config, out string? error))
        {
            stderr.WriteLine(error);
            stderr.WriteLine(Usage);
            return 2;
        }

        Log.Information("CLI run requested: {Args}", string.Join(' ', args));

        try
        {
            var engine = new Engine(config, new SeededRandomSource(), Log.Logger);
            var result = engine.Run();
            PrintMetrics(stdout, config, result);
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

    private static Logger CreateMainLogger()
        => new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Debug,
                shared: true)
            .WriteTo.File("logs/errors-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Warning,
                shared: true)
            .CreateLogger();

    private static Logger CreateFileOnlyLogger()
        => new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Debug,
                shared: true)
            .WriteTo.File("logs/errors-.log", rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7, restrictedToMinimumLevel: LogEventLevel.Warning,
                shared: true)
            .CreateLogger();

    private static void PrintMetrics(TextWriter stdout, EngineConfig config, SimulationResult result)
    {
        stdout.WriteLine();
        stdout.WriteLine($"Stage: {config.StageName}");
        stdout.WriteLine("── Simulation metrics ──────────────────────────────");
        stdout.WriteLine($"  Patients served          : {result.TotalPatientsServed,8}");
        stdout.WriteLine($"  Average wait (min)       : {result.AverageWaitMinutes,8:F3}");
        stdout.WriteLine($"  Average queue length     : {result.AverageQueueLength,8:F3}");
        stdout.WriteLine($"  Average system time (min): {result.AverageSystemTimeMinutes,8:F3}");
        stdout.WriteLine($"  Stage utilisation        : {result.StageUtilisation,8:P1}");
        for (int i = 0; i < result.PerServerUtilisation.Count; i++)
            stdout.WriteLine($"  Server {i} utilisation    : {result.PerServerUtilisation[i],8:P1}");
        stdout.WriteLine($"  Throughput (patients/min): {result.ThroughputPerMinute,8:F3}");
        stdout.WriteLine($"  Operating time (min)     : {result.OperatingTimeMinutes,8:F3}");
        stdout.WriteLine($"  ρ = λ/(c·μ)              : {config.Rho,8:F2}");
        stdout.WriteLine("─────────────────────────────────────────────────────");
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

    private static bool Fail(string message, out EngineConfig config, out string? error)
    {
        config = null!;
        error = message;
        return false;
    }

    private static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
}