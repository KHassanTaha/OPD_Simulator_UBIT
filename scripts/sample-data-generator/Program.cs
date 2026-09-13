using System.Globalization;
using ClosedXML.Excel;

/// <summary>
/// Generates the committed sample data files (and the dirty validation fixture)
/// for the Milestone-2 demo, deterministically seeded.
/// </summary>
/// <remarks>
/// Distribution CULTURE USED (documented in the viva):
/// <list type="bullet">
/// <item>Inter-arrival times: Exp(λ = 0.5/min) → mean 2.0 minutes.</item>
/// <item>Service times: Exp(μ = 0.666…/min) → mean 1.5 minutes.</item>
/// <item>ρ with 1 server = 1.5/2.0 = 0.75 → stable, near-jam demo.</item>
/// </list>
/// Inverse-CDF transform for each draw: X = −ln(1−U)/rate, with U from a seeded
/// <c>System.Random(42)</c>. Arrivals start at 08:15 (495 min). All rows exit at
/// Screening ⇒ p_exit = 1.0, matching CONTEXT §5.5.
/// </remarks>
internal static class Program
{
    public static int Main(string[] args)
    {
        string root = args.Length > 0
            ? args[0]
            : throw new ArgumentException("Pass the repository root as the only argument.");

        var rows = GenerateRows(60, 42);

        // samples/sample_patients.xlsx — real Excel times also readable as fractions.
        string xlsx = Path.Combine(root, "samples", "sample_patients.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(xlsx)!);
        WriteXlsx(xlsx, rows);

        // samples/sample_patients.csv — plain text twin for terminal workflows.
        string csv = Path.Combine(root, "samples", "sample_patients.csv");
        WriteCsv(csv, rows);

        // Dirty fixture the validator must reject (specific row numbers asserted
        // in DataValidatorTests-style repro).
        string dirty = Path.Combine(root, "tests", "OpdSimulator.Data.Tests", "Fixtures", "dirty_missing.xlsx");
        Directory.CreateDirectory(Path.GetDirectoryName(dirty)!);
        WriteDirtyFixture(dirty, rows);

        Console.WriteLine($"Wrote: {xlsx}");
        Console.WriteLine($"Wrote: {csv}");
        Console.WriteLine($"Wrote: {dirty}");
        return 0;
    }

    private sealed record Row(string Arrival, double ArrivalMinutes, string ScreeningStart, string ScreeningEnd);

    private static List<Row> GenerateRows(int count, int seed)
    {
        var rng = new Random(seed);
        var rows = new List<Row>(count);

        double clockDays = 8.0 * 60 + 15; // 08:15 minutes since midnight
        foreach (int _ in Enumerable.Range(0, count))
        {
            double arrivalGap = SampleExponential(rng, 0.5);       // mean 2.0 min
            clockDays += arrivalGap;

            double service = SampleExponential(rng, 1.0 / 1.5);    // mean 1.5 min
            double bump = 0.5 + SampleExponential(rng, 0.5);       // small pre-service delay (mean +1 min)
            double start = clockDays + bump;
            double end = start + service;

            rows.Add(new Row(
                TimeText(clockDays),
                clockDays,
                TimeText(start),
                TimeText(end)));
        }

        return rows;
    }

    private static double SampleExponential(Random rng, double rate)
        => -Math.Log(1.0 - rng.NextDouble()) / rate;

    private static string TimeText(double minutes)
    {
        // Second precision (HH:MM:SS) — see D-048. Minute-precision storage made
        // the fitted distribution look discrete, so chi-square rejected a true
        // exponential at p ≈ 0. Quantising at 1/60 min keeps the distortion far
        // below the bin width (~13 min with k = 8) instead of creating ties.
        long totalSeconds = (long)Math.Round(minutes * 60.0);
        long h = totalSeconds / 3600;
        long m = (totalSeconds % 3600) / 60;
        long s = totalSeconds % 60;
        return $"{h}:{m:D2}:{s:D2}";
    }

    private static void WriteXlsx(string path, List<Row> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "arrival_time";
        sheet.Cell(1, 2).Value = "departure_stage";
        sheet.Cell(1, 3).Value = "screening_start";
        sheet.Cell(1, 4).Value = "screening_end";

        for (int i = 0; i < rows.Count; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Arrival;
            sheet.Cell(i + 2, 2).Value = "Screening";
            sheet.Cell(i + 2, 3).Value = rows[i].ScreeningStart;
            sheet.Cell(i + 2, 4).Value = rows[i].ScreeningEnd;
        }
        book.SaveAs(path);
    }

    private static void WriteCsv(string path, List<Row> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("arrival_time,departure_stage,screening_start,screening_end");
        foreach (var r in rows)
            sb.AppendLine($"{r.Arrival},Screening,{r.ScreeningStart},{r.ScreeningEnd}");
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteDirtyFixture(string path, List<Row> clean)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Data");
        sheet.Cell(1, 1).Value = "arrival_time";
        sheet.Cell(1, 2).Value = "departure_stage";
        sheet.Cell(1, 3).Value = "screening_start";
        sheet.Cell(1, 4).Value = "screening_end";

        void Set(int row, string arrival, string stage, string start, string end)
        {
            sheet.Cell(row, 1).Value = arrival;
            sheet.Cell(row, 2).Value = stage;
            sheet.Cell(row, 3).Value = start;
            sheet.Cell(row, 4).Value = end;
        }

        // Row 2: clean.
        Set(2, clean[0].Arrival, "Screening", clean[0].ScreeningStart, clean[0].ScreeningEnd);
        // Row 3: arrival cell is empty (missing value).
        Set(3, "", "Screening", clean[1].ScreeningStart, clean[1].ScreeningEnd);
        // Row 4: bad departure stage.
        Set(4, clean[2].Arrival, "Laboratory", clean[2].ScreeningStart, clean[2].ScreeningEnd);
        // Row 5: service ends before it starts.
        Set(5, clean[3].Arrival, "Screening", "9:30", "9:10");
        // Row 6: completely empty row.
        Set(6, "", "", "", "");
        // Row 7: arrival earlier than row 6/2 (out of order against 8:15).
        Set(7, "8:00", "Screening", clean[4].ScreeningStart, clean[4].ScreeningEnd);

        book.SaveAs(path);
    }
}