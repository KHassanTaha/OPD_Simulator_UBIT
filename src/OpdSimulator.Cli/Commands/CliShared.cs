namespace OpdSimulator.Cli.Commands;

using System.Globalization;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;

/// <summary>
/// Small helpers shared by the M2 data commands (verify, fit, simulate-data, export).
/// </summary>
internal static class CliShared
{
    /// <summary>
    /// Loads the file via the loader factory and validates it.
    /// </summary>
    /// <param name="path">Data file path.</param>
    /// <param name="dataSet">The validated dataset on success.</param>
    /// <param name="errorLine">A user-facing error line to print to stderr on failure.</param>
    /// <returns><see langword="true"/> when the file loaded and passed validation.</returns>
    public static bool TryLoadValidated(string path, out DataSet? dataSet, out string errorLine)
    {
        dataSet = null;
        errorLine = string.Empty;

        try
        {
            var loader = new DataLoaderFactory().Create(path);
            dataSet = loader.Load(path);
            DataValidator.Validate(dataSet);
            return true;
        }
        catch (DataValidationException ex)
        {
            errorLine = $"Data validation failed with {ex.Issues.Count} issue(s).";
            return false;
        }
        catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidDataException)
        {
            errorLine = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads a raw string as an invariant-culture double.
    /// </summary>
    public static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}