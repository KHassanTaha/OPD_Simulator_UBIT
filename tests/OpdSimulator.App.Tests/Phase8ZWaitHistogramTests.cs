using Avalonia.Headless.XUnit;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Engine;
using Xunit;
using Axis = LiveChartsCore.SkiaSharpView.Axis;
using LogarithmicAxis = LiveChartsCore.SkiaSharpView.LogarithmicAxis;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Z — the waiting-time histogram answers the owner's four complaints:
/// a labelled zero spike, a round bin width an outlier cannot flatten, named
/// axes with integer patient ticks, and an honest caption for censored waits
/// (D-224). Service-level tests are plain Facts; the axis names/ticks need a
/// rendered chart, so those are AvaloniaFacts (D-121 test lesson).
/// </summary>
public class Phase8ZWaitHistogramTests
{
    [Fact]
    public void ZeroWaits_GetTheirOwnBar()
    {
        // 41 patients walked straight through, 3 waited: the spike must be a
        // bar labelled "0", not folded into the first positive bin.
        var data = WaitHistogramService.BuildForSamples(
            "Reception",
            Enumerable.Repeat(0d, 41).Concat(new[] { 0.6, 1.2, 1.8 }).ToArray());

        Assert.True(data.HasSeries);
        Assert.Equal("0", data.Categories[0]);
        Assert.Equal(41, data.Counts[0]);
        Assert.Equal(41, data.ZeroWaitCount);
        // The zero bar does not change the width-derived label of the first bin.
        Assert.Equal(">0.5", data.Categories[1]);
    }

    [Fact]
    public void BinWidth_ComesFromTheReadableLadder()
    {
        // One 5000-minute outlier must not set the width. With 21 waits the 95th
        // percentile is the largest small value (~1 min), so the ladder picks
        // 0.5 — a round number — instead of the data-dependent (5000/10).
        var waits = Enumerable.Range(1, 20).Select(i => i / 20d).Append(5000d).ToArray();
        var data = WaitHistogramService.BuildForSamples("Reception", waits);

        Assert.Equal(0.5, data.BinWidthMinutes);
        Assert.NotNull(data.OverflowFromMinutes);
        Assert.Equal(10 * 0.5, data.OverflowFromMinutes);
    }

    [Fact]
    public void Overflow_CollectsTheTailAndNothingIsLost()
    {
        var waits = Enumerable.Repeat(0.2, 30).Concat(new[] { 700d, 900d }).ToArray();
        var data = WaitHistogramService.BuildForSamples("Reception", waits);

        Assert.Equal(waits.Length, data.Counts.Sum());
        Assert.StartsWith(">", data.Categories[^1]);
        Assert.Equal(2, data.Counts[^1]);
        Assert.True(data.Categories.Count <= 12, "the bar budget holds");
    }

    [Fact]
    public void AllZero_SingleZeroBar()
    {
        var data = WaitHistogramService.BuildForSamples("Reception", new[] { 0d, 0d, 0d, 0d });

        Assert.True(data.HasSeries);
        Assert.Equal(new[] { "0" }, data.Categories);
        Assert.Equal(new double[] { 4 }, data.Counts);
        Assert.Null(data.BinWidthMinutes);
        Assert.Null(data.OverflowFromMinutes);
    }

    [Fact]
    public void NoSamples_Seriesless()
    {
        var data = WaitHistogramService.BuildForSamples("Reception", Array.Empty<double>());

        Assert.False(data.HasSeries);
        Assert.Empty(data.Categories);
        Assert.Empty(data.Counts);
        Assert.Equal(string.Empty, data.Caption);
    }

    [Fact]
    public void Labels_AreShortRanges_NoBrackets()
    {
        var data = WaitHistogramService.BuildForSamples(
            "Reception", new[] { 1.1, 2.2, 7.7, 11.5, 30.1 });

        Assert.All(data.Categories, label =>
        {
            Assert.DoesNotContain("[", label);
            Assert.DoesNotContain(")", label);
            Assert.DoesNotContain(",", label);
        });
        Assert.Contains(data.Categories, label => label.Contains("–"));
    }

    [Fact]
    public void UnfinishedSamples_AreIncludedButCountedSeparately()
    {
        // The censored tail is shaped into the bars, but its count is disclosed
        // so a reader never mistakes "still waiting" for a completed wait (D-224).
        var data = WaitHistogramService.BuildForSamples(
            "Reception",
            new[] { 0d, 1.2, 2.4 },
            new[] { 5.0, 9.0 });

        Assert.Equal(3, data.CompletedCount);
        Assert.Equal(2, data.UnfinishedCount);
        Assert.Equal(5, data.Counts.Sum());
        Assert.Contains("2 still waiting when the run ended", data.Caption);
        Assert.Contains("5 waits (3 completed + 2 still waiting", data.Caption);
    }

    [Fact]
    public void Percentages_SumTo100()
    {
        var data = WaitHistogramService.BuildForSamples(
            "Reception", new[] { 0d, 0d, 0.7, 3.2, 80d });

        Assert.Equal(data.Counts.Count, data.Percentages.Count);
        Assert.Equal(100d, data.Percentages.Sum(), precision: 6);
        Assert.All(data.Percentages, p => Assert.InRange(p, 0d, 100d));
    }

    [Fact]
    public void Caption_ReportsZeroMassAndUnfinished()
    {
        var data = WaitHistogramService.BuildForSamples(
            "Reception",
            new[] { 0d, 0d, 0d, 4.25 },
            new[] { 900d });

        Assert.Contains("5 waits", data.Caption);
        Assert.Contains("3 had no wait", data.Caption);
        Assert.Contains("longest 900 min", data.Caption);
        Assert.Contains("bins", data.Caption);
        Assert.Contains("last bar > 600 min", data.Caption);
        Assert.Contains("1 still waiting when the run ended", data.Caption);
    }

    [Fact]
    public void Builder_TooltipShowsCountAndShare()
    {
        var data = WaitHistogramService.BuildForSamples(
            "Reception", new[] { 0d, 0d, 1.5 });

        string zero = WaitHistogramService.BarTooltip(data, 0);
        Assert.Contains("0 min (no wait)", zero);
        Assert.Contains("2 patients", zero);
        Assert.Contains("66.7%", zero);

        string positive = WaitHistogramService.BarTooltip(data, 3);
        Assert.Contains("min", positive);
        Assert.Contains("1 patients", positive);
        Assert.Contains("33.3%", positive);
    }

    [AvaloniaFact]
    public void Builder_NamesBothAxes()
    {
        var data = WaitHistogramService.BuildForSamples("Reception", new[] { 0d, 1d, 2d });
        var chart = ChartControlBuilder.BuildWaitHistogramChart(data, logScale: false)!;

        Assert.Equal("Waiting time (minutes)", chart.XAxes.Single().Name);
        Assert.Equal("Patients per bin", chart.YAxes.Single().Name);
    }

    [AvaloniaFact]
    public void Builder_IntegerYLabels()
    {
        var data = WaitHistogramService.BuildForSamples("Reception", new[] { 0d, 1d, 2d });
        var chart = ChartControlBuilder.BuildWaitHistogramChart(data, logScale: false)!;
        var y = chart.YAxes.Single();

        Assert.Equal(0d, y.MinLimit);
        Assert.Equal(1d, y.MinStep);
        Assert.NotNull(y.Labeler);
        Assert.Equal("3", y.Labeler!(2.7));
        Assert.DoesNotContain(".", y.Labeler!(4.0));
    }

    [AvaloniaFact]
    public void Builder_LogAxisIsNamed()
    {
        var data = WaitHistogramService.BuildForSamples("Reception", new[] { 0d, 1d, 500d });
        var chart = ChartControlBuilder.BuildWaitHistogramChart(data, logScale: true)!;
        var y = Assert.IsType<LogarithmicAxis>(chart.YAxes.Single());

        Assert.Equal("Patients per bin (log scale)", y.Name);
        Assert.Equal(1d, y.MinLimit);
        Assert.NotNull(y.Labeler);
        Assert.DoesNotContain(".", y.Labeler!(10.0));
        var values = chart.Series.OfType<ColumnSeries<double?>>().Single().Values!;
        Assert.Contains(values, v => v is null);
    }

    [Fact]
    public void Caption_FollowsStageAndLogToggle()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Reception",
                    WaitingTimeSamples = new[] { new WaitSample(1, 0), new WaitSample(2, 1.5) },
                },
                new StageMetrics
                {
                    StageName = "Doctor",
                    WaitingTimeSamples = new[] { new WaitSample(1, 0), new WaitSample(2, 9d) },
                    UnfinishedWaitSamples = new[] { new WaitSample(3, 12d) },
                },
            },
        };
        var vm = new ResultsPanelViewModel();
        vm.StartRun();
        vm.CompleteRun(new RunOutcome(result, Array.Empty<FitReport>(), Array.Empty<string>(),
            SimulationCoordinator.DefaultExitProbability, 0.0, null));

        Assert.Contains("2 waits", vm.WaitHistogramCaption);
        Assert.DoesNotContain("still waiting", vm.WaitHistogramCaption);

        vm.SelectedWaitStage = "Doctor";
        Assert.Contains("1 still waiting when the run ended", vm.WaitHistogramCaption);

        vm.IsWaitLogScale = true;
        Assert.Contains(WaitHistogramService.LogScaleNote, vm.WaitHistogramCaption);

        vm.Reset();
        Assert.Equal(WaitHistogramService.Caption, vm.WaitHistogramCaption);
    }
}
