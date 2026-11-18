using Avalonia.Headless.XUnit;
using LiveChartsCore;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Y — the operating-time horizontal axis and the selected session's
/// drain tail on the queue-length chart (FR-UI-40, D-223, superseding D-203).
/// </summary>
/// <remarks>
/// The gate has two halves a data test cannot cover together: the service must
/// project samples onto a compressed, per-session axis, and the builder must
/// publish that axis to LiveCharts. Both are asserted here — a data record with
/// correct offsets would still render a wrong chart if the builder ignored them.
/// </remarks>
public class Phase8T3QueueSessionTests
{
    private static readonly ClinicSession[] ThreeOpenSessions =
    {
        new(1, 0, DayOfWeek.Saturday),
        new(2, 2, DayOfWeek.Monday),
        new(3, 3, DayOfWeek.Tuesday),
    };

    private static SimulationResult RunWithSessions() => new()
    {
        Sessions = ThreeOpenSessions,
        SessionLengthMinutes = 165,
        SessionStartDay = DayOfWeek.Saturday,
        GeneratorDays = 3,
        StageMetrics = new[]
        {
            new StageMetrics
            {
                StageName = "Reception",
                QueueLengthSeries = new[]
                {
                    new QueueSample(0, 0),
                    new QueueSample(30, 2),
                    new QueueSample(164, 1),
                    new QueueSample(165, 0),           // window end is inclusive
                    new QueueSample(200, 3),           // drain after the window
                    new QueueSample(2 * 1440 + 10, 4), // Monday
                    new QueueSample(3 * 1440, 5),      // Tuesday
                    new QueueSample(3 * 1440 + 20, 6),
                    new QueueSample(3 * 1440 + 165, 7),
                },
            },
        },
    };

    private static SimulationResult RunWithIdleSession() => new()
    {
        Sessions = new[] { ThreeOpenSessions[0], ThreeOpenSessions[1] },
        SessionLengthMinutes = 165,
        SessionStartDay = DayOfWeek.Saturday,
        GeneratorDays = 2,
        StageMetrics = new[]
        {
            new StageMetrics
            {
                StageName = "Reception",
                // Day 2 has no samples at all — it must still keep a slot.
                QueueLengthSeries = new[] { new QueueSample(0, 0), new QueueSample(30, 2) },
            },
        },
    };

    private static RunOutcome Outcome(SimulationResult result) => new(
        result,
        Array.Empty<FitReport>(),
        Array.Empty<string>(),
        0.3,
        0.1,
        null);

    // --------------------------------------------------------------- selector

    [Fact]
    public void SessionOptions_ListAllSessionsThenOpenDaysOnly()
    {
        var options = QueueLengthChartService.SessionOptions(RunWithSessions());

        Assert.Equal(
            new[] { "All sessions", "Day 1 (Sat)", "Day 2 (Mon)", "Day 3 (Tue)" },
            options);
        Assert.DoesNotContain(options, option => option.Contains("Sun", StringComparison.Ordinal));
    }

    [Fact]
    public void Selector_DefaultsToAllSessionsAndResetsOnANewRun()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(Outcome(RunWithSessions()));

        Assert.True(results.ShowQueueSessionSelector);
        Assert.Equal(QueueLengthChartService.AllSessionsLabel, results.SelectedQueueSession);

        results.SelectedQueueSession = "Day 3 (Tue)";
        Assert.Equal("Day 3 (Tue)", results.SelectedQueueSession);

        // A new run's session ordinals belong to that run, so the selection must
        // not survive it even when the new run has the same shape.
        results.CompleteRun(Outcome(RunWithSessions()));

        Assert.Equal(QueueLengthChartService.AllSessionsLabel, results.SelectedQueueSession);
    }

    [Fact]
    public void Selection_PersistsAcrossAWidgetVisibilityToggle()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(Outcome(RunWithSessions()));
        results.SelectedQueueSession = "Day 2 (Mon)";

        results.ShowQueueLength = false;
        results.ShowQueueLength = true;

        Assert.Equal("Day 2 (Mon)", results.SelectedQueueSession);
    }

    [Fact]
    public void PlainHorizonRun_HasNoSelector()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics { StageName = "Reception", QueueLengthSeries = new[] { new QueueSample(0, 0) } },
            },
        };

        var results = new ResultsPanelViewModel();
        results.CompleteRun(Outcome(result));

        Assert.False(results.ShowQueueSessionSelector);
        Assert.Null(results.QueueSessionNames);
        Assert.Null(results.SelectedQueueSession);
        Assert.Empty(QueueLengthChartService.SessionOptions(result));
    }

    [Fact]
    public void PlainHorizon_Unchanged()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Reception",
                    QueueLengthSeries = new[] { new QueueSample(0, 0), new QueueSample(45, 2) },
                },
            },
        };

        var data = QueueLengthChartService.Build(result);

        Assert.True(data.HasSeries);
        Assert.Null(data.TimeAxis);
        Assert.Null(data.XAxisMaximum);
        Assert.Equal(new[] { 0d, 45d }, data.Series.Single().Points.Select(p => p.Time));
    }

    // ------------------------------------------------------------ compression

    [Fact]
    public void AllSessions_CompressesClosedPeriods()
    {
        var data = QueueLengthChartService.Build(
            RunWithSessions(), QueueLengthChartService.AllSessionsLabel);
        var axis = data.TimeAxis!;

        Assert.True(axis.Compressed);
        var segmentTimes = data.Series.Single().Points
            .Where(p => !p.IsSeam)
            .Select(p => p.Time)
            .ToArray();

        // No adjacent pair spans 1440 (an overnight closure) or 2880 (Friday +
        // Sunday). Day 2 starts one gutter (3) after Day 1's slot ends at 200:
        // its first sample is 203 + 10 = 213, not 2890.
        Assert.Contains(213d, segmentTimes);
        Assert.Contains(371d, segmentTimes); // Day 3 opens at 371 (200+3+165+3)
        Assert.All(segmentTimes.Skip(1).Zip(segmentTimes), pair =>
            Assert.True(pair.Second - pair.First < 300,
                $"an x gap of {pair.Second - pair.First} still spans a closure"));

        Assert.Equal(203d, axis.Segments[1].Start);
    }

    [Fact]
    public void AllSessions_KeepsEverySessionSlot()
    {
        var data = QueueLengthChartService.Build(
            RunWithIdleSession(), QueueLengthChartService.AllSessionsLabel);
        var axis = data.TimeAxis!;

        Assert.Equal(2, axis.Segments.Count);
        Assert.Equal("Day 1 (Sat)", axis.Segments[0].SessionLabel);
        Assert.Equal("Day 2 (Mon)", axis.Segments[1].SessionLabel);
        // The idle day still owns a full 165-minute slot.
        Assert.Equal(165d, axis.Segments[1].End - axis.Segments[1].Start);
    }

    [Fact]
    public void AllSessions_MapsDrainIntoTheProducingSlot()
    {
        var data = QueueLengthChartService.Build(
            RunWithSessions(), QueueLengthChartService.AllSessionsLabel);
        var times = data.Series.Single().Points
            .Where(p => !p.IsSeam)
            .Select(p => p.Time)
            .ToArray();

        // close(165) + 35 drain belongs to Day 1's slot at 200, not to 1440+200.
        Assert.Contains(200d, times);
        Assert.DoesNotContain(times, t => t > 1440 && t < 2880);
        // One NaN seam between each adjacent pair of sessions.
        Assert.Equal(2, data.Series.Single().Points.Count(p => p.IsSeam));
    }

    [Fact]
    public void AllSessions_DayLabels()
    {
        var axis = QueueLengthChartService.Build(
            RunWithSessions(), QueueLengthChartService.AllSessionsLabel).TimeAxis!;

        Assert.Equal("Day 1", axis.FormatTick(0));
        Assert.Equal("Day 1 +50m", axis.FormatTick(50));
        Assert.Equal("Day 2", axis.FormatTick(203));
        Assert.Equal("Day 1 (Sat) · 50 min from open", axis.FormatTooltip(50));
        Assert.Contains("Day 2 (Mon)", axis.FormatTooltip(213));
    }

    [Fact]
    public void Decimation_IsPerSession()
    {
        // A busy first session gets many points; a quiet second session has only
        // two. With a global budget the quiet session's first/last could be lost;
        // per-session bucketing must preserve them.
        var result = new SimulationResult
        {
            Sessions = new[] { ThreeOpenSessions[0], ThreeOpenSessions[1] },
            SessionLengthMinutes = 165,
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Reception",
                    QueueLengthSeries = Enumerable.Range(0, 5000)
                        .Select(i => new QueueSample(i % 160, i % 7))
                        .Concat(new[] { new QueueSample(2 * 1440 + 5, 9), new QueueSample(2 * 1440 + 160, 1) })
                        .ToArray(),
                },
            },
        };

        var data = QueueLengthChartService.Build(result, QueueLengthChartService.AllSessionsLabel);
        var points = data.Series.Single().Points.Where(p => !p.IsSeam).ToArray();

        Assert.True(points.Length <= QueueLengthChartService.MaxPoints);
        // Day 2's two samples survive at its slot start (165 + 3 = 168).
        Assert.Contains(points, p => p.Time == 173d && p.Length == 9);
        Assert.Contains(points, p => p.Time == 328d && p.Length == 1);
    }

    // ---------------------------------------------------------- selected view

    [Fact]
    public void Session_IncludesTheDrainTail()
    {
        var data = QueueLengthChartService.Build(RunWithSessions(), "Day 1 (Sat)");

        Assert.True(data.HasSeries);
        Assert.Equal(200d, data.XAxisMaximum);
        Assert.Equal(165d, data.ArrivalsCloseAt);
        Assert.Equal(
            new[] { 0d, 30d, 164d, 165d, 200d },
            data.Series.Single().Points.Select(p => p.Time));
        Assert.Equal(new[] { 0, 2, 1, 0, 3 }, data.Series.Single().Points.Select(p => p.Length));
    }

    [Fact]
    public void Session_NoDrain_StaysAt165()
    {
        var data = QueueLengthChartService.Build(RunWithSessions(), "Day 3 (Tue)");

        Assert.True(data.HasSeries);
        Assert.Equal(165d, data.XAxisMaximum);
        Assert.Null(data.ArrivalsCloseAt);
        Assert.Equal(
            new[] { 0d, 20d, 165d },
            data.Series.Single().Points.Select(p => p.Time));
        Assert.Equal(new[] { 5, 6, 7 }, data.Series.Single().Points.Select(p => p.Length));
    }

    // --------------------------------------------------------------- builder

    [AvaloniaFact]
    public void Builder_PinsTheOperatingAxis()
    {
        var data = QueueLengthChartService.Build(
            RunWithSessions(), QueueLengthChartService.AllSessionsLabel);
        var chart = ChartControlBuilder.BuildQueueChart(data);

        Assert.NotNull(chart);
        var axis = Assert.Single(chart!.XAxes);
        Assert.Equal(0d, axis.MinLimit);
        Assert.Equal(data.TimeAxis!.Maximum, axis.MaxLimit);
        Assert.Equal("Operating minutes (closed time removed)", axis.Name);
        Assert.Equal("Day 1", axis.Labeler!(0));
        Assert.Equal("Day 2", axis.Labeler!(203));
        Assert.Single(chart.Series);
    }

    [AvaloniaFact]
    public void Builder_MarksArrivalsClose()
    {
        var withDrain = QueueLengthChartService.Build(RunWithSessions(), "Day 1 (Sat)");
        var withDrainChart = ChartControlBuilder.BuildQueueChart(withDrain);
        var noDrainChart = ChartControlBuilder.BuildQueueChart(
            QueueLengthChartService.Build(RunWithSessions(), "Day 3 (Tue)"));

        Assert.NotNull(withDrainChart);
        var section = Assert.IsType<LiveChartsCore.SkiaSharpView.RectangularSection>(
            Assert.Single(withDrainChart!.Sections));
        Assert.Equal("arrivals close", section.Label);
        Assert.Equal(165d, section.Xi!.Value);
        Assert.Empty(noDrainChart!.Sections);

        // The marker is a section, not a series: no blank legend entry.
        Assert.Equal(
            new[] { "Reception" },
            withDrainChart.Series.Select(s => s.Name).ToArray());
    }

    [AvaloniaFact]
    public void Builder_SplitsSessionSeams()
    {
        var data = QueueLengthChartService.Build(
            RunWithSessions(), QueueLengthChartService.AllSessionsLabel);
        var chart = ChartControlBuilder.BuildQueueChart(data);

        Assert.NotNull(chart);
        var line = Assert.IsType<LiveChartsCore.SkiaSharpView.StepLineSeries<
            LiveChartsCore.Defaults.ObservablePoint>>(chart!.Series.Single());
        Assert.True(line.EnableNullSplitting);
        Assert.Equal(2, line.Values!.Count(point => double.IsNaN(point.Y ?? 0d)));
    }
}
