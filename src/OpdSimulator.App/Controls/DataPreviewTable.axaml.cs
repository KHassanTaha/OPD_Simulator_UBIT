using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using OpdSimulator.App.Services;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Read-only, virtualised preview of an uploaded data file (FR-UI-20).
/// The column set comes from the file; click a header to cycle the sort
/// ascending → descending → original. Invalid rows inherit the FR-UI-17
/// error treatment (red border + icon + specific validator reason tooltip).
/// Row cells are read-only <see cref="TextBox"/>es so text is selectable and
/// Ctrl+C works (FR-UI-20). Data comes from a <see cref="DataPreviewStore"/>.
/// </summary>
public partial class DataPreviewTable : UserControl
{
    private const double AutoBadgeWidth = 40;
    private const double CellMargin = 8;

    private readonly List<TextBlock> _sortIndicators = new();
    private ColumnDefinitions? _columnDefs;

    /// <summary>Creates the preview table.</summary>
    public DataPreviewTable()
    {
        InitializeComponent();
    }

    /// <summary>Loads a new preview, rebuilding the header and row template.</summary>
    public void Load(DataPreviewStore store)
    {
        _sortIndicators.Clear();
        HeaderGrid.Children.Clear();

        // Equal (1*) column widths keep header, rows and per-row grids aligned
        // without shared-size groups; a trailing Auto column hosts the
        // invalid-row badge.
        _columnDefs = new ColumnDefinitions();
        for (var i = 0; i < Math.Max(store.ColumnHeaders.Count, 1); i++)
        {
            _columnDefs.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        _columnDefs.Add(new ColumnDefinition(new GridLength(AutoBadgeWidth, GridUnitType.Auto)));

        BuildHeader(store);

        RowsList.ItemTemplate = new FuncDataTemplate<DataPreviewRow>(
            (row, _) => BuildRow(row, store),
            supportsRecycling: false);
        RowsList.ItemsSource = store.View;
    }

    /// <summary>Empties the preview (e.g., replaced by an error summary).</summary>
    public void Clear()
    {
        HeaderGrid.Children.Clear();
        _sortIndicators.Clear();
        RowsList.ItemTemplate = null;
        RowsList.ItemsSource = null;
    }

    private void BuildHeader(DataPreviewStore store)
    {
        for (var i = 0; i < store.ColumnHeaders.Count; i++)
        {
            var columnIndex = i;

            var label = new TextBlock
            {
                Text = store.ColumnHeaders[i],
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var indicator = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsVisible = false,
            };
            _sortIndicators.Add(indicator);

            var cell = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(CellMargin, 0, CellMargin, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            cell.Children.Add(label);
            cell.Children.Add(indicator);

            Grid.SetColumn(cell, columnIndex);
            cell.Tapped += (_, _) => OnHeaderTapped(store, columnIndex);
            HeaderGrid.Children.Add(cell);

            // The template is x:Name-less, so give the header a name for the
            // accessibility tree while keeping header cells out of tab order.
            AutomationProperties.SetName(cell, $"Sort by {store.ColumnHeaders[i]}");
        }

        HeaderGrid.ColumnDefinitions = _columnDefs!;
    }

    private void OnHeaderTapped(DataPreviewStore store, int columnIndex)
    {
        store.ToggleSort(columnIndex);

        for (var i = 0; i < _sortIndicators.Count; i++)
        {
            var isActive = store.ActiveSort?.ColumnIndex == i;
            _sortIndicators[i].IsVisible = isActive;
            _sortIndicators[i].Text =
                store.ActiveSort?.Direction == SortDirection.Descending ? "\uE70E" : "\uE70D";
        }

        // Reassign the source so the virtualizing ListBox realises the new order.
        RowsList.ItemsSource = null;
        RowsList.ItemsSource = store.View;
    }

    private Control BuildRow(DataPreviewRow row, DataPreviewStore store)
    {
        var grid = new Grid { ColumnDefinitions = CloneColumnDefs() };

        for (var i = 0; i < store.ColumnHeaders.Count; i++)
        {
            var cell = new TextBox
            {
                Text = i < row.Cells.Length ? row.Cells[i] : string.Empty,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(CellMargin, 4, CellMargin, 4),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (row.IsInvalid)
            {
                // FR-UI-17 treatment: red is never the only cue — the badge
                // column and tooltip name the specific validator reason.
                cell.Background = ResolveBrush("BrushErrorBackground");
                cell.Foreground = ResolveBrush("BrushErrorDark");
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        if (row.IsInvalid)
        {
            var badge = new TextBlock
            {
                Text = "\uE9CE", // warning triangle
                FontSize = 14,
                Foreground = ResolveBrush("BrushError"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, CellMargin, 0),
            };
            ToolTip.SetTip(badge, row.ValidationReason ?? "This row failed validation.");
            Grid.SetColumn(badge, store.ColumnHeaders.Count);
            grid.Children.Add(badge);
        }

        return grid;
    }

    private ColumnDefinitions CloneColumnDefs()
    {
        var clone = new ColumnDefinitions();
        foreach (var column in _columnDefs!)
        {
            clone.Add(new ColumnDefinition(column.Width));
        }

        return clone;
    }

    private IBrush? ResolveBrush(string resourceKey)
        => this.TryFindResource(resourceKey, out var value) ? value as IBrush : null;
}