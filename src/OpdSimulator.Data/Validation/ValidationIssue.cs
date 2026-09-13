namespace OpdSimulator.Data.Validation;

/// <summary>
/// A single data-quality problem found by <see cref="DataValidator"/>.
/// </summary>
/// <param name="RowNumber">1-based data row number (row 1 = first data row below the header).
/// Zero means a file-level problem (missing column, no stage pairs).</param>
/// <param name="ColumnName">The offending column, or <see langword="null"/> for row-level problems.</param>
/// <param name="Reason">Human-readable description of the problem.</param>
public sealed record ValidationIssue(int RowNumber, string? ColumnName, string Reason)
{
    /// <summary>
    /// Renders the issue as a one-line, CLI-friendly message.
    /// </summary>
    /// <returns>e.g. <c>Row 4, column 'arrival_time': missing value</c>.</returns>
    public override string ToString()
        => RowNumber == 0
            ? $"Column '{ColumnName}': {Reason}"
            : $"Row {RowNumber}{(ColumnName is null ? "" : $", column '{ColumnName}'")}: {Reason}";
}