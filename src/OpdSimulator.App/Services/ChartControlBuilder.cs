namespace OpdSimulator.App.Services;

using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

/// <summary>
/// Builds the <see cref="CartesianChart"/> controls for the chart cards — the
/// Input Analysis histograms and chi-square pairs (Phase 6C) and the Results
/// per-server utilisation chart (Phase 6c.4). Lives in Services, not the view
/// model, because it owns the LiveCharts — UI — types; view models hold the
/// built controls. Called on the UI thread (G5): LiveCharts controls are
/// Avalonia controls and cannot be created on the background thread.
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
    /// Creates the per-server utilisation chart (FR-STAT-7, Phase 6c.4). One
    /// column per server, green by default and amber when the server deviates
    /// from its stage mean by more than
    /// <see cref="UtilisationChartService.ImbalanceThreshold"/>; a thin
    /// reference line per stage at the stage mean. The legend is hidden because
    /// there is one series per server plus one per stage — the X labels,
    /// per-server tooltips and caption carry the meaning instead.
    /// </summary>
    public static CartesianChart? BuildUtilisationChart(UtilisationChartData data)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var greenColor = BrushColor("BrushChartSeries1", new SKColor(0x1B, 0x7A, 0x4C));
        var amberColor = BrushColor("BrushWarning", new SKColor(0x8A, 0x53, 0x00));
        var lineColor = BrushColor("BrushChartAxisText", new SKColor(0x44, 0x50, 0x4A));

        var series = new List<ISeries>();
        var xCategories = new List<string>();
        var count = data.Bars.Count;

        int barIndex = 0;
        foreach (var bar in data.Bars)
        {
            // A single non-null value at the server's categorical index; nulls
            // elsewhere leave gaps so the bar lands on the right X label.
            var values = new double?[count];
            values[barIndex] = bar.Utilisation;

            var tooltip = bar.IsOutlier
                ? bar.DeltaFromAverage > 0
                    ? $"Utilisation: {bar.Utilisation.ToString("P1", CultureInfo.InvariantCulture)}\n" +
                      $"Above average by {bar.DeltaFromAverage.ToString("0.0%", CultureInfo.InvariantCulture)}"
                    : $"Utilisation: {bar.Utilisation.ToString("P1", CultureInfo.InvariantCulture)}\n" +
                      $"Below average by {Math.Abs(bar.DeltaFromAverage).ToString("0.0%", CultureInfo.InvariantCulture)}"
                : $"Utilisation: {bar.Utilisation.ToString("P1", CultureInfo.InvariantCulture)}";

            series.Add(new ColumnSeries<double?>
            {
                Name = $"{bar.StageName} · Server {bar.ServerNumber}",
                Values = values,
                Fill = new SolidColorPaint(bar.IsOutlier ? amberColor : greenColor),
                MaxBarWidth = 28,
                YToolTipLabelFormatter = _ => tooltip,
            });

            xCategories.Add($"{bar.StageName} S{bar.ServerNumber}");
            barIndex++;
        }

        foreach (var reference in data.ReferenceLines)
        {
            var values = new double?[count];
            for (int i = reference.FirstBarIndex; i <= reference.LastBarIndex; i++)
            {
                values[i] = reference.Average;
            }

            series.Add(new LineSeries<double?>
            {
                Name = "Stage average",
                Values = values,
                Stroke = new SolidColorPaint(lineColor, 1),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
            });
        }

        return CreateChart(
            series,
            xCategories,
            yLabeler: value => value.ToString("P0", CultureInfo.InvariantCulture),
            showLegend: false);
    }

    /// <summary>
    /// Creates the queue-length-over-time chart (FR-UI-4 P2, Phase 6c.5): one
    /// <see cref="LineSeries{TModel}"/> per stage in the four-series chart
    /// palette, X = simulated minutes (numeric axis, not categories), legend =
    /// stage names. Points come pre-decimated by
    /// <see cref="QueueLengthChartService"/>. Returns null for a seriesless card.
    /// </summary>
    public static CartesianChart? BuildQueueChart(QueueLengthChartData data)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var series = new List<ISeries>();
        foreach (var stage in data.Series)
        {
            // ObservablePoint pairs time/length directly; numeric X means the
            // axis scales to the sampled interval instead of faking categories.
            series.Add(new LineSeries<ObservablePoint>
            {
                Name = stage.StageName,
                Values = stage.Points
                    .Select(p => new ObservablePoint(p.Time, p.Length))
                    .ToList(),
                Stroke = new SolidColorPaint(SeriesPaletteColor(series.Count), 2),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
            });
        }

        return CreateChart(
            series,
            xLabeler: value => value.ToString("0.##", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Creates the waiting-time histogram (FR-UI-4 P2, Phase 6c.5): one column
    /// series for the selected stage, X = equal-width waiting-time bins, Y =
    /// patient count. With <paramref name="logScale"/> the Y axis becomes a
    /// base-10 <see cref="LogarithmicAxis"/> — for long tails; bins with zero
    /// patients are dropped then (log 0 is undefined and empty bins carry no
    /// visual mass on a log axis anyway). Returns null for a seriesless card.
    /// </summary>
    public static CartesianChart? BuildWaitHistogramChart(WaitHistogramData data, bool logScale)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var values = data.Counts
            .Select(count => logScale && count == 0 ? (double?)null : count)
            .ToList();

        var series = new ISeries[]
        {
            new ColumnSeries<double?>
            {
                Name = $"Waiting time — {data.StageName}",
                Values = values,
                Fill = new SolidColorPaint(SeriesPaletteColor(0)),
                MaxBarWidth = 26,
            },
        };

        return CreateChart(
            series,
            data.Categories,
            yAxisOverride: logScale ? new LogarithmicAxis(10) : null);
    }

    /// <summary>
    /// Shared shell for every card's chart: categorical X labels by default (a
    /// numeric-X time series passes an <paramref name="xLabeler"/> instead and
    /// leaves the categories null), a Y axis (frequency by default, percent for
    /// utilisation, base-10 logarithmic for the wait histogram's long tails),
    /// and theme-resolved axis/legend/tooltip paints. When
    /// <paramref name="yAxisOverride"/> is supplied its missing paint slots are
    /// filled from the same theme tokens, so styling stays in one place.
    /// </summary>
    private static CartesianChart CreateChart(
        IReadOnlyList<ISeries> series,
        IReadOnlyList<string>? xCategories = null,
        Func<double, string>? xLabeler = null,
        Func<double, string>? yLabeler = null,
        bool showLegend = true,
        Axis? yAxisOverride = null)
    {
        var gridColor = BrushColor("BrushChartGrid", new SKColor(0xC9, 0xD1, 0xCC));
        var axisTextColor = BrushColor("BrushChartAxisText", new SKColor(0x44, 0x50, 0x4A));
        var tickColor = BrushColor("BrushChartAxisTicks", new SKColor(0xA4, 0xB3, 0xA9));
        var legendTextColor = BrushColor("BrushChartLegendText", new SKColor(0x44, 0x50, 0x4A));
        var legendBackgroundColor = BrushColor("BrushChartLegendBackground", new SKColor(0xFF, 0xFF, 0xFF));
        var tooltipTextColor = BrushColor("BrushChartTooltipText", new SKColor(0x1A, 0x23, 0x20));
        var tooltipBackgroundColor = BrushColor("BrushChartTooltipBackground", new SKColor(0xFF, 0xFF, 0xFF));

        var yAxis = yAxisOverride ?? new Axis();
        yAxis.Labeler ??= yLabeler ?? (value => value.ToString("0.##", CultureInfo.InvariantCulture));
        yAxis.LabelsPaint ??= new SolidColorPaint(axisTextColor);
        yAxis.TicksPaint ??= new SolidColorPaint(tickColor);
        yAxis.SeparatorsPaint ??= new SolidColorPaint(gridColor);

        return new CartesianChart
        {
            Series = new ObservableCollection<ISeries>(series),
            XAxes = new ObservableCollection<Axis>
            {
                new()
                {
                    Labels = xCategories?.ToArray(),
                    Labeler = xLabeler ?? (value => value.ToString("0.##", CultureInfo.InvariantCulture)),
                    LabelsPaint = new SolidColorPaint(axisTextColor),
                    TicksPaint = new SolidColorPaint(tickColor),
                },
            },
            YAxes = new ObservableCollection<Axis> { yAxis },
            Height = ChartHeight,
            LegendPosition = showLegend ? LegendPosition.Top : LegendPosition.Hidden,
            LegendTextPaint = new SolidColorPaint(legendTextColor),
            LegendBackgroundPaint = new SolidColorPaint(legendBackgroundColor),
            TooltipTextPaint = new SolidColorPaint(tooltipTextColor),
            TooltipBackgroundPaint = new SolidColorPaint(tooltipBackgroundColor),
        };
    }

    /// <summary>
    /// The four-series palette (green, teal, violet, vermillion), cycled when a
    /// network has more than four stages. Theme brush name + matching hex
    /// fallback for headless tests (see <see cref="BrushColor"/>).
    /// </summary>
    private static (string Key, byte R, byte G, byte B)[] SeriesPalette { get; } =
    {
        ("BrushChartSeries1", 0x1B, 0x7A, 0x4C),
        ("BrushChartSeries2", 0x0B, 0x72, 0x85),
        ("BrushChartSeries3", 0x6B, 0x2F, 0xBA),
        ("BrushChartSeries4", 0xE5, 0x46, 0x00),
    };

    private static SKColor SeriesPaletteColor(int index)
    {
        var entry = SeriesPalette[index % SeriesPalette.Length];
        return BrushColor(entry.Key, new SKColor(entry.R, entry.G, entry.B));
    }

    private static SKColor BrushColor(string key, SKColor fallback)
    {
        try
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true
                && value is SolidColorBrush brush)
            {
                var c = brush.Color;
                return new SKColor(c.R, c.G, c.B, c.A);
            }
        }
        catch (Exception ex)
        {
            // The resource lookup can throw when a theme dictionary builds
            // lazily in a context where its StaticResource targets are not yet
            // resolvable (headless unit tests). The fallback hex matches the
            // theme color, so degraded contexts degrade to the same pixels.
            Serilog.Log.Warning(ex, "Chart brush {Key} unresolvable; using theme fallback", key);
        }

        return fallback;
    }
}