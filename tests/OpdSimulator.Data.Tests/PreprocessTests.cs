namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Export;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;
using OpdSimulator.Data.Validation;
using Xunit;

public class PreprocessTests
{
    private static DataSet TwoStage() => new(
        "mem://two-stage",
        DateTime.UtcNow,
        new[] { "arrival_time", "departure_stage", "screening_start", "screening_end", "doctor_start", "doctor_end" },
        new IReadOnlyDictionary<string, string>[]
        {
            new Dictionary<string, string> { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening", ["screening_start"] = "8:20", ["screening_end"] = "8:30", ["doctor_start"] = "", ["doctor_end"] = "" },
            new Dictionary<string, string> { ["arrival_time"] = "8:40", ["departure_stage"] = "Doctor", ["screening_start"] = "8:42", ["screening_end"] = "8:55", ["doctor_start"] = "8:56", ["doctor_end"] = "9:10" },
        });

    [Fact]
    public void InterArrival_IsGapBetweenConsecutiveArrivals()
    {
        double[] gaps = InterArrivalCalculator.Compute(new[] { 10.0, 15.0, 21.0 });

        Assert.Equal(new[] { 5.0, 6.0 }, gaps);
    }

    [Fact]
    public void InterArrival_SingleRow_YieldsEmpty()
    {
        Assert.Empty(InterArrivalCalculator.Compute(new[] { 10.0 }));
    }

    [Fact]
    public void ServiceTime_DetectsStagesByColumnPairs()
    {
        var times = ServiceTimeCalculator.Compute(TwoStage());

        Assert.Equal(2, times.Count);
        Assert.Contains(times, kvp => kvp.Key.Equals("Screening", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(times, kvp => kvp.Key.Equals("Doctor", StringComparison.OrdinalIgnoreCase));

        // Screening: two rows (10, 13 min). Doctor: one row (14 min).
        Assert.Equal(new[] { 10.0, 13.0 }, times.First(t => t.Key.Equals("Screening", StringComparison.OrdinalIgnoreCase)).Value);
        Assert.Equal(new[] { 14.0 }, times.First(t => t.Key.Equals("Doctor", StringComparison.OrdinalIgnoreCase)).Value);
    }

    [Fact]
    public void PExit_CountsScreeningDoctorAndExcludesReception()
    {
        var data = new DataSet(
            "mem://p-exit",
            DateTime.UtcNow,
            new[] { "departure_stage" },
            new IReadOnlyDictionary<string, string>[]
            {
                new Dictionary<string, string> { ["departure_stage"] = "Screening" },
                new Dictionary<string, string> { ["departure_stage"] = "Screening" },
                new Dictionary<string, string> { ["departure_stage"] = "Doctor" },
                new Dictionary<string, string> { ["departure_stage"] = "Reception" },
            });

        var result = PExitCalculator.Compute(data);

        Assert.Equal(2, result.ScreeningExits);
        Assert.Equal(1, result.DoctorExits);
        Assert.Equal(1, result.ReceptionExited);
        Assert.Equal(2.0 / 3.0, result.ExitProbability, 6);
    }

    [Fact]
    public void PExit_NoCandidates_Throws()
    {
        var data = new DataSet(
            "mem://p-exit",
            DateTime.UtcNow,
            new[] { "departure_stage" },
            new IReadOnlyDictionary<string, string>[]
            {
                new Dictionary<string, string> { ["departure_stage"] = "Reception" },
            });

        Assert.Throws<Validation.DataValidationException>(() => PExitCalculator.Compute(data));
    }

    [Fact]
    public void Export_WritesComputedColumns()
    {
        string outPath = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.csv");
        try
        {
            DataExporter.ExportToCsv(TwoStage(), outPath);
            string[] lines = File.ReadAllLines(outPath);

            Assert.Equal(3, lines.Length); // header + 2 rows
            // Detection sorts stages alphabetically: Doctor before Screening.
            Assert.StartsWith("patient_id,departure_stage,arrival_minutes,inter_arrival_minutes,doctor_start_minutes,doctor_end_minutes,doctor_service_minutes,screening_start_minutes,screening_end_minutes,screening_service_minutes", lines[0]);

            // Row 2 (patient 2): arrival 8:40 ⇒ 520, gap vs 8:15 ⇒ 25,
            // doctor 8:56→9:10 = 14 min, screening 8:42→8:55 = 13 min.
            Assert.Contains(",Doctor,520,25,536,550,14,522,535,13", lines[2]);
        }
        finally
        {
            File.Delete(outPath);
        }
    }
}
/// <summary>
/// D-179: the p_exit denominator is the number of patients who were actually
/// screened, and patients who went straight to a doctor are measured separately
/// as p_bypass.
/// </summary>
public class BypassDetectionTests
{
    private static DataSet File(params (string Departure, bool Screened, bool SeenByDoctor)[] rows)
    {
        var dictionaries = new List<IReadOnlyDictionary<string, string>>();
        int minute = 0;
        foreach (var (departure, screened, seenByDoctor) in rows)
        {
            string t = Time(minute);
            dictionaries.Add(new Dictionary<string, string>
            {
                ["arrival_time"] = t,
                ["departure_stage"] = departure,
                ["reception_start"] = t,
                ["reception_end"] = t,
                ["screening_start"] = screened ? t : "",
                ["screening_end"] = screened ? t : "",
                ["doctor_start"] = seenByDoctor ? t : "",
                ["doctor_end"] = seenByDoctor ? t : "",
            });
            minute += 5;
        }

        return new DataSet(
            "mem://bypass",
            DateTime.UtcNow,
            new[] { "arrival_time", "departure_stage", "reception_start", "reception_end",
                    "screening_start", "screening_end", "doctor_start", "doctor_end" },
            dictionaries);
    }

    private static string Time(int offsetMinutesFromEight)
        => DateTime.Today.AddHours(8).AddMinutes(offsetMinutesFromEight).ToString("H:mm");

    [Fact]
    public void BypassRow_IsCountedSeparatelyAndLeavesThePExitDenominator()
    {
        // 2 screening exits, 2 screened patients who continued, 3 who bypassed.
        var data = File(
            ("Screening", true, false),
            ("Screening", true, false),
            ("Doctor", true, true),
            ("Doctor", true, true),
            ("Doctor", false, true),
            ("Doctor", false, true),
            ("Doctor", false, true));

        var result = PExitCalculator.Compute(data);

        Assert.Equal(2, result.ScreeningExits);
        Assert.Equal(5, result.DoctorExits);
        Assert.Equal(3, result.BypassExits);

        // The three bypass rows are removed from the denominator: 4 screened
        // patients (2 exits + 2 continuations), NOT 7 rows with a departure stage.
        Assert.Equal(4, result.ScreenedPatients);
        Assert.Equal(0.5, result.ExitProbability, 6);
    }

    [Fact]
    public void BypassProbability_IsBypassCountOverEveryArrival()
    {
        var data = File(
            ("Screening", true, false),
            ("Doctor", true, true),
            ("Doctor", false, true),
            ("Reception", false, false));

        var result = PExitCalculator.Compute(data);

        // 1 of 4 arrivals skipped screening, including the patient who reneged at
        // Reception in the denominator.
        Assert.Equal(0.25, PExitCalculator.ComputeBypassProbability(data, result), 6);
    }

    [Fact]
    public void NoScreeningColumn_LeavesTheOldDenominatorIntact()
    {
        // A file that predates the screening columns cannot answer "was this
        // patient screened?", so nothing is inferred and p_exit keeps counting
        // every row with a departure stage.
        var data = new DataSet(
            "mem://no-screening",
            DateTime.UtcNow,
            new[] { "arrival_time", "departure_stage" },
            new IReadOnlyDictionary<string, string>[]
            {
                new Dictionary<string, string> { ["arrival_time"] = "8:15", ["departure_stage"] = "Screening" },
                new Dictionary<string, string> { ["arrival_time"] = "8:40", ["departure_stage"] = "Doctor" },
            });

        var result = PExitCalculator.Compute(data);

        Assert.Equal(0, result.BypassExits);
        Assert.Equal(2, result.ScreenedPatients);
        Assert.Equal(0.5, result.ExitProbability, 6);
    }

    [Fact]
    public void EveryRowBypassed_ThrowsBecausePExitIsUndefined()
    {
        var data = File(("Doctor", false, true), ("Doctor", false, true));

        // Nobody was screened, so "of the screened, how many stopped" has no
        // denominator. Refusing is better than reporting 0/0 as a number.
        Assert.Throws<DataValidationException>(() => PExitCalculator.Compute(data));
    }
}
