using OpdSimulator.Cli.Commands;
using Serilog;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Headless command-line entry point (Milestone 1 → M2 subcommands).
/// </summary>
/// <remarks>
/// <para>
/// Usage: <c>dotnet run --project src/OpdSimulator.Cli -- &lt;command&gt; [options]</c>
/// where <c>command</c> is one of:
/// <c>simulate-params</c> (M/M/c from rates), <c>verify</c> (data validation),
/// <c>fit</c> (distribution fitting + chi-square, writes JSON for SPSS),
/// <c>simulate-data</c> (simulate from fitted rates), <c>export</c> (clean CSV).
/// </para>
/// <para>
/// Exit codes: <c>0</c> success; <c>1</c> refused (unstable system, ρ ≥ 1, or
/// data validation failed); <c>2</c> invalid command-line usage.
/// Refusals print a single clean line to stderr (D-037); the full exception with
/// stack trace is written only to the file logs, so the terminal never looks like
/// a crash. <see cref="Run"/> is public so exit code and console output are
/// testable in-process by OpdSimulator.Cli.Tests.
/// </para>
/// </remarks>
public static class Program
{
    private const string GlobalUsage =
        "Usage: dotnet run --project src/OpdSimulator.Cli -- <command> [options]\n" +
        "\n" +
        "Commands:\n" +
        "  simulate-params  single-stage M/M/c simulation from rate parameters\n" +
        "  verify           load + validate a data file; prints every issue\n" +
        "  fit              fit a distribution + chi-square; writes logs/fit-*.json\n" +
        "  simulate-data    simulation from fitted rates (single stage: --servers sweep; stage-aware data: per-stage counts)\n" +
        "  export           write a validated file to a clean analysis-ready CSV\n" +
        "\n" +
        "Run 'dotnet run --project src/OpdSimulator.Cli -- <command> --help' for command options.";

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
            Log.Fatal(ex, "Unexpected failure during the CLI run");
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
    /// <param name="stdout">Where command output is written.</param>
    /// <param name="stderr">Where refusal/usage messages are written.</param>
    /// <param name="fileLogger">Logger that receives full refusal exceptions.</param>
    /// <returns>Process exit code (0 success, 1 refused/validation failed, 2 usage error).</returns>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, ILogger fileLogger)
    {
        if (args.Length == 0)
        {
            stderr.WriteLine(GlobalUsage);
            return 2;
        }

        string command = args[0];
        string[] rest = args.Skip(1).ToArray();

        Log.Information("CLI run requested: {Args}", string.Join(' ', args));

        return command switch
        {
            "simulate-params" => SimulateParamsCommand.Run(rest, stdout, stderr, fileLogger),
            "verify" => VerifyCommand.Run(rest, stdout, stderr, fileLogger),
            "fit" => FitCommand.Run(rest, stdout, stderr, fileLogger),
            "simulate-data" => SimulateDataCommand.Run(rest, stdout, stderr, fileLogger),
            "export" => ExportCommand.Run(rest, stdout, stderr, fileLogger),
            _ => UnknownCommand(command, stderr),
        };
    }

    private static int UnknownCommand(string command, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown command '{command}'.");
        stderr.WriteLine(GlobalUsage);
        return 2;
    }

    /// <summary>
    /// Prints the engine results as the aligned metrics block shared by the
    /// simulate-params and simulate-data commands.
    /// </summary>
    internal static void PrintMetrics(TextWriter stdout, OpdSimulator.Core.Engine.EngineConfig config, OpdSimulator.Core.Engine.SimulationResult result)
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

    /// <summary>
    /// Prints a stage-aware simulation run: one metrics block per stage (per-stage
    /// ρᵢ, utilisation, wait, queue, throughput — FR-STAT-6/7) followed by the
    /// whole-network totals. Used by the stage-aware simulate-data path.
    /// </summary>
    internal static void PrintNetworkMetrics(TextWriter stdout, OpdSimulator.Core.Engine.SimulationResult result)
    {
        foreach (var metric in result.StageMetrics)
        {
            stdout.WriteLine();
            stdout.WriteLine($"Stage: {metric.StageName}");
            stdout.WriteLine("── Simulation metrics ──────────────────────────────");
            stdout.WriteLine($"  Patients served          : {metric.PatientsServed,8}");
            stdout.WriteLine($"  Average wait (min)       : {metric.AverageWaitMinutes,8:F3}");
            stdout.WriteLine($"  Average queue length     : {metric.AverageQueueLength,8:F3}");
            stdout.WriteLine($"  Stage utilisation        : {metric.StageUtilisation,8:P1}");
            for (int i = 0; i < metric.PerServerUtilisation.Count; i++)
                stdout.WriteLine($"  Server {i} utilisation    : {metric.PerServerUtilisation[i],8:P1}");
            stdout.WriteLine($"  Throughput (patients/min): {metric.ThroughputPerMinute,8:F3}");
            stdout.WriteLine($"  ρ = λᵢ/(c·μ)             : {metric.Rho,8:F2}");
            stdout.WriteLine("─────────────────────────────────────────────────────");
        }

        stdout.WriteLine();
        stdout.WriteLine("── Network totals ──────────────────────────────────");
        stdout.WriteLine($"  Patients served          : {result.TotalPatientsServed,8}");
        stdout.WriteLine($"  Average wait (min)       : {result.AverageWaitMinutes,8:F3}");
        stdout.WriteLine($"  Average system time (min): {result.AverageSystemTimeMinutes,8:F3}");
        stdout.WriteLine($"  Operating time (min)     : {result.OperatingTimeMinutes,8:F3}");
        stdout.WriteLine($"  Throughput (patients/min): {result.ThroughputPerMinute,8:F3}");
        stdout.WriteLine("─────────────────────────────────────────────────────");
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
}