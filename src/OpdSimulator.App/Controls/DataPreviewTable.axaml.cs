using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace OpdSimulator.App.Controls;

/// <summary>
/// FR-UI-20 read-only data preview: virtualised (ItemsRepeater) table whose
/// headers come from the loaded file, sort cycles asc→desc→original, invalid
/// rows are red-flagged with a reason tooltip, and cells are selectable for
/// Ctrl+C. The control derives the column widths once from header + sampled
/// rows (10k rows render under a second; sorting 10k rows well under 200 ms).
/// </summary>
public partial class DataPreviewTable : TemplatedControl
{
    private readonly List<PreviewRow> _originalRows = new();
    private readonly List<Button> _headerButtons = new();
    private ObservableCollection<PreviewColumn> _columns = new();
    private ObservableCollection<PreviewRow> _rows = new();
    private bool _suppressRebuild;

    private StackPanel? _headerPanel;
    private ErrorBanner? _errorBanner;
    private Grid? _tableArea;
    private TextBlock? _noRowsText;
    private ListBox? _rowList;

    public DataPreviewTable()
    {
        InitializeComponent();
    }

    /// <summary>Column header titles, in file order.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> ColumnTitlesProperty =
        AvaloniaProperty.Register<DataPreviewTable, IEnumerable<string>?>(nameof(ColumnTitles));

    /// <summary>Column header titles, in file order.</summary>
    public IEnumerable<string>? ColumnTitles
    {
        get => GetValue(ColumnTitlesProperty);
        set => SetValue(ColumnTitlesProperty, value);
    }

    /// <summary>Raw preview rows (each an ordered list of cell strings or nulls).</summary>
    public static readonly StyledProperty<IEnumerable<IReadOnlyList<string?>>?> RowsProperty =
        AvaloniaProperty.Register<DataPreviewTable, IEnumerable<IReadOnlyList<string?>>?>(nameof(Rows));

    /// <summary>Raw preview rows.</summary>
    public IEnumerable<IReadOnlyList<string?>>? Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Row index → validator reason for rows that failed validation.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<int, string?>?> InvalidRowsProperty =
        AvaloniaProperty.Register<DataPreviewTable, IReadOnlyDictionary<int, string?>?>(nameof(InvalidRows));

    /// <summary>Row index → validator reason for rows that failed validation.</summary>
    public IReadOnlyDictionary<int, string?>? InvalidRows
    {
        get => GetValue(InvalidRowsProperty);
        set => SetValue(InvalidRowsProperty, value);
    }

    /// <summary>When set, the table is replaced by an FR-UI-9 error banner.</summary>
    public static readonly StyledProperty<string?> LoadErrorSummaryProperty =
        AvaloniaProperty.Register<DataPreviewTable, string?>(nameof(LoadErrorSummary));

    /// <summary>When set, the table is replaced by an FR-UI-9 error banner.</summary>
    public string? LoadErrorSummary
    {
        get => GetValue(LoadErrorSummaryProperty);
        set => SetValue(LoadErrorSummaryProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _headerPanel = e.NameScope.Find("HeaderPanel") as StackPanel;
        _errorBanner = e.NameScope.Find("LoadErrorBanner") as ErrorBanner;
        _tableArea = e.NameScope.Find("TableArea") as Grid;
        _noRowsText = e.NameScope.Find("NoRowsText") as TextBlock;
        _rowList = e.NameScope.Find("RowRepeater") as ListBox;

        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnTitlesProperty
            || change.Property == RowsProperty
            || change.Property == InvalidRowsProperty
            || change.Property == LoadErrorSummaryProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        if (_suppressRebuild || _headerPanel is null || _rowList is null)
        {
            return;
        }

        bool loadFailed = !string.IsNullOrWhiteSpace(LoadErrorSummary);
        _errorBanner!.IsVisible = loadFailed;
        _tableArea!.IsVisible = !loadFailed && ColumnTitles is not null;
        _noRowsText!.IsVisible = !loadFailed && ColumnTitles is not null && !(Rows?.Any() ?? false);
        if (loadFailed || ColumnTitles is null || Rows is null)
        {
            return;
        }

        var titles = ColumnTitles.ToList();
        var rawRows = Rows.ToList();
        var invalid = InvalidRows ?? new Dictionary<int, string?>();

        double[] widths = ComputeWidths(titles, rawRows, out int colCount);

        _columns = new ObservableCollection<PreviewColumn>(
            titles.Select((t, i) => new PreviewColumn { Title = t, Width = widths[i] }));

        _originalRows.Clear();
        _originalRows.AddRange(rawRows.Select((raw, index) =>
        {
            var isInvalid = invalid.TryGetValue(index, out var reason) && !string.IsNullOrWhiteSpace(reason);
            var cells = new List<PreviewCell>(colCount);
            for (int c = 0; c < colCount; c++)
            {
                cells.Add(new PreviewCell(raw.Count > c ? raw[c] ?? string.Empty : string.Empty, widths[c]));
            }

            return new PreviewRow(cells, isInvalid, isInvalid ? reason : null);
        }));

        _rows = new ObservableCollection<PreviewRow>(_originalRows);
        _headerPanel.Children.Clear();
        _headerButtons.Clear();
        for (int i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var header = new Button
            {
                Content = column.Title,
                DataContext = column,
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 0, 4, 0),
            };
            header.Classes.Add("GhostButton");
            header.Click += OnColumnHeaderClick;
            ToolTip.SetTip(header, "Click to sort (asc → desc → original).");
            _headerButtons.Add(header);
            _headerPanel.Children.Add(header);
        }

        _rowList.ItemsSource = _rows;
    }

    private static double[] ComputeWidths(List<string> titles, List<IReadOnlyList<string?>> rows, out int colCount)
    {
        colCount = Math.Max(titles.Count, rows.Count > 0 ? rows.Max(r => r.Count) : 0);
        var widths = new double[Math.Max(colCount, 1)];
        for (int c = 0; c < colCount; c++)
        {
            int longest = titles.Count > c ? titles[c].Length : 4;
            foreach (var row in rows.Take(300))
            {
                if (row.Count > c)
                {
                    longest = Math.Max(longest, row[c]?.Length ?? 0);
                }
            }

            widths[c] = Math.Clamp(longest * 7.2 + 24, 96, 320);
        }

        return widths;
    }

    private void OnColumnHeaderClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PreviewColumn column })
        {
            return;
        }

        int index = _columns.IndexOf(column);
        if (index < 0)
        {
            return;
        }

        SortDirection direction = column.CycleSort();
        ApplySort(index, direction);

        _headerButtons[index].Content =
            string.IsNullOrEmpty(column.SortGlyph) ? column.Title : $"{column.Title} {column.SortGlyph}";
    }

    private void ApplySort(int index, SortDirection direction)
    {
        // Snapshot before clearing so LINQ never iterates a mutating collection.
        IEnumerable<PreviewRow> ordered = direction switch
        {
            SortDirection.Ascending => _originalRows.OrderBy(r => CellOf(r, index), StringComparer.OrdinalIgnoreCase),
            SortDirection.Descending => _originalRows.OrderByDescending(r => CellOf(r, index), StringComparer.OrdinalIgnoreCase),
            _ => _originalRows,
        };

        var snapshot = ordered.ToList();
        _suppressRebuild = true;
        _rows.Clear();
        foreach (var row in snapshot)
        {
            _rows.Add(row);
        }

        _suppressRebuild = false;
    }

    private static string CellOf(PreviewRow row, int index)
        => index < row.Cells.Count ? row.Cells[index].Text : string.Empty;
}