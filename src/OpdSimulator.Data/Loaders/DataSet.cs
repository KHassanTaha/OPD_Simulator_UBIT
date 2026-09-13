namespace OpdSimulator.Data.Loaders;

/// <summary>
/// An in-memory representation of an uploaded spreadsheet or CSV file.
/// </summary>
/// <remarks>
/// Holds the raw cell data exactly as read from disk: column names in header
/// order and one row per patient, each row a dictionary keyed by column name.
/// Cells are kept as strings so validation and parsing (which own the rules)
/// decide structure and types — the loader never silently coerces values.
/// </remarks>
public sealed class DataSet
{
    /// <summary>
    /// Creates a dataset.
    /// </summary>
    /// <param name="sourcePath">The absolute path the data was loaded from.</param>
    /// <param name="loadedAt">When the file was loaded.</param>
    /// <param name="columns">Column headers in file order.</param>
    /// <param name="rows">Cell data, one element per data row.</param>
    public DataSet(
        string sourcePath,
        DateTime loadedAt,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        SourcePath = sourcePath;
        LoadedAt = loadedAt;
        Columns = columns;
        Rows = rows;
    }

    /// <summary>The absolute path the data was loaded from.</summary>
    public string SourcePath { get; }

    /// <summary>When the file was loaded.</summary>
    public DateTime LoadedAt { get; }

    /// <summary>Column headers in file order.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>Cell data, one element per data row (row index = data-row number).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; }

    /// <summary>Number of data rows (excluding the header row).</summary>
    public int RowCount => Rows.Count;
}