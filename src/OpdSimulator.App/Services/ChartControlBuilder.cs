namespace OpdSimulator.App.Services;

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

/// <summary>
/// Builds the <see cref="CartesianChart"/> controls for the Input Analysis
/// cards (Phase 6C). Lives in Services, not the view model, because it owns
/// the LiveCharts — UI — types; view models hold the built controls. Called on
/// the UI thread (G5): LiveCharts controls are Avalonia controls and cannot be
/// created on the background thread.
/// </summary>
/// <remarks>
/// Every colour resolves a ChartTheme.axaml brush (AGENTS §16.3 — no hex in
/// views); the hex constants below are only the headless-test fallbacks that
/// match those exact theme colors (1B7A4C / 0B7285 / C9D1CC / 44504A / A4B3A9 /
/// FFFFFF / 1A2320). LiveCharts legend/tooltip are Paint objects set per chart.
/// Multi-column series sharing a coordinate render side-by-side (grouped) by
/// default — stacking would require the separate <c>StackedColumnSeries</c>.
/// </remarks>
public static class ChartControlBuilder
{
    /// <summary>Fixed chart height so every card's plot is uniformly sized.</summary>
    internal const double ChartHeight = 220;

    /// <summary>
    /// Creates the column + fitted-line chart for one histogram card. Returns
    /// null for a seriesless card — the chart stays hidden and the card's
    /// empty state is shown instead.
    /// </summary>
    public static CartesianChart? Build(HistogramChartData data)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var observedColor = BrushColor("BrushChartSeries1", new SKColor(0x1B, 0x7A, 0x4C));
        var fittedColor = BrushColor("BrushChartSeries2", new SKColor(0x0B, 0x72, 0x85));

        var observed = new ColumnSeries<double>
        {
            Name = "Observed",
            Values = data.Observed,
            Fill = new SolidColorPaint(observedColor),
            MaxBarWidth = 22,
        };
        var fitted = new LineSeries<double>
        {
            Name = "Fitted distribution",
            Values = data.FittedPdf,
            Stroke = new SolidColorPaint(fittedColor, 2),
            Fill = null,
            GeometrySize = 0,
            LineSmoothness = 0,
        };

        return CreateChart(new ISeries[] { observed, fitted }, data.Categories);
    }

    /// <summary>
    /// Creates the paired observed-vs-expected bar chart for one chi-square
    /// card. The two column series share every bin coordinate, so LiveCharts
    /// draws them grouped side-by-side (never stacked). Returns null for a
    /// seriesless card.
    /// </summary>
    public static CartesianChart? BuildChiSquareChart(ChiSquareChartData data)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var observedColor = BrushColor("BrushChartSeries1", new SKColor(0x1B, 0x7A, 0x4C));
        var expectedColor = BrushColor("BrushChartSeries2", new SKColor(0x0B, 0x72, 0x85));

        var observed = new ColumnSeries<double>
        {
            Name = "Observed",
            Values = data.Observed,
            Fill = new SolidColorPaint(observedColor),
            MaxBarWidth = 22,
        };
        var expected = new ColumnSeries<double>
        {
            Name = "Expected",
            Values = data.Expected,
            Fill = new SolidColorPaint(expectedColor),
            MaxBarWidth = 22,
        };

        return CreateChart(new ISeries[] { observed, expected }, data.Categories);
    }

    /// <summary>
    /// Shared shell for every card's chart: categorical X labels, a frequency
    /// Y axis, and theme-resolved axis/legend/tooltip paints.
    /// </summary>
    private static CartesianChart CreateChart(IReadOnlyList<ISeries> series, IReadOnlyList<string> xCategories)
    {
        var gridColor = BrushColor("BrushChartGrid", new SKColor(0xC9, 0xD1, 0xCC));
        var axisTextColor = BrushColor("BrushChartAxisText", new SKColor(0x44, 0x50, 0x4A));
        var tickColor = BrushColor("BrushChartAxisTicks", new SKColor(0xA4, 0xB3, 0xA9));
        var legendTextColor = BrushColor("BrushChartLegendText", new SKColor(0x44, 0x50, 0x4A));
        var legendBackgroundColor = BrushColor("BrushChartLegendBackground", new SKColor(0xFF, 0xFF, 0xFF));
        var tooltipTextColor = BrushColor("BrushChartTooltipText", new SKColor(0x1A, 0x23, 0x20));
        var tooltipBackgroundColor = BrushColor("BrushChartTooltipBackground", new SKColor(0xFF, 0xFF, 0xFF));

        return new CartesianChart
        {
            Series = new ObservableCollection<ISeries>(series),
            XAxes = new ObservableCollection<Axis>
            {
                new()
                {
                    Labels = xCategories.ToArray(),
                    LabelsPaint = new SolidColorPaint(axisTextColor),
                    TicksPaint = new SolidColorPaint(tickColor),
                },
            },
            YAxes = new ObservableCollection<Axis>
            {
                new()
                {
                    Labeler = value => value.ToString("0.##", CultureInfo.InvariantCulture),
                    LabelsPaint = new SolidColorPaint(axisTextColor),
                    TicksPaint = new SolidColorPaint(tickColor),
                    SeparatorsPaint = new SolidColorPaint(gridColor),
                },
            },
            Height = ChartHeight,
            LegendPosition = LegendPosition.Top,
            LegendTextPaint = new SolidColorPaint(legendTextColor),
            LegendBackgroundPaint = new SolidColorPaint(legendBackgroundColor),
            TooltipTextPaint = new SolidColorPaint(tooltipTextColor),
            TooltipBackgroundPaint = new SolidColorPaint(tooltipBackgroundColor),
        };
    }

    private static SKColor BrushColor(string key, SKColor fallback)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true
            && value is SolidColorBrush brush)
        {
            var c = brush.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }

        return fallback;
    }
}