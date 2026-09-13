namespace OpdSimulator.Data.Loaders;

/// <summary>
/// Chooses the file-format loader by extension (FR-DATA-1).
/// </summary>
/// <remarks>
/// Dispatches <c>.xlsx</c> to <see cref="ExcelLoader"/> and <c>.csv</c> to
/// <see cref="CsvLoader"/>; anything else is rejected before I/O begins.
/// </remarks>
public sealed class DataLoaderFactory
{
    private static readonly IDataSource[] Loaders =
    {
        new ExcelLoader(),
        new CsvLoader(),
    };

    /// <summary>
    /// Creates a loader for the given file path, or throws on an unknown format.
    /// </summary>
    /// <param name="path">The file to load.</param>
    /// <returns>A loader whose <see cref="IDataSource.CanHandle"/> matched the extension.</returns>
    /// <exception cref="FileNotFoundException">If the file does not exist.</exception>
    /// <exception cref="NotSupportedException">If the extension is not <c>.xlsx</c> or <c>.csv</c>.</exception>
    public IDataSource Create(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Data file not found: '{path}'.", path);

        string ext = Path.GetExtension(path);
        var loader = Loaders.FirstOrDefault(l => l.CanHandle(ext))
            ?? throw new NotSupportedException(
                $"Unsupported data format '{ext}'. Supported: .xlsx, .csv (FR-DATA-1).");

        return loader;
    }
}