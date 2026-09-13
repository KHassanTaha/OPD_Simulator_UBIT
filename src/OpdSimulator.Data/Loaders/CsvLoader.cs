namespace OpdSimulator.Data.Loaders;

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

/// <summary>
/// Loads a <c>.csv</c> file as a <see cref="DataSet"/>.
/// </summary>
/// <remarks>
/// Uses CsvHelper (D-004) with invariant-culture parsing so numbers and dates are
/// interpreted identically on Linux and Windows. The first record is the header
/// row; <c>null</c> fields (missing trailing values) become empty strings so the
/// validator sees them as missing data.
/// </remarks>
public sealed class CsvLoader : IDataSource
{
    /// <inheritdoc />
    public string FormatName => "CSV (CsvHelper)";

    /// <inheritdoc />
    public bool CanHandle(string extension)
        => string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public DataSet Load(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,     // tolerate ragged rows; blanks surface as ""
            BadDataFound = null,          // keep raw text rather than throwing mid-file
        });

        csv.Read();
        csv.ReadHeader();
        if (csv.HeaderRecord is null || csv.HeaderRecord.Length == 0)
            throw new InvalidDataException($"'{path}' contains no header row.");

        string[] columns = csv.HeaderRecord;

        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>(columns.Length, StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < columns.Length; c++)
                row[columns[c]] = csv.GetField(c) ?? string.Empty;
            rows.Add(row);
        }

        return new DataSet(path, DateTime.UtcNow, columns, rows);
    }
}