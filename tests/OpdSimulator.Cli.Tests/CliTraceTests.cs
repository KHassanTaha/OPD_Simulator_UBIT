using Serilog;
using Serilog.Core;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Binds <c>trace</c> through <see cref="Program.Run"/>: the golden end-to-end
/// lock (CLI stdout must byte-match the hand-verified 5-patient fixture),
/// level variants, the unstable refusal (exit 1), the <c>--output</c> file mode,
/// and the usage/validation contract (exit 2).
/// </summary>
public class CliTraceTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    private static string FixturePath
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "trace-5-patients.txt");

    /// <summary>
    /// Splits a writer dump into lines, tolerating platform newlines.
    /// </summary>
    private static string[] LinesOf(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static string[] RunTrace(string level = "detailed")
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(
            new[] { "trace", "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "5", "--seed", "42", "--level", level },
            stdout, stderr, NullLogger);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        return LinesOf(stdout.ToString());
    }

    /// <summary>
    /// The CLI end-to-end golden lock: rendering through <c>Program.Run</c> must
    /// produce exactly the frozen, hand-verified 5-patient story.
    /// </summary>
    [Fact]
    public void Trace_DetailedLevel_MatchesHandVerifiedFixture()
    {
        string[] expected = LinesOf(File.ReadAllText(FixturePath));
        Assert.Equal(expected, RunTrace("detailed"));
    }

    [Fact]
    public void Trace_DebugLevel_ListsDrawsAgainstTheReferenceSequence()
    {
        string[] lines = RunTrace("debug");
        Assert.Contains(lines, l => l.Contains("seed=42", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("draw#1 U=0.6681", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("draw#2 U=0.1409", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("draw#6 U=0.2626", StringComparison.Ordinal));
    }

    [Fact]
    public void Trace_StandardLevel_DropsStateColumnsAndRngRows()
    {
        string[] lines = RunTrace("standard");
        Assert.All(lines, l => Assert.DoesNotContain("s0", l));
        Assert.All(lines, l => Assert.DoesNotContain("→", l));
        Assert.All(lines, l => Assert.DoesNotContain("RNG", l));
        Assert.Contains("q=1", string.Join("\n", lines)); // queue values still present at standard level
    }

    [Fact]
    public void Trace_Unstable_RefusesWithExitOne()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(
            new[] { "trace", "--lambda", "5", "--mu", "1", "--servers", "1", "--patients", "5" },
            stdout, stderr, NullLogger);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Refusing to run:", stderr.ToString());
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public void Trace_OutputFile_WritesTraceAndConfirmsOnStdout()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"opd-trace-test-{Guid.NewGuid():N}.txt");
        try
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            int exitCode = Program.Run(
                new[] { "trace", "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "5", "--seed", "42", "--output", tempFile },
                stdout, stderr, NullLogger);

            Assert.Equal(0, exitCode);
            Assert.Equal(string.Empty, stderr.ToString());
            string confirmation = stdout.ToString();
            Assert.Contains("Trace written to", confirmation);
            Assert.Contains("5 patient exit(s)", confirmation);
            Assert.Equal(LinesOf(File.ReadAllText(FixturePath)), LinesOf(File.ReadAllText(tempFile)));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Trace_Help_PrintsUsageAndExitsZero()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Assert.Equal(0, Program.Run(new[] { "trace", "--help" }, stdout, stderr, NullLogger));
        Assert.Contains("Usage: dotnet run --project src/OpdSimulator.Cli -- trace", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    public static IEnumerable<object[]> InvalidArgumentsCases()
    {
        yield return new object[] { new[] { "--mu", "4", "--servers", "1", "--patients", "5" } };                       // missing --lambda
        yield return new object[] { new[] { "--lambda", "3", "--servers", "1", "--patients", "5" } };                    // missing --mu
        yield return new object[] { new[] { "--lambda", "3", "--mu", "4,2", "--servers", "1", "--patients", "5" } };     // server/mu length mismatch
        yield return new object[] { new[] { "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "5", "--p-exit", "0.3" } }; // p-exit with < 3 stages
        yield return new object[] { new[] { "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "0" } };       // non-positive patients
        yield return new object[] { new[] { "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "5", "--level", "verbose" } }; // bad level
        yield return new object[] { new[] { "--lambda", "3", "--mu", "4", "--servers", "1", "--patients", "5", "--real-start", "25:00" } }; // bad clock
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentsCases))]
    public void Trace_InvalidArguments_AreUsageErrors(string[] rest)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(new[] { "trace" }.Concat(rest).ToArray(), stdout, stderr, NullLogger);
        Assert.Equal(2, exitCode);
        Assert.NotEqual(string.Empty, stderr.ToString());
    }
}