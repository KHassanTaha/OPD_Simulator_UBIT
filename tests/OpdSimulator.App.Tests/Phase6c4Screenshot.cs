using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.4 gate evidence (feat/milestone-6c-input-analysis-charts): the real
/// <see cref="MainWindow"/> on the Simulation tab after a real engine run of a
/// manual three-stage clinic (Reception 1 server, Screening 2, Doctor 3), driven
/// through the same seam <see cref="MainViewModel"/> uses
/// (<see cref="SimulationCoordinator.Run"/> → <see cref="ResultsPanelViewModel.CompleteRun"/>).
/// Proves the per-server utilisation widget renders from RunResult data and is
/// saved as <c>logs/screenshots/phase-6c4-utilisation.png</c>.
/// </summary>
public class Phase6c4Screenshot
{
    [AvaloniaFact]
    public void Render_UtilisationWidgetFromRealRun_SavePhase6c4Screenshot()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            Assert.True(main.Results.ShowUtilisation, "the utilisation widget defaults on (FR-UI-14)");
            Assert.True(main.Results.ShowUtilisationEmptyState, "empty until a run finishes");

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

            Assert.True(main.Results.HasUtilisationChart, "a finished run must build the chart");
            Assert.False(main.Results.ShowUtilisationEmptyState);
            Assert.Equal(UtilisationChartService.Caption, main.Results.UtilisationCaption);

            // One bar per server plus one reference line per stage.
            var chart = main.Results.UtilisationChart as CartesianChart
                ?? throw new InvalidOperationException("UtilisationChart must be a CartesianChart");
            var expectedBars = outcome.Result.StageMetrics.Sum(m => m.PerServerUtilisation.Count);
            var expectedSeries = expectedBars + outcome.Result.StageMetrics.Count;
            Assert.Equal(expectedSeries, chart.Series.Count());

            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-6c4-utilisation.png");
            frame.Save(shotPath);

            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "utilisation frame missing or suspiciously small");
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