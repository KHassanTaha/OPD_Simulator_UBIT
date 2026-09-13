using Serilog;
using Serilog.Core;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Binds the new M2 data subcommands (verify, simulate-data) through the public
/// <see cref="Program.Run"/> surface: exit codes and the shape of stdout/stderr.
/// The deeper loader/validator/fitting logic is unit-tested in OpdSimulator.Data.Tests.
/// </summary>
public class CliDataCommandTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    private static string WriteValidSampleCsv(string dir, string fileName)
    {
        string path = Path.Combine(dir, fileName);
        var lines = new List<string> { "arrival_time,departure_stage,screening_start,screening_end" };
        // Arrival every 5 minutes from 8:15; service a constant 4 minutes.
        for (int i = 0; i < 30; i++)
        {
            int arrival = 495 + i * 5;
            lines.Add($"{ArrivalText(arrival)},Screening,{ArrivalText(arrival + 1)},{ArrivalText(arrival + 5)}");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string ArrivalText(int minutes)
    {
        int h = minutes / 60;
        int m = minutes % 60;
        return $"{h}:{m:D2}";
    }

    [Fact]
    public void Verify_ValidFile_ExitsZero_WithSummary()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteValidSampleCsv(dir, "valid.csv");
            int exitCode = Program.Run(new[] { "verify", "--file", path }, stdout, stderr, NullLogger);

            Assert.Equal(0, exitCode);
            Assert.Contains("File is valid: 30 row(s), 1 service stage pair(s).", stdout.ToString());
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Verify_DirtyFile_ExitsOne_AndListsEveryIssue()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = Path.Combine(dir, "dirty.csv");
            File.WriteAllText(path,
                "arrival_time,departure_stage,screening_start,screening_end\n" +
                "8:15,Screening,8:20,8:30\n" +
                ",Screening,8:20,8:30\n"); // missing arrival

            int exitCode = Program.Run(new[] { "verify", "--file", path }, stdout, stderr, NullLogger);

            Assert.Equal(1, exitCode);
            string output = stdout.ToString();
            Assert.Contains("Row 2, column 'arrival_time'", output); // exact row surfaced
            Assert.Contains("Validation failed: 1 issue(s)", output);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Verify_UnknownCommand_ExitsTwo_WithGlobalUsage()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        int exitCode = Program.Run(new[] { "not-a-command" }, stdout, stderr, NullLogger);

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown command 'not-a-command'.", stderr.ToString());
        Assert.Contains("simulate-params", stderr.ToString());
    }

    [Fact]
    public void SimulateData_FromFile_RunsStableSystem_ExitsZero()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteValidSampleCsv(dir, "data.csv");
            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1,2", "--horizon", "500", "--seed", "42" },
                stdout, stderr, NullLogger);

            Assert.Equal(0, exitCode);
            string output = stdout.ToString();
            Assert.Contains("Fitted from data: λ = 0.2", output);
            Assert.Contains($"ρ = λ/(c·μ)", output);            // metrics block printed twice
            Assert.Equal(2, output.Split("── Simulation metrics").Length - 1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SimulateData_NonExponentialDistribution_IsRefusedWithCleanMessage()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteValidSampleCsv(dir, "data.csv");
            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1", "--distribution", "lognormal" },
                stdout, stderr, NullLogger);

            Assert.Equal(2, exitCode);
            Assert.Contains("M2", stderr.ToString());
            Assert.Equal(string.Empty, stdout.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}