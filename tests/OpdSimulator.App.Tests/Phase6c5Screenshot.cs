using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.5 gate evidence (feat/milestone-6c-input-analysis-charts): the real
/// <see cref="MainWindow"/> on the Simulation tab after a real engine run of a
/// manual three-stage clinic (Reception 1 server, Screening 2, Doctor 3), driven
/// through the same seam <see cref="MainViewModel"/> uses
/// (<see cref="SimulationCoordinator.Run"/> → <see cref="ResultsPanelViewModel.CompleteRun"/>).
/// Proves the queue-length-over-time chart (three coloured stage lines) and the
/// waiting-time histogram (first stage selected by default) render from RunResult
/// data and is saved as <c>logs/screenshots/phase-6c5-charts.png</c> — the single
/// integrated screenshot the owner requested for 6c.5.
/// </summary>
public class Phase6c5Screenshot
{
    [AvaloniaFact]
    public void Render_QueueAndWaitHistogramFromRealRun_SavePhase6c5Screenshot()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // tall enough to show both new widgets in one frame
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            Assert.True(main.Results.ShowQueueLength, "the queue-length widget defaults on (FR-UI-14)");
            Assert.True(main.Results.ShowWaitHistogram, "the waiting-time widget defaults on (FR-UI-14)");
            Assert.True(main.Results.ShowQueueLengthEmptyState, "empty until a run finishes");

            var config = new ConfigPanelViewModel();
            config.ParametersIsOptionalEnabled = true;
            config.ManualLambda.Value = "0.1";
            config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
            config.StageRows[0].Servers.Value = "1";
            config.StageRows[1].Servers.Value = "2";
            config.StageRows[2].Servers.Value = "3";

            var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding: null);
            Assert.Null(outcome.Error);
            Assert.NotNull(outcome.Result);
            Assert.Equal(3, outcome.Result!.StageMetrics.Count);

            main.Results.CompleteRun(outcome);

            Assert.True(main.Results.HasQueueLengthChart, "a finished run must build the queue chart");
            Assert.False(main.Results.ShowQueueLengthEmptyState);
            Assert.Equal(QueueLengthChartService.Caption, main.Results.QueueLengthCaption);

            Assert.True(main.Results.HasWaitHistogramChart, "a finished run must build the wait histogram");
            Assert.False(main.Results.ShowWaitHistogramEmptyState);
            Assert.Equal(outcome.Result.StageMetrics[0].StageName, main.Results.SelectedWaitStage);
            Assert.True(main.Results.SelectedWaitStage is not null);

            // The queue chart must draw one line per stage (three stages here).
            var queueChart = main.Results.QueueLengthChart as LiveChartsCore.SkiaSharpView.Avalonia.CartesianChart
                ?? throw new InvalidOperationException("QueueLengthChart must be a CartesianChart");
            Assert.Equal(3, queueChart.Series.Count());

            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-6c5-charts.png");
            frame.Save(shotPath);

            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "queue/wait frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("could not locate OpdSimulator.sln from " + start);
    }
}