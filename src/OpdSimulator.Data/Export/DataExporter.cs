namespace OpdSimulator.Data.Export;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;

/// <summary>
/// Writes a validated dataset to a clean, analysis-ready CSV for SPSS (FR-DATA-8).
/// </summary>
/// <remarks>
/// The export is one row per patient with computed columns added:
/// <list type="bullet">
/// <item>patient_id — 1-based row number (stable identity for joins).</item>
/// <item>departure_stage — copied as-is.</item>
/// <item>arrival_minutes — parsed arrival time as minutes since midnight.</item>
/// <item>inter_arrival_minutes — gap to the previous patient (first row = blank).</item>
/// <item>per stage: <c>&lt;stage&gt;_start_minutes</c>, <c>&lt;stage&gt;_end_minutes</c>,
/// <c>&lt;stage&gt;_service_minutes</c>.</item>
/// </list>
/// Numbers are written in invariant culture so SPSS reads the same values on any OS.
/// </remarks>
public static class DataExporter
{
    /// <summary>
    /// Exports the dataset to the given CSV path.
    /// </summary>
    /// <param name="dataSet">IDataSource data (assumed validated).</param>
    /// <param name="outputPath">Absolute destination path for the CSV.</param>
    /// <exception cref="ArgumentNullException">If either argument is null.</exception>
    public static void ExportToCsv(DataSet dataSet, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(outputPath);

        var pairs = StagePairDetector.Detect(dataSet.Columns).Pairs;

        using var writer = new StreamWriter(outputPath, append: false);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        var header = new List<string> { "patient_id", "departure_stage", "arrival_minutes", "inter_arrival_minutes" };
        foreach (var p in pairs)
        {
            header.Add($"{p.Stage}_start_minutes");
            header.Add($"{p.Stage}_end_minutes");
            header.Add($"{p.Stage}_service_minutes");
        }
        foreach (var h in header)
            csv.WriteField(h);
        csv.NextRecord();

        double? previousArrival = null;
        for (int i = 0; i < dataSet.Rows.Count; i++)
        {
            var row = dataSet.Rows[i];
            bool hasArrival = false;
            double arrival = 0;
            if (row.TryGetValue("arrival_time", out string? arrivalText) && TimeParser.TryParse(arrivalText ?? string.Empty, out arrival))
                hasArrival = true;
            string departure = row.TryGetValue("departure_stage", out string? depText) ? depText?.Trim() ?? "" : "";

            csv.WriteField(i + 1);
            csv.WriteField(departure);
            csv.WriteField(hasArrival ? Fmt(arrival) : string.Empty);
            csv.WriteField(hasArrival && previousArrival.HasValue ? Fmt(arrival - previousArrival.Value) : string.Empty);

            foreach (var p in pairs)
            {
                bool hasStart = false;
                bool hasEnd = false;
                double start = 0;
                double end = 0;
                if (row.TryGetValue(p.StartColumn, out string? st) && TimeParser.TryParse(st ?? string.Empty, out start))
                    hasStart = true;
                if (row.TryGetValue(p.EndColumn, out string? et) && TimeParser.TryParse(et ?? string.Empty, out end))
                    hasEnd = true;

                csv.WriteField(hasStart ? Fmt(start) : string.Empty);
                csv.WriteField(hasEnd ? Fmt(end) : string.Empty);
                csv.WriteField(hasStart && hasEnd && end >= start ? Fmt(end - start) : string.Empty);
            }
            csv.NextRecord();

            if (hasArrival)
                previousArrival = arrival;
        }
    }

    private static string Fmt(double value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);
}