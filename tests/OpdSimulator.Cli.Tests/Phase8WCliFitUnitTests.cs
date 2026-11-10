namespace OpdSimulator.Cli.Tests;

using System.Globalization;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Core;

/// <summary>
/// Phase 8W: the CLI reports fits in the unit the user asks for. The engine is
/// untouched — <c>fit</c> scales the samples it fits, and <c>simulate-data</c>
/// prints converted λ/μ while still running per minute.
/// </summary>
public class Phase8WCliFitUnitTests
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
    public void Fit_InHours_FitsSamplesSixtyTimesSmaller_AndStatesTheUnit()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var minutesOut = new StringWriter();
        using var hoursOut = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteVaryingCsv(dir, "data.csv");

            int minutesExit = Program.Run(
                new[] { "fit", "--file", path }, minutesOut, stderr, NullLogger);
            int hoursExit = Program.Run(
                new[] { "fit", "--file", path, "--fit-unit", "hours" }, hoursOut, stderr, NullLogger);

            Assert.Equal(0, minutesExit);
            Assert.Equal(0, hoursExit);
            string hours = hoursOut.ToString();

            Assert.Contains("unit = hours", hours);
            Assert.Contains("(n = 29, unit = hours)", hours);
            Assert.Contains("/hour", hours);

            // The samples were divided by 60 before fitting, and the native mean
            // is still printed so the conversion is auditable.
            double meanMinutes = MeanFrom(minutesOut.ToString(), "min");
            double meanHours = MeanFrom(hours, "hours");
            Assert.Equal(meanMinutes / 60.0, meanHours, 3);
            Assert.Contains($"(native: {meanMinutes:0.###} min)", hours);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Reads the "sample mean = X unit" figure from a fit report.</summary>
    private static double MeanFrom(string output, string unit)
    {
        var match = Regex.Match(
            output,
            @"sample mean = ([0-9.]+) " + Regex.Escape(unit),
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"No 'sample mean' line in {unit} found in:{Environment.NewLine}{output}");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void Fit_UnknownUnit_ExitsTwo_AndNamesTheChoices()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteVaryingCsv(dir, "data.csv");

            int exitCode = Program.Run(
                new[] { "fit", "--file", path, "--fit-unit", "fortnights" },
                stdout, stderr, NullLogger);

            Assert.Equal(2, exitCode);
            Assert.Contains("minutes, seconds, hours, days, sessions", stderr.ToString());
            Assert.Equal(string.Empty, stdout.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SimulateData_InHours_PrintsConvertedRates_AndStillRunsPerMinute()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            string path = WriteConstantGapCsv(dir, "data.csv");

            int exitCode = Program.Run(
                new[] { "simulate-data", "--file", path, "--servers", "1,2", "--horizon", "500", "--seed", "42", "--fit-unit", "hours" },
                stdout, stderr, NullLogger);

            Assert.Equal(0, exitCode);
            string output = stdout.ToString();

            // A 5-minute arrival gap is 0.2/min = 12/hour; 4-minute service is
            // 0.25/min = 15/hour. Both figures appear, and the native rate stays
            // visible because that is what the engine consumed.
            Assert.Contains("λ = 12/hour (0.2/min)", output);
            Assert.Contains("μ = 15/hour (0.25/min)", output);
            Assert.Contains("0.083 hours", output); // mean inter-arrival = 5 min in hours
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>30 arrivals whose gaps cycle 2–6 minutes, so the sample varies.</summary>
    private static string WriteVaryingCsv(string dir, string fileName)
    {
        string path = Path.Combine(dir, fileName);
        var lines = new List<string> { "arrival_time,departure_stage,screening_start,screening_end" };
        int arrival = 495;
        for (int i = 0; i < 30; i++)
        {
            if (i > 0)
            {
                arrival += 2 + (i % 5);
            }

            lines.Add($"{Text(arrival)},Screening,{Text(arrival + 1)},{Text(arrival + 5)}");
        }

        File.WriteAllLines(path, lines);
        return path;
    }

    /// <summary>30 arrivals every 5 minutes with a constant 4-minute service.</summary>
    private static string WriteConstantGapCsv(string dir, string fileName)
    {
        string path = Path.Combine(dir, fileName);
        var lines = new List<string> { "arrival_time,departure_stage,screening_start,screening_end" };
        for (int i = 0; i < 30; i++)
        {
            int arrival = 495 + (i * 5);
            lines.Add($"{Text(arrival)},Screening,{Text(arrival + 1)},{Text(arrival + 5)}");
        }

        File.WriteAllLines(path, lines);
        return path;
    }

    private static string Text(int minutesSinceMidnight)
        => $"{minutesSinceMidnight / 60}:{minutesSinceMidnight % 60:00}";
}
