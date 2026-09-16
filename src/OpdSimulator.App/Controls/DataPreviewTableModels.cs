using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpdSimulator.App.Controls;

/// <summary>Sort state of a preview column header.</summary>
public enum SortDirection
{
    /// <summary>Original (file) order restored.</summary>
    None,

    /// <summary>Ascending.</summary>
    Ascending,

    /// <summary>Descending.</summary>
    Descending,
}

/// <summary>Resolves theme brushes defensively (rows may be built before the
/// template is applied, but always after App theme load in real paths).</summary>
internal static class ThemeBrush
{
    internal static IBrush Get(string key, IBrush fallback)
        => Application.Current is { } app
            && app.Resources.TryGetResource(key, null, out var value)
            && value is IBrush brush
                ? brush
                : fallback;
}

/// <summary>
/// A preview column header carrying its fixed pixel width and current
/// sort-affordance glyph; raising change notifications so the header re-renders
/// when the user cycles a sort (AGENTS §16.10).
/// </summary>
public partial class PreviewColumn : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private double _width = 160;

    [ObservableProperty]
    private string _sortGlyph = string.Empty;

    /// <summary>Cycles None → Ascending → Descending → None; returns the new state.</summary>
    public SortDirection CycleSort()
    {
        SortDirection next = SortGlyph switch
        {
            string g when g == "\u25B2" => SortDirection.Descending,
            string g when g == "\u25BC" => SortDirection.None,
            _ => SortDirection.Ascending,
        };

        SortGlyph = next switch
        {
            SortDirection.Ascending => "\u25B2",
            SortDirection.Descending => "\u25BC",
            _ => string.Empty,
        };

        return next;
    }
}

/// <summary>One cell (already width-sized) inside a preview row.</summary>
public sealed class PreviewCell
{
    public PreviewCell(string text, double width)
    {
        Text = text;
        Width = width;
    }

    /// <summary>The cell's display text (empty for null/blank).</summary>
    public string Text { get; }

    /// <summary>Fixed pixel width shared with the matching column header.</summary>
    public double Width { get; }
}

/// <summary>
/// One preview row: horizontally repeated <see cref="PreviewCell"/>s plus the
/// invalid-state treatment (FR-UI-20 / FR-UI-17 link).
/// </summary>
public sealed class PreviewRow
{
    public PreviewRow(IReadOnlyList<PreviewCell> cells, bool isInvalid, string? invalidReason)
    {
        Cells = cells;
        IsInvalid = isInvalid;
        InvalidReason = invalidReason;

        // FR-UI-17 link: invalid rows inherit the red treatment (background +
        // left accent) rather than a one-off look.
        if (isInvalid)
        {
            RowBackground = ThemeBrush.Get("BrushErrorBackground", Avalonia.Media.Brushes.MistyRose);
            RowBorderBrush = ThemeBrush.Get("BrushError", new SolidColorBrush(Avalonia.Media.Colors.Red));
            RowBorderThickness = new Avalonia.Thickness(3, 0, 0, 0);
        }
        else
        {
            RowBackground = ThemeBrush.Get("BrushBackgroundPanel", Avalonia.Media.Brushes.Transparent);
            RowBorderBrush = Avalonia.Media.Brushes.Transparent;
            RowBorderThickness = new Avalonia.Thickness(0);
        }
    }

    /// <summary>The cells, left to right.</summary>
    public IReadOnlyList<PreviewCell> Cells { get; }

    /// <summary>True when the row failed validation (red treatment + tooltip).</summary>
    public bool IsInvalid { get; }

    /// <summary>The specific validator reason shown in the row tooltip.</summary>
    public string? InvalidReason { get; }

    /// <summary>Background brush, red-tinted for invalid rows (FR-UI-17).</summary>
    public IBrush RowBackground { get; }

    /// <summary>Left accent brush for invalid rows.</summary>
    public IBrush RowBorderBrush { get; }

    /// <summary>Left accent thickness for invalid rows.</summary>
    public Avalonia.Thickness RowBorderThickness { get; }
}