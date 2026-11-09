using Avalonia.Headless.XUnit;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8T.3 — the operating-session selector on the queue-length chart
/// (FR-UI-40, D-203).
/// </summary>
/// <remarks>
/// The gate has two halves that a data test cannot cover together: the entries
/// must name only open days, and selecting one must both filter the samples and
/// rebase the horizontal axis to minutes from the opening. The axis itself is
/// asserted on the built chart, because a data record carrying the right number
/// would still render a wrong axis if the builder ignored it.
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

    private static RunOutcome Outcome(SimulationResult result) => new(
        result,
        Array.Empty<FitReport>(),
        Array.Empty<string>(),
        0.3,
        0.1,
        null);

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
    public void SelectingDay3_RebasesItsSamplesToMinutesFromTheOpening()
    {
        var data = QueueLengthChartService.Build(RunWithSessions(), "Day 3 (Tue)");

        Assert.True(data.HasSeries);
        Assert.Equal(165, data.XAxisMaximum);
        var points = data.Series.Single().Points;
        Assert.Equal(new[] { 0d, 20d, 165d }, points.Select(p => p.Time));
        Assert.Equal(new[] { 5, 6, 7 }, points.Select(p => p.Length));
    }

    [Fact]
    public void AllSessions_KeepsTheAbsoluteClockAndEverySample()
    {
        var result = RunWithSessions();
        var data = QueueLengthChartService.Build(result, QueueLengthChartService.AllSessionsLabel);

        Assert.Null(data.XAxisMaximum);
        Assert.Equal(
            result.StageMetrics[0].QueueLengthSeries.Select(s => s.Time),
            data.Series.Single().Points.Select(p => p.Time));
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

    [AvaloniaFact]
    public void FilteredQueueChart_PinsTheXAxisToTheSessionWindow()
    {
        var data = QueueLengthChartService.Build(RunWithSessions(), "Day 3 (Tue)");
        var chart = ChartControlBuilder.BuildQueueChart(data);

        Assert.NotNull(chart);
        var axis = Assert.Single(chart!.XAxes);
        Assert.Equal(0d, axis.MinLimit);
        Assert.Equal(165d, axis.MaxLimit);
    }
}
