namespace OpdSimulator.App.ViewModels;

using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;

/// <summary>
/// Phase 7D: the loaded-file preview state, extracted from
/// <see cref="ResultsPanelViewModel"/> when the data preview moved from the
/// Results panel to the merged Input tab. Owns the column titles, rows and
/// invalid-row map the <see cref="DataPreviewTable"/> control consumes, plus
/// the validation summary that drives the tab's banner.
/// </summary>
/// <remarks>
/// The four preview collections keep the reference-assigned types the
/// <see cref="DataPreviewTable"/> control's styled properties expect
/// (<see cref="IEnumerable{T}"/> / <see cref="IReadOnlyDictionary{TKey,TValue}"/>):
/// the control rebuilds only when a property reference changes, so each
/// <see cref="SetPreview"/> assigns fresh instances (mirrors the pre-7D
/// behaviour). The four collections are therefore a flat projection, not an
/// <c>ObservableCollection</c> of row view models — the shared control is
/// deliberately left untouched.
/// </remarks>
public partial class InputPreviewViewModel : ObservableObject
{
    private DataBindingResult? _binding;

    /// <summary>Column header titles from the loaded file (null before a file loads).</summary>
    [ObservableProperty]
    private IEnumerable<string>? _columnTitles;

    /// <summary>Raw preview rows, each an ordered list of cell strings or nulls.</summary>
    [ObservableProperty]
    private IEnumerable<IReadOnlyList<string?>>? _rows;

    /// <summary>Row index → validator reason for invalid rows.</summary>
    [ObservableProperty]
    private IReadOnlyDictionary<int, string?>? _invalidRows;

    /// <summary>Load-failure summary that replaces the preview table (FR-UI-9).</summary>
    [ObservableProperty]
    private string? _loadErrorSummary;

    /// <summary>True when the current file has any validation problem (row issues or a load failure).</summary>
    public bool HasValidationIssues =>
        _binding is not null && (LoadErrorSummary is not null || _binding.Issues.Count > 0);

    /// <summary>True when the file could not be loaded at all (a hard error, not a row warning).</summary>
    public bool HasValidationErrors => LoadErrorSummary is not null;

    /// <summary>Banner accent: red for a load failure, amber for row-level issues.</summary>
    public BannerSeverity Severity =>
        HasValidationErrors ? BannerSeverity.Error : BannerSeverity.Warning;

    /// <summary>The message the validation banner shows, or null when there is nothing to report.</summary>
    public string? BannerMessage => LoadErrorSummary is not null
        ? LoadErrorSummary
        : _binding is { Issues.Count: > 0 }
            ? IssueSummary
            : null;

    /// <summary>
    /// The first five validator issues as one line, with a trailing
    /// "…and N more" when the file has more (FR-UI-20 preview contract).
    /// </summary>
    public string IssueSummary
    {
        get
        {
            if (_binding is not { Issues.Count: > 0 } binding)
            {
                return string.Empty;
            }

            const int maxShown = 5;
            string shown = string.Join("; ", binding.Issues.Take(maxShown).Select(issue => issue.ToString()));
            int remaining = binding.Issues.Count - maxShown;
            return remaining > 0 ? $"{shown} …and {remaining} more" : shown;
        }
    }

    /// <summary>True when the preview has at least one row to show.</summary>
    public bool HasRows => Rows is not null && Rows.Any();

    /// <summary>
    /// Projects a loaded binding into the preview collections. A null binding
    /// (or one with no parsed dataset) clears the rows and surfaces the load
    /// error, if any, through <see cref="LoadErrorSummary"/>.
    /// </summary>
    /// <param name="binding">The analysed data binding, or null to clear.</param>
    public void SetPreview(DataBindingResult? binding)
    {
        _binding = binding;
        LoadErrorSummary = binding?.ErrorMessage;

        if (binding?.DataSet is { } dataSet)
        {
            ColumnTitles = dataSet.Columns.ToList();
            Rows = dataSet.Rows
                .Select(row => (IReadOnlyList<string?>)row.Values.Select(v => (string?)v).ToList())
                .ToList();

            // RowNumber is 1-based data-row number (row 1 = first data row below
            // the header); the preview widget uses 0-based indices.
            InvalidRows = binding.Issues
                .Where(i => i.RowNumber > 0)
                .GroupBy(i => i.RowNumber - 1)
                .ToDictionary(g => g.Key, g => (string?)g.First().Reason);
        }
        else
        {
            ColumnTitles = null;
            Rows = null;
            InvalidRows = null;
        }

        OnPropertyChanged(nameof(HasValidationIssues));
        OnPropertyChanged(nameof(HasValidationErrors));
        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(BannerMessage));
        OnPropertyChanged(nameof(IssueSummary));
        OnPropertyChanged(nameof(HasRows));
    }

    /// <summary>Returns the preview to its no-file state.</summary>
    public void Clear() => SetPreview(null);
}
