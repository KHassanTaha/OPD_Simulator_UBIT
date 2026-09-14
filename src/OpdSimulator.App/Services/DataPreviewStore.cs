using System;
using System.Collections.Generic;
using System.Linq;

namespace OpdSimulator.App.Services;

/// <summary>One sortable virtual column state.</summary>
public enum SortDirection
{
    /// <summary>Column not sorted (original file order).</summary>
    Original,

    /// <summary>Sorted ascending.</summary>
    Ascending,

    /// <summary>Sorted descending.</summary>
    Descending,
}

/// <summary>Immutable description of the active sort (for header indicators).</summary>
public sealed record SortState(int ColumnIndex, SortDirection Direction);

/// <summary>
/// One previewable row: its cells plus the FR-UI-20 invalid-row decoration
/// (red border + icon + specific validator reason). Pure data, no UI.
/// </summary>
public sealed class DataPreviewRow
{
    /// <summary>Cell values in column order (one per header).</summary>
    public required string[] Cells { get; init; }

    /// <summary>1-based position in the source file (stable across sorting).</summary>
    public required int SourceIndex { get; init; }

    /// <summary>True when this row failed validation and must be highlighted.</summary>
    public bool IsInvalid { get; init; }

    /// <summary>Specific validator reason shown in the invalid-row tooltip.</summary>
    public string? ValidationReason { get; init; }
}

/// <summary>
/// Pure data holder behind the preview table (FR-UI-20): column headers, the
/// current row view, and the asc → desc → original sort cycle. No UI types,
/// so sorting and the cycle logic are unit-testable.
/// </summary>
public sealed class DataPreviewStore
{
    private readonly List<DataPreviewRow> _rows = new();

    /// <summary>Gets the column headers, in order.</summary>
    public IReadOnlyList<string> ColumnHeaders { get; private set; } = Array.Empty<string>();

    /// <summary>Gets the rows in their current sort order.</summary>
    public IReadOnlyList<DataPreviewRow> View => _rows;

    /// <summary>Gets the active sort, or null when in original order.</summary>
    public SortState? ActiveSort { get; private set; }

    /// <summary>Replaces the whole preview content and resets the sort.</summary>
    /// <param name="headers">Column names, in order.</param>
    /// <param name="rows">Rows to preview.</param>
    public void SetData(IReadOnlyList<string> headers, IEnumerable<DataPreviewRow> rows)
    {
        ColumnHeaders = headers.ToArray();
        _rows.Clear();
        _rows.AddRange(rows);
        ActiveSort = null;
    }

    /// <summary>
    /// Cycles the given column through original → ascending → descending →
    /// original. Other columns return to original order when a new column wins.
    /// </summary>
    public void ToggleSort(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= ColumnHeaders.Count)
        {
            return;
        }

        var next = ActiveSort is { ColumnIndex: var c } && c == columnIndex
            ? ActiveSort.Direction switch
            {
                SortDirection.Original => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                _ => SortDirection.Original,
            }
            : SortDirection.Ascending;

        if (next == SortDirection.Original)
        {
            _rows.Sort((l, r) => l.SourceIndex.CompareTo(r.SourceIndex));
            ActiveSort = null;
            return;
        }

        var sign = next == SortDirection.Ascending ? 1 : -1;
        _rows.Sort((l, r) =>
        {
            // Ordinal comparison is deliberately simple: the preview is a
            // verification surface, not a calculator (FR-UI-20).
            var comparison = string.CompareOrdinal(
                Cell(l, columnIndex),
                Cell(r, columnIndex));
            return comparison == 0 ? l.SourceIndex.CompareTo(r.SourceIndex) : comparison * sign;
        });

        ActiveSort = new SortState(columnIndex, next);
    }

    private static string Cell(DataPreviewRow row, int column)
        => column < row.Cells.Length ? row.Cells[column] : string.Empty;
}