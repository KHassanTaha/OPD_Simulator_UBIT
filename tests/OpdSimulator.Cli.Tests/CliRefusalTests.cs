using OpdSimulator.Core.Distributions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Tests the CLI's unstable-configuration refusal: it must exit 1, print one
/// clean "Refusing to run" line to stderr with no stack trace, and write the
/// full exception (with stack) only to the file-detail logger (D-037).
/// </summary>
public class CliRefusalTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Fact]
    public void UnstableConfig_ExitsOne_WithCleanStderr_AndFileLogKeepsStackTrace()
    {
        var mainLogSink = new CollectingSink();
        var detailSink = new CollectingSink();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(mainLogSink)
            .CreateLogger();

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var detailLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(detailSink)
            .CreateLogger();

        int exitCode = Program.Run(
            new[] { "simulate-params", "--lambda", "5", "--mu", "4", "--servers", "1", "--horizon", "1000" },
            stdout, stderr, detailLogger);

        Assert.Equal(1, exitCode);

        string errorText = stderr.ToString();
        Assert.StartsWith("Refusing to run:", errorText);

        // The stack trace must never reach the console/stderr.
        Assert.DoesNotContain("at OpdSimulator", errorText);
        Assert.DoesNotContain("UnstableSystemException:", errorText);
        Assert.Single(errorText.Trim().Split('\n'));

        // No metrics are printed for a refused run.
        Assert.Equal(string.Empty, stdout.ToString());

        // The full exception (with stack trace) goes to the file-detail logger only.
        var refusal = Assert.Single(detailSink.Events, e => e.Exception is not null);
        Assert.IsType<OpdSimulator.Core.Engine.UnstableSystemException>(refusal.Exception);
        Assert.Contains("at OpdSimulator", refusal.Exception!.ToString());

        // The main (console) logger received only a normal run line, no exception.
        Assert.DoesNotContain(mainLogSink.Events, e => e.Exception is not null);

        Log.CloseAndFlush();
    }
}