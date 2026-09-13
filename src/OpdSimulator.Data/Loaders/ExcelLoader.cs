namespace OpdSimulator.Data.Loaders;

using System.Globalization;
using ClosedXML.Excel;

/// <summary>
/// Loads the first worksheet of an <c>.xlsx</c> workbook as a <see cref="DataSet"/>.
/// </summary>
/// <remarks>
/// Uses ClosedXML (D-004). Rules: the first worksheet is read; row 1 is the header
/// row; blank cell values become empty strings so validation can report precisely
/// where data is missing. Cells are read as raw text (<see cref="XLCellValue"/>),
/// never coerced to numbers/dates by the loader.
/// </remarks>
public sealed class ExcelLoader : IDataSource
{
    /// <inheritdoc />
    public string FormatName => "Excel (ClosedXML)";

    /// <inheritdoc />
    public bool CanHandle(string extension)
        => string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public DataSet Load(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();

        int usedColumns = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        int usedRows = sheet.LastRowUsed()?.RowNumber() ?? 0;

        if (usedColumns == 0 || usedRows < 1)
            throw new InvalidDataException(
                $"'{path}' contains no usable table (no columns and/or no header row).");

        var columns = new List<string>(usedColumns);
        for (int c = 1; c <= usedColumns; c++)
            columns.Add(ReadCell(sheet, 1, c));

        var rows = new List<IReadOnlyDictionary<string, string>>(Math.Max(0, usedRows - 1));
        for (int r = 2; r <= usedRows; r++)
        {
            var row = new Dictionary<string, string>(usedColumns, StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < usedColumns; c++)
                row[columns[c]] = ReadCell(sheet, r, c + 1);
            rows.Add(row);
        }

        return new DataSet(path, DateTime.UtcNow, columns, rows);
    }

    private static string ReadCell(IXLWorksheet sheet, int row, int col)
    {
        var cell = sheet.Cell(row, col);
        if (cell.IsEmpty())
            return string.Empty;

        // ClosedXML models each cell as an XLCellValue typed by its stored type;
        // GetValue<object>() round-trips poorly, so switch on the declared type.
        // Numbers and dates render invariant so text round-trips identically on
        // Linux and Windows.
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Boolean => value.GetBoolean().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            XLDataType.Text => value.GetText(),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }
}