namespace OpdSimulator.App.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Services;
using Xunit;

/// <summary>
/// Phase 8S issue 5 — the equal-share benchmark against the bars it is meant to span.
/// </summary>
/// <remarks>
/// <para>
/// Two things have to hold for the dashed line to read as a benchmark rather than as
/// decoration: it must sit at the stage's equal share, and it must run across that
/// stage's bars rather than across the whole plot.
/// </para>
/// <para>
/// The second one is a property of how the chart is built rather than of any number
/// in the data. There is one <c>ColumnSeries</c> per stage, each carrying a value only
/// at its own stage's slots. LiveCharts gives each bar a share of the category band
/// per contributing series at that category, so the invariant that keeps a bar centred
/// — and therefore keeps a line at the same index centred on it — is that exactly ONE
/// column series claims each category. If two ever claimed the same slot the band
/// would split, the bar would slide off centre, and the line would no longer cross it.
/// That invariant is asserted below rather than left to the library's internals.
/// </para>
/// <para>
/// Pixel-level extraction of the realised bar centres was attempted and abandoned: the
/// headless capture (HeadlessScreenshot) does not contain LiveCharts' Skia layer, so
/// there are no bar pixels to measure. The visual confirmation of this change is the
/// phase-8s-utilisation-benchmark.png frame; what is asserted here is the structure
/// that decides the position. Recorded rather than papered over (D-166, D-197).
/// </para>
/// </remarks>
public sealed class Phase8SBenchmarkTests
{
    /// <summary>
    /// One horizontal segment per stage, flat at the stage's equal share, with the
    /// markers off. A benchmark with a dot on every server reads as a data series.
    /// </summary>
    [Fact]
    public void UtilisationChart_BenchmarkLine_SpansStageBars_AtEqualShare()
    {
        var data = UtilisationChartService.Build(Phase8MFixtures.ResultWithStages(
            ("Triage", 2, 0.45, new[] { 0.45, 0.45 }),
            ("Lab", 4, 0.60, new[] { 0.85, 0.60, 0.60, 0.35 }),
            ("Consultation", 5, 0.40, new[] { 0.40, 0.40, 0.40, 0.40, 0.40 })));
        var chart = ChartControlBuilder.BuildUtilisationChart(data)!;

        var lines = chart.Series.OfType<LineSeries<double?>>().ToList();
        Assert.Equal(data.ReferenceLines.Count, lines.Count);

        foreach (var reference in data.ReferenceLines)
        {
            var stageBars = data.Bars
                .Skip(reference.FirstBarIndex)
                .Take(reference.LastBarIndex - reference.FirstBarIndex + 1)
                .ToList();
            Assert.NotEmpty(stageBars);

            // The height is the stage mean rescaled by the server count — the share
            // each server would contribute if the stage were perfectly even. It is
            // what makes "this server is above/below the benchmark" meaningful.
            var stageMean = stageBars.Average(b => b.Utilisation);
            Assert.Equal(stageMean / stageBars[0].ServerCount, reference.EqualShare, 12);

            var line = lines.Single(l =>
                (string?)l.Name == $"{stageBars[0].StageName} — equal share");
            var lineValues = LineValuesFor(line);

            // Flat across exactly this stage's slots, and absent everywhere else: a
            // line that ran the full category list would be one line for three stages.
            for (var i = 0; i < data.Bars.Count; i++)
            {
                var value = lineValues[i];
                var insideStage = i >= reference.FirstBarIndex && i <= reference.LastBarIndex;

                if (insideStage)
                {
                    Assert.Equal(reference.EqualShare, value);
                }
                else
                {
                    Assert.Null(value);
                }
            }

            // Markers off, and no smoothing: a curve between two equal values would
            // bow away from the very height it is asserting.
            Assert.Equal(0d, line.GeometrySize);
            Assert.Equal(0d, line.LineSmoothness);
        }
    }

    /// <summary>
    /// Each bar's category is claimed by exactly one column series, and the
    /// benchmark for that stage covers exactly those categories — so the line is
    /// drawn at the same band centre as the bars it belongs to.
    /// </summary>
    [Fact]
    public void UtilisationChart_BenchmarkLine_AlignedWithBarCentres()
    {
        var data = UtilisationChartService.Build(Phase8MFixtures.ResultWithStages(
            ("Triage", 2, 0.45, new[] { 0.45, 0.45 }),
            ("Lab", 4, 0.60, new[] { 0.85, 0.60, 0.60, 0.35 }),
            ("Consultation", 5, 0.40, new[] { 0.40, 0.40, 0.40, 0.40, 0.40 })));
        var chart = ChartControlBuilder.BuildUtilisationChart(data)!;

        var columns = chart.Series.OfType<ColumnSeries<double?>>().ToList();
        Assert.Equal(3, columns.Count);

        // One column series per stage, each with a name the legend and tooltip share.
        var stageNames = data.Bars.Select(b => b.StageName).Distinct().ToList();
        Assert.Equal(stageNames, columns.Select(c => (string?)c.Name));

        // THE invariant: no category is claimed by two series. A split band would put
        // the bar off the category centre and take the benchmark line with it.
        for (var i = 0; i < data.Bars.Count; i++)
        {
            var claimants = columns.Count(c => SeriesValues(c)[i].HasValue);
            Assert.True(
                claimants == 1,
                $"category {i} ('{data.Bars[i].StageName} S{data.Bars[i].ServerNumber}') is claimed by " +
                $"{claimants} column series — the band would split and the benchmark would miss the bar centre");
        }

        // And each stage's benchmark covers precisely that stage's bar categories.
        foreach (var reference in data.ReferenceLines)
        {
            var stageName = data.Bars[reference.FirstBarIndex].StageName;
            var barSlots = data.Bars
                .Select((b, i) => (b, i))
                .Where(t => t.b.StageName == stageName)
                .Select(t => t.i)
                .ToList();

            var line = chart.Series.OfType<LineSeries<double?>>()
                .Single(l => (string?)l.Name == $"{stageName} — equal share");

            var lineSlots = Enumerable.Range(0, data.Bars.Count)
                .Where(i => LineValuesFor(line)[i].HasValue)
                .ToList();

            Assert.Equal(barSlots, lineSlots);
        }
    }

    /// <summary>A series' values as a list, so they can be indexed by category.</summary>
    private static List<double?> SeriesValues(ColumnSeries<double?> series)
    {
        Assert.NotNull(series.Values);
        return series.Values.ToList();
    }

    /// <summary>Same, for a benchmark line.</summary>
    private static List<double?> LineValuesFor(LineSeries<double?> series)
    {
        Assert.NotNull(series.Values);
        return series.Values.ToList();
    }
}
