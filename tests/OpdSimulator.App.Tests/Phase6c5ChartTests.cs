using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Engine;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.5 gate (feat/milestone-6c-input-analysis-charts) — the two P2
/// Results-panel charts: queue-length-over-time and the waiting-time histogram
/// (FR-UI-4 P2). Verification intent:
/// - the queue chart never renders more than 2000 points per stage, keeps the
///   first/last samples verbatim, and provably retains the global min/max
///   queue (the extremes are what a reader looks for, D-122);
/// - the wait histogram bins one stage at a time, data/window follows the
///   selector, the selector resets to the first stage on every new run, and
///   the log-scale toggle really swaps the Y axis;
/// - empty states hold until a finished run exists.
/// Service-level assertions are plain Facts; the two chart-internals tests are
/// AvaloniaFacts because LiveCharts controls only construct inside a rendered
/// Avalonia app (D-121 test lesson).
/// </summary>
public class Phase6c5ChartTests
{
    [Fact]
    public void QueueLengthChart_DownsampledTo2000Points_Max()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics { StageName = "Reception", QueueLengthSeries = Queue(10_000) },
                new StageMetrics { StageName = "Screening", QueueLengthSeries = Queue(5_000) },
            },
        };

        var data = QueueLengthChartService.Build(result);

        Assert.True(data.HasSeries, "a finished run must contribute sampled points");
        Assert.Equal(2, data.Series.Count);
        Assert.All(data.Series, s => Assert.True(
            s.Points.Count <= QueueLengthChartService.MaxPoints,
            $"{s.StageName} must be downsampled to at most {QueueLengthChartService.MaxPoints} points"));
        Assert.True(data.Series[0].Points.Count > 1, "thinning must still keep a meaningful series");
    }

    [Fact]
    public void QueueLengthChart_PreservesFirstLastAndMinMax()
    {
        var samples = Enumerable.Range(0, 10_000)
            .Select(i => new QueueSample(i * 30.0, (i * 37) % 500))
            .ToArray();

        var decimated = QueueLengthChartService.Decimate(samples);

        Assert.True(decimated.Count < samples.Length, "a long series must actually thin");
        Assert.True(samples[0] == decimated[0], "the opening sample must be kept verbatim");
        Assert.True(samples[^1] == decimated[^1], "the final sample must be kept verbatim");
        Assert.True(samples.Min(s => s.Length) == decimated.Min(s => s.Length),
            "the global minimum queue length must survive the decimation");
        Assert.True(samples.Max(s => s.Length) == decimated.Max(s => s.Length),
            "the global maximum queue length must survive the decimation");
        for (int i = 1; i < decimated.Count; i++)
        {
            Assert.True(decimated[i].Time > decimated[i - 1].Time,
                "decimated points must stay in time order");
        }
    }

    [Fact]
    public void QueueLengthChart_HasOneLinePerStage()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics { StageName = "Reception", QueueLengthSeries = Queue(120) },
                new StageMetrics { StageName = "Screening", QueueLengthSeries = Queue(80) },
            },
        };

        // The service emits exactly the per-stage data the builder turns into
        // one LineSeries per stage. The chart-level proof lives in the
        // rendered-window screenshot test (Phase6c5Screenshot asserts 3 chart
        // series); constructing bare LiveCharts controls here flaked the
        // headless dispatcher in a full-suite run (D-121 lesson).
        var data = QueueLengthChartService.Build(result);

        Assert.True(data.HasSeries);
        Assert.Equal(new[] { "Reception", "Screening" }, data.Series.Select(s => s.StageName));
        Assert.All(data.Series, s => Assert.True(s.Points.Count > 0,
            $"{s.StageName} must contribute sampled points"));
        Assert.All(data.Series, s => Assert.True(s.Points.Count <= QueueLengthChartService.MaxPoints));
    }

    [Fact]
    public void WaitHistogram_StageSelectorChangesData()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics { StageName = "Reception", WaitingTimeSamples = new[] { 0.5, 1.0, 1.4, 2.0, 30.0 } },
                new StageMetrics { StageName = "Doctor", WaitingTimeSamples = new[] { 10.0, 12.0, 14.0, 60.0 } },
            },
        };

        var reception = WaitHistogramService.Build(result, "Reception");
        var doctor = WaitHistogramService.Build(result, "Doctor");
        Assert.Equal("Reception", reception.StageName);
        Assert.Equal("Doctor", doctor.StageName);
        Assert.True(reception.HasSeries && doctor.HasSeries);
        Assert.False(reception.Counts.SequenceEqual(doctor.Counts),
            "different stages bin different samples");

        // The widget's selector defaults to the first stage…
        var vm = new ResultsPanelViewModel();
        vm.StartRun();
        vm.CompleteRun(new RunOutcome(result, Array.Empty<FitReport>(), Array.Empty<string>(),
            SimulationCoordinator.DefaultExitProbability, null));

        Assert.Equal("Reception", vm.SelectedWaitStage);
        Assert.Equal("Reception", vm.WaitHistogram?.StageName);

        // …and data follows the selection.
        vm.SelectedWaitStage = "Doctor";
        Assert.Equal("Doctor", vm.WaitHistogram?.StageName);
        Assert.Equal(doctor.Counts.ToArray(), vm.WaitHistogram?.Counts.ToArray());

        // A new run resets the selection to ITS first stage (deterministic UI).
        var rerun = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics { StageName = "Screening", WaitingTimeSamples = new[] { 3.0 } },
                new StageMetrics { StageName = "Doctor", WaitingTimeSamples = new[] { 40.0, 60.0 } },
            },
        };
        vm.CompleteRun(new RunOutcome(rerun, Array.Empty<FitReport>(), Array.Empty<string>(),
            SimulationCoordinator.DefaultExitProbability, null));
        Assert.Equal("Screening", vm.SelectedWaitStage);
        Assert.Equal("Screening", vm.WaitHistogram?.StageName);
    }

    [AvaloniaFact]
    public void WaitHistogram_LogScaleToggle_ChangesAxis()
    {
        // Window harness: chart controls must construct inside a rendered Avalonia
        // app (the same seam the screenshot test proves). The toggle goes through
        // the real widget code path — CompleteRun → rebuild on IsWaitLogScale.
        var window = new MainWindow();
        window.Show();
        try
        {
            ResultsPanelViewModel results = ((MainViewModel)window.DataContext!).Results;
            var result = new SimulationResult
            {
                StageMetrics = new[]
                {
                    new StageMetrics
                    {
                        StageName = "Doctor",
                        WaitingTimeSamples = new[] { 1.0, 2.0, 3.0, 4.0, 500.0 }, // long tail → empty bins
                    },
                },
            };
            results.StartRun();
            results.CompleteRun(new RunOutcome(result, Array.Empty<FitReport>(), Array.Empty<string>(),
                SimulationCoordinator.DefaultExitProbability, null));

            var linear = results.WaitHistogramChart as LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart
                ?? throw new InvalidOperationException("WaitHistogramChart must be a CartesianChart");
            Assert.False(linear.YAxes.ElementAt(0) is LiveChartsCore.SkiaSharpView.LogarithmicAxis,
                "linear mode must use a plain axis");

            results.IsWaitLogScale = true;

            var log = results.WaitHistogramChart as LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart
                ?? throw new InvalidOperationException("log toggle must rebuild the chart");
            Assert.True(log.YAxes.ElementAt(0) is LiveChartsCore.SkiaSharpView.LogarithmicAxis,
                "log mode must swap the Y axis for a base-10 logarithmic axis");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void WaitHistogram_EmptyState_WhenNoRun()
    {
        var empty = WaitHistogramService.Build(null, "Doctor");
        Assert.False(empty.HasSeries);
        Assert.Empty(empty.Categories);
        Assert.Empty(empty.Counts);

        var vm = new ResultsPanelViewModel();
        Assert.False(vm.HasWaitHistogramChart);
        Assert.True(vm.ShowWaitHistogramEmptyState);
        Assert.Null(vm.WaitStageNames);
        Assert.Null(vm.SelectedWaitStage);
        Assert.Null(vm.WaitHistogram);

        // A refused run keeps the empty state — no stage to select, no data.
        vm.StartRun();
        vm.CompleteRun(new RunOutcome(null, Array.Empty<FitReport>(), Array.Empty<string>(),
            SimulationCoordinator.DefaultExitProbability, SimulationCoordinator.MissingArrivalRateMessage));
        Assert.False(vm.HasWaitHistogramChart);
        Assert.True(vm.ShowWaitHistogramEmptyState);
        Assert.Null(vm.WaitStageNames);
        Assert.Null(vm.WaitHistogram);
    }

    /// <summary>A queue-length series with a spiky, deterministic pattern.</summary>
    private static QueueSample[] Queue(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new QueueSample(i * 30.0, (i * 37) % 500))
            .ToArray();
}