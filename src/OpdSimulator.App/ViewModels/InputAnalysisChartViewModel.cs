namespace OpdSimulator.App.ViewModels;

using Avalonia.Controls;
using OpdSimulator.App.Services;

/// <summary>
/// One chart card on the Input Analysis tab (Phase 6C): the heading, the
/// fit/chi-square caption line, and the already-built chart control. The
/// control is built on the UI thread by <see cref="ChartControlBuilder"/> and
/// simply stored here for the card to bind to (G5).
/// </summary>
/// <param name="title">Card heading shown by the <c>ChartCard</c> title slot.</param>
/// <param name="caption">Fit + chi-square verdict line, or null when the fit failed.</param>
/// <param name="hasSeries">False when the fit failed — the card shows its empty state.</param>
/// <param name="chartContent">The chart control, or null when <paramref name="hasSeries"/> is false.</param>
public sealed class InputAnalysisChartViewModel(
    string title,
    string? caption,
    bool hasSeries,
    Control? chartContent)
{
    /// <summary>Card heading, e.g. "Inter-arrival time (minutes)".</summary>
    public string Title { get; } = title;

    /// <summary>Fit + chi-square verdict line, or null when the fit failed.</summary>
    public string? Caption { get; } = caption;

    /// <summary>True when the card has a drawable histogram; false shows the card's empty state.</summary>
    public bool HasSeries { get; } = hasSeries;

    /// <summary>Inverse of <see cref="HasSeries"/>, bound to the card's <c>ShowEmptyState</c>.</summary>
    public bool ShowEmptyState => !HasSeries;

    /// <summary>The chart control hosted inside the card (null on the empty state).</summary>
    public Control? ChartContent { get; } = chartContent;
}