using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Locks the Milestone-1 <c>simulate-params</c> output to its known values so
/// the N-stage engine refactor cannot silently change single-stage behaviour.
/// </summary>
public class CliSimulateParamsTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) { }
    }

    [Fact]
    public void SimulateParams_Regression_M1GoldenValues()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(
            new[] { "simulate-params", "--lambda", "3", "--mu", "4", "--servers", "1", "--horizon", "10000", "--seed", "42" },
            stdout, stderr, NullLogger);

        Assert.Equal(0, exitCode);
        string output = stdout.ToString();

        // Kickoff G2: served = 29892, wait = 0.724, ρ = 0.75 — metric values,
        // not string bytes (the output text is not a contract — D-054).
        Assert.Contains("Patients served", output);
        Assert.Contains("29892", output);
        Assert.Contains("0.724", output);
        Assert.Contains("0.75", output);
        Assert.Equal(string.Empty, stderr.ToString());
    }
}