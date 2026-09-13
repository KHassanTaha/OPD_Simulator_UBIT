namespace OpdSimulator.Data.Loaders;

/// <summary>
/// A file-format loader that reads structured tabular data into a <see cref="DataSet"/>.
/// </summary>
/// <remarks>
/// One implementation per supported format (Excel via ClosedXML, CSV via CsvHelper).
/// <see cref="CanHandle"/> answers whether this loader recognises a file extension,
/// so <see cref="DataLoaderFactory"/> can dispatch on it (FR-DATA-1).
/// </remarks>
public interface IDataSource
{
    /// <summary>
    /// Human-readable name of the loader (used in logs and error messages).
    /// </summary>
    string FormatName { get; }

    /// <summary>
    /// Whether this loader can load a file with the given extension.
    /// </summary>
    /// <param name="extension">File extension, e.g. <c>".xlsx"</c> or <c>".csv"</c>.</param>
    /// <returns><see langword="true"/> if this loader owns the format.</returns>
    bool CanHandle(string extension);

    /// <summary>
    /// Loads the file at <paramref name="path"/> into a <see cref="DataSet"/>.
    /// </summary>
    /// <param name="path">Absolute path to the file.</param>
    /// <returns>The parsed dataset (header row becomes <see cref="DataSet.Columns"/>).</returns>
    DataSet Load(string path);
}