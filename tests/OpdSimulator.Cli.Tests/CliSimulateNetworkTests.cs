using Serilog;
using Serilog.Core;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Binds <c>simulate-network</c> through <see cref="Program.Run"/>: the
/// parameter-driven multi-stage run, the --days clinic-calendar run with
/// same-seed reproducibility, the all-stages unstable refusal, the pre-run ρᵢ
/// print (B3/--verbose), and the usage contract of the run-mode and
/// --p-exit flags.
/// </summary>
public class CliSimulateNetworkTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    [Fact]
    public void ThreeStage_RunsNetwork_ExitsZero()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(
            new[] { "simulate-network", "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--horizon", "500", "--seed", "42" },
            stdout, stderr, NullLogger);

        Assert.Equal(0, exitCode);
        string output = stdout.ToString();
        Assert.Equal(3, output.Split("── Simulation metrics").Length - 1);
        Assert.Contains("── Network totals ─", output);
        Assert.Contains("Stage: Reception", output);
        Assert.Contains("Stage: Screening", output);
        Assert.Contains("Stage: Doctor", output);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void PExit_ThreeStage_RunsWithDoctorReduced_ExitsZero()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(
            new[] { "simulate-network", "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--p-exit", "0.7", "--horizon", "500", "--seed", "42" },
            stdout, stderr, NullLogger);

        Assert.Equal(0, exitCode);
        // Screening serves everyone; the Doctor serves only (1 − p_exit) ≈ 0.3.
        string output = stdout.ToString();
        Assert.Contains("Stage: Screening", output);
        Assert.Contains("Stage: Doctor", output);
        Assert.DoesNotContain("Refusing", output);
    }

    [Fact]
    public void Verbose_PrintsPreRunRho_ThenRuns()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(
            new[] { "simulate-network", "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--p-exit", "0.7", "--horizon", "500", "--verbose" },
            stdout, stderr, NullLogger);

        Assert.Equal(0, exitCode);
        string output = stdout.ToString();
        int rhoBlock = output.IndexOf("── Pre-run stability", StringComparison.Ordinal);
        Assert.True(rhoBlock >= 0, "pre-run stability block expected with --verbose");
        Assert.Contains("Stage 1 'Reception'", output);
        Assert.Contains("Stage 3 'Doctor'", output);
        Assert.Contains("→ ρ = 0.1", output);   // doctor: λ₀·(1−0.7)/(3·0.2)
        Assert.True(output.IndexOf("── Pre-run stability", StringComparison.Ordinal) < output.IndexOf("── Network totals ─", StringComparison.Ordinal));
    }

    [Fact]
    public void DaysTwo_SameSeed_RunsAreIdentical()
    {
        using var firstOut = new StringWriter();
        using var stderr = new StringWriter();
        string[] args = new[]
        {
            "simulate-network", "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2",
            "--p-exit", "0.7", "--days", "5", "--start-day", "Monday", "--cap", "80", "--seed", "42",
        };
        Assert.Equal(0, Program.Run(args, firstOut, stderr, NullLogger));
        string first = firstOut.ToString();
        Assert.Contains("── Clinic day model ──", first);

        using var secondOut = new StringWriter();
        Assert.Equal(0, Program.Run(args, secondOut, stderr, NullLogger));
        Assert.Equal(first, secondOut.ToString());
        Assert.Contains("── Network totals ─", first);
    }

    [Fact]
    public void UnstableStage_RefusedListingAllUnstable()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        // Reception: ρ = 0.2/(1·0.0333) ≈ 6.0; Screening: ρ = 0.2/(2·0.025) = 4.0.
        // Both must appear in the single stderr line.
        int exitCode = Program.Run(
            new[] { "simulate-network", "--lambda", "0.2", "--c", "1,2", "--mu", "0.033,0.025", "--horizon", "500" },
            stdout, stderr, NullLogger);

        Assert.Equal(1, exitCode);
        string err = stderr.ToString();
        Assert.StartsWith("Refusing to run:", err);
        Assert.Contains("Reception", err);
        Assert.Contains("Screening", err);
        Assert.Equal(2, err.Split("is unstable").Length - 1); // every unstable stage appears
        Assert.Contains("ρ = 6.06", err);
        Assert.Contains("ρ = 4.00", err);
        Assert.DoesNotContain("── Network totals ─", stdout.ToString());
    }

    public static IEnumerable<object[]> InvalidArgumentsCases()
    {
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2", "--mu", "0.5,0.25,0.2" } };                 // length mismatch
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2", "--mu", "0.5,0.25", "--p-exit", "0.7" } };   // p-exit needs ≥ 3 stages
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--days", "5", "--horizon", "1000" } }; // conflicting run modes
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--start-day", "Monday" } };           // start-day without --days
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--cap", "5" } };                       // cap without --days
        yield return new object[] { new[] { "--lambda", "0.2", "--c", "1,2,3", "--mu", "0.5,0.25,0.2", "--p-exit", "1.5" } };                  // p-exit out of range
        yield return new object[] { new[] { "--c", "1,2,3", "--mu", "0.5,0.25,0.2" } };                                                          // missing --lambda
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentsCases))]
    public void InvalidArguments_AreUsageErrors(string[] rest)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = Program.Run(new[] { "simulate-network" }.Concat(rest).ToArray(), stdout, stderr, NullLogger);
        Assert.Equal(2, exitCode);
        Assert.NotEqual(string.Empty, stderr.ToString());
    }
}