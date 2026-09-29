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
    /// Creates the per-server utilisation chart (FR-STAT-7, Phase 6c.4, revised
    /// by Phase 8M / D-160). Bar height is each server's CONTRIBUTION to its
    /// stage's utilisation — busy time divided by server count times operating
    /// time — so a stage's bars sum to that stage's utilisation. One
    /// <see cref="ColumnSeries{TModel}"/> per stage carries that stage's bars,
    /// with a null at every other category; deviating servers are marked by an
    /// amber <see cref="ScatterSeries{TModel}"/> on the same slot so the marker
    /// paints on top of its bar; a dashed <see cref="LineSeries{TModel}"/> per
    /// stage marks the equal-share benchmark. Stage colours come from
    /// <see cref="StageColourPalette"/> (D-163) and the legend is hidden because
    /// the X labels, per-server tooltips, dynamic legend and caption carry the
    /// meaning instead.
    /// </summary>
    public static CartesianChart? BuildUtilisationChart(UtilisationChartData data)
    {
        if (!data.HasSeries)
        {
            return null;
        }

        var lineColor = BrushColor("BrushChartAxisText", new SKColor(0x44, 0x50, 0x4A));
        var amberPaint = new SolidColorPaint(ToSkia(StageColourPalette.ImbalanceHighlight), 1);

        var series = new List<ISeries>();
        var xCategories = new List<string>();
        var count = data.Bars.Count;

        // Group the flat bar list by stage, keeping stage order. The palette
        // keys off the stage's position, so a stage is the same colour here as
        // in the queue and wait charts (D-163).
        var stageBars = new List<(int StageIndex, string StageName, List<UtilisationBarRow> Bars)>();
        foreach (var bar in data.Bars)
        {
            if (stageBars.Count == 0 || stageBars[^1].StageIndex != bar.StageIndex)
            {
                stageBars.Add((bar.StageIndex, bar.StageName, new List<UtilisationBarRow>()));
            }

            stageBars[^1].Bars.Add(bar);
        }

        foreach (var stage in stageBars)
        {
            var stageColor = ToSkia(StageColourPalette.ForStageIndex(stage.StageIndex));
            var values = new double?[count];
            var outliers = new double?[count];

            // One ColumnSeries per STAGE, not per server (Phase 8M, D-160): a
            // per-server series each had to fill the whole category list with
            // nulls, and a null-heavy category is what thinned the X axis out.
            int slot = stageBars.TakeWhile(s => s.StageIndex != stage.StageIndex).Sum(s => s.Bars.Count);
            foreach (var bar in stage.Bars)
            {
                values[slot] = bar.Contribution;
                if (bar.IsOutlier)
                {
                    // The amber pass rides on the same slot as a marker series
                    // rather than a second column series, so it paints ON TOP of
                    // the stage column instead of being grouped beside it.
                    outliers[slot] = bar.Contribution;
                }

                xCategories.Add($"{stage.StageName} S{bar.ServerNumber}");
                slot++;
            }

            series.Add(new ColumnSeries<double?>
            {
                Name = stage.StageName,
                Values = values,
                Fill = new SolidColorPaint(stageColor),
                MaxBarWidth = 28,
                // The chart draws contributions, so the tooltip has to give the
                // underlying numbers back: a reader hovering a bar wants to know
                // both its share and the server utilisation it came from.
                YToolTipLabelFormatter = point => TooltipFor(BarAt(data, point.Index)),
            });

            series.Add(new ScatterSeries<double?>
            {
                Name = $"{stage.StageName} — deviating server",
                Values = outliers,
                GeometrySize = 22,
                Fill = amberPaint,
                Stroke = amberPaint,
                YToolTipLabelFormatter = point => OutlierTooltipFor(BarAt(data, point.Index)),
            });
        }

        foreach (var reference in data.ReferenceLines)
        {
            var values = new double?[count];
            for (int i = reference.FirstBarIndex; i <= reference.LastBarIndex; i++)
            {
                values[i] = reference.EqualShare;
            }

            series.Add(new LineSeries<double?>
            {
                Name = $"{data.Bars[reference.FirstBarIndex].StageName} — equal share",
                Values = values,
                Stroke = new SolidColorPaint(lineColor, 1),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
            });
        }

        // Fixed y-axis: a contribution is a share of ONE server's capacity, so
        // the largest contribution any bar can ever have is 1/c for the smallest
        // server count in the run. Fixing the top there — rather than at the
        // tallest bar this run happened to produce — is what makes two runs with
        // different imbalances comparable at a glance, and it can never clip a
        // deviating server: a bar reaches that ceiling only if a server in the
        // smallest stage ran at 100% while the rest of the run was quiet.
        var fewestServers = data.Bars.Min(b => b.ServerCount);
        var axisMax = 1d / Math.Max(1, fewestServers);
        var yAxis = new Axis
        {
            MinLimit = 0,
            MaxLimit = axisMax,
            Labeler = value => value.ToString("P0", CultureInfo.InvariantCulture),
        };

        return CreateChart(
            series,
            xCategories,
            xLabelsRotation: 30,
            yAxisOverride: yAxis,
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

        // Draw order: the busiest-average stage first so the quietest-average
        // stage lands on top. Without this the busiest stage's spikes cross the
        // quieter stages' lines wherever they overlap, and the reader cannot
        // tell which line they are following (Phase 8M, D-162).
        var ordered = data.Series
            .OrderByDescending(s => s.Points.Count == 0 ? 0 : s.Points.Average(p => p.Length))
            .ToList();

        var series = new List<ISeries>();
        foreach (var stage in ordered)
        {
            // StepLineSeries, not a smoothed line: the engine only changes the
            // queue length at an event, so the value is CONSTANT between
            // events. A straight segment would draw an average that never
            // existed, and smoothing would invent the same fiction more
            // prettily. Numeric X pairs time/length directly.
            series.Add(new StepLineSeries<ObservablePoint>
            {
                Name = stage.StageName,
                Values = stage.Points
                    .Select(p => new ObservablePoint(p.Time, p.Length))
                    .ToList(),
                Stroke = new SolidColorPaint(
                    SeriesPaletteColor(stage.StageIndex), 2),
                Fill = null,
                GeometrySize = 0,
            });
        }

        // +1 headroom: the queue length steps to whole numbers, so an axis that
        // stops exactly at the maximum would clip the tallest step's cap.
        var yMax = data.Series
            .SelectMany(s => s.Points)
            .Select(p => p.Length)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return CreateChart(
            series,
            xLabeler: value => value.ToString("0.##", CultureInfo.InvariantCulture),
            yAxisOverride: new Axis { MinLimit = 0, MaxLimit = yMax });
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
                Fill = new SolidColorPaint(SeriesPaletteColor(data.StageIndex)),
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
        double? xLabelsRotation = null,
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
                    // 30 degrees on a dense categorical axis: 11 "Stage S3"
                    // labels collide vertically at 0 degrees and get dropped.
                    LabelsRotation = xLabelsRotation ?? 0,
                    // N1 (Phase 8M): only install a numeric labeler when the
                    // axis has no string categories. A non-null Labeler takes
                    // precedence over Labels in LiveCharts2, so the numeric
                    // default below was computing "{Stage} S{n}" and then
                    // throwing it away, printing bare indices instead.
                    Labeler = xLabeler ?? CategoricalXLabeler(xCategories),
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
    /// <summary>
    /// The X labeler for a categorical axis (N1, Phase 8M).
    /// </summary>
    /// <remarks>
    /// LiveCharts2 ships a NON-NULL default <c>Labeler</c> and a non-null labeler
    /// wins over the axis's string <c>Labels</c> — which is exactly how
    /// "{Stage} S{n}" was being computed, attached as <c>Labels</c>, and then
    /// replaced on screen by a bare index. The fix cannot be to assign null
    /// (that fights the non-nullable annotation, and the next reader would be
    /// tempted to "fix" it back). So the labeler is kept and taught to return
    /// the category text itself: the labels now genuinely render, and the
    /// behaviour is unit-testable without a chart.
    /// </remarks>
    private static Func<double, string> CategoricalXLabeler(IReadOnlyList<string>? xCategories) =>
        value => LabelAt(xCategories, value);

    /// <summary>
    /// The category text at a numeric axis position, falling back to the plain
    /// number when the axis is not categorical or the position is out of range.
    /// Pure, so the label behaviour is testable headlessly.
    /// </summary>
    internal static string LabelAt(IReadOnlyList<string>? xCategories, double value)
    {
        if (xCategories is null || xCategories.Count == 0)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        int index = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return index >= 0 && index < xCategories.Count
            ? xCategories[index]
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The hover text for one utilisation bar. States the contribution (the bar's
    /// height) AND the server utilisation behind it, because the 1/c rescale
    /// means the bar height is deliberately not the server's utilisation.
    /// </summary>
    /// <summary>
    /// Resolves the bar behind a LiveCharts point index.
    /// </summary>
    /// <remarks>
    /// Every series in this chart carries a value array spanning ALL categories,
    /// with nulls wherever the series has nothing to draw, so a point index is a
    /// position in the run-wide category list — not a position inside the
    /// reporting stage's own bars. Resolving through <see cref="UtilisationChartData.Bars"/>
    /// is what keeps the third stage of a 1/2/3 run (whose local list holds only
    /// three entries) from being asked for element five of three. Kept as a named
    /// method rather than an inline index so the mapping is directly testable.
    /// </remarks>
    /// <param name="data">The chart data holding the run-wide bar list.</param>
    /// <param name="globalIndex">The point index reported by LiveCharts.</param>
    /// <returns>The bar occupying that category.</returns>
    public static UtilisationBarRow BarAt(UtilisationChartData data, int globalIndex) =>
        data.Bars[globalIndex];

    private static string TooltipFor(UtilisationBarRow bar) =>
        $"{bar.StageName} · server {bar.ServerNumber}\n" +
        $"Contribution {bar.Contribution.ToString("P1", CultureInfo.InvariantCulture)} " +
        $"(server utilisation {bar.Utilisation.ToString("P1", CultureInfo.InvariantCulture)})";

    /// <summary>
    /// The hover text for an amber bar, naming the direction and size of the
    /// deviation so the colour is not the only signal (§16.9: red/amber is never
    /// the sole cue).
    /// </summary>
    private static string OutlierTooltipFor(UtilisationBarRow bar) =>
        $"{bar.StageName} · server {bar.ServerNumber} deviates\n" +
        $"{(bar.DeltaFromAverage > 0 ? "Above" : "Below")} its stage mean by " +
        $"{Math.Abs(bar.DeltaFromAverage).ToString("P1", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Converts an Avalonia palette <see cref="Color"/> to the Skia colour
    /// LiveCharts paints with. The palette is asked for colour by STAGE INDEX
    /// (D-163), so every chart shares one stage identity.
    /// </summary>
    private static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);

    private static SKColor SeriesPaletteColor(int index) => ToSkia(StageColourPalette.ForStageIndex(index));

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