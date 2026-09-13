using Serilog;
using Serilog.Core;

namespace OpdSimulator.Cli.Tests;

/// <summary>
/// Binds the M3 stage-aware <c>simulate-data</c> path through
/// <see cref="Program.Run"/>: per-stage fitting, p_exit from departure_stage,
/// per-stage metrics output, the all-stages unstable refusal, and the
/// server-count contract for stage-aware files.
/// </summary>
public class CliSimulateDataNetworkTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    private static string ArrivalText(int minutes)
    {
        int h = minutes / 60;
        int m = minutes % 60;
        return $"{h}:{m:D2}";
    }

    /// <summary>
    /// 20 patients arriving every 5 min from 08:15. Reception ≈ 2 min,
    /// Screening ≈ 4 min; 14 exit at Screening (blank doctor cells) and 6 see
    /// the doctor for ≈ 5 min ⇒ λ0 = 0.2/min, p_exit = 14/20 = 0.7.
    /// </summary>
    private static string WriteStableThreeStageCsv(string dir)
    {
        string path = Path.Combine(dir, "network.csv");
        var lines = new List<string>
        {
            "arrival_time,departure_stage,reception_start,reception_end,screening_start,screening_end,doctor_start,doctor_end",
        };
        for (int i = 0; i < 20; i++)
        {
            int arrival = 495 + i * 5;
            bool seesDoctor = i < 6; // 6 of 20 → p_exit = 0.7
            string departure = seesDoctor ? "Doctor" : "Screening";
            var cells = new List<string>
            {
                ArrivalText(arrival),
                departure,
                ArrivalText(arrival + 1),
                ArrivalText(arrival + 3),
                ArrivalText(arrival + 3),
                ArrivalText(arrival + 7),
            };
            if (seesDoctor)
                cells.AddRange(new[] { ArrivalText(arrival + 8), ArrivalText(arrival + 13) });
            lines.Add(string.Join(',', cells));
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    /// <summary>
    /// 4 patients every 5 min; Reception ≈ 30 min and Screening ≈ 40 min with
    /// c = 1 and c = 2 respectively ⇒ ρ ≈ 6.0 and 4.0 — both unstable. Two of
    /// the four see the doctor (≈ 5 min) so the Doctor stage exists and is stable.
    /// </summary>
    private static string WriteUnstableThreeStageCsv(string dir)
    {
        string path = Path.Combine(dir, "unstable.csv");
        var lines = new List<string>
        {
            "arrival_time,departure_stage,reception_start,reception_end,screening_start,screening_end,doctor_start,doctor_end",
        };
        for (int i = 0; i < 4; i++)
        {
            int arrival = 495 + i * 5;
            if (i % 2 == 0)
                lines.Add($"{ArrivalText(arrival)},Screening,{ArrivalText(arrival + 1)},{ArrivalText(arrival + 31)},{ArrivalText(arrival + 32)},{ArrivalText(arrival + 72)}");
            else
                lines.Add($"{ArrivalText(arrival)},Doctor,{ArrivalText(arrival + 1)},{ArrivalText(arrival + 31)},{ArrivalText(arrival + 32)},{ArrivalText(arrival + 72)},{ArrivalText(arrival + 73)},{ArrivalText(arrival + 78)}");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void SimulateData_ThreeStageFile_FitsPerStage_RunsNetwork_ExitsZero()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteStableThreeStageCsv(dir);
            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1,2,3", "--horizon", "500", "--seed", "42" },
                stdout, stderr, NullLogger);

            Assert.Equal(0, exitCode);
            string output = stdout.ToString();
            Assert.Contains("Fitted from data: λ = 0.2", output);
            Assert.Contains("p_exit = 0.7", output);
            Assert.Contains("Stage 'Reception'", output);
            Assert.Contains("Stage 'Screening'", output);
            Assert.Contains("Stage 'Doctor'", output);
            Assert.Equal(3, output.Split("── Simulation metrics").Length - 1); // three per-stage blocks
            Assert.Contains("── Network totals ─", output);
            Assert.Equal(string.Empty, stderr.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SimulateData_ThreeStageFile_UnstableStages_RefusedListingAll()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteUnstableThreeStageCsv(dir);
            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1,2,3", "--horizon", "500", "--seed", "42" },
                stdout, stderr, NullLogger);

            Assert.Equal(1, exitCode);
            string err = stderr.ToString();
            Assert.Matches("(?:\\bReception\\b).{0,40}is unstable", err);
            Assert.Matches("(?:\\bScreening\\b).{0,40}is unstable", err);
            Assert.Contains("ρ = 6.00", err);
            Assert.Contains("ρ = 4.00", err);
            Assert.DoesNotContain("── Network totals ─", stdout.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SimulateData_ThreeStageFile_ServerCountMismatch_IsUsageError()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteStableThreeStageCsv(dir);
            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1,2", "--horizon", "500", "--seed", "42" },
                stdout, stderr, NullLogger);

            Assert.Equal(2, exitCode);
            Assert.Contains("pass one count per stage", stderr.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}