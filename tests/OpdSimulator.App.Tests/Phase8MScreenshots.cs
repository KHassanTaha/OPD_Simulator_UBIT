using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Preprocess;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8M gate evidence — the five frames the owner asked to see, each taken
/// from the real <see cref="MainWindow"/> after a real three-stage run.
/// </summary>
/// <remarks>
/// The point of these frames is the thing a unit test cannot judge: whether the
/// labels are actually painted, whether the stage colours match between the
/// legend and the charts, and whether the eleven-bar case is still readable. The
/// assertions therefore check that the frame was captured and that the widgets
/// really carry the expected series, and the visual pass is the owner's.
/// </remarks>
public class Phase8MScreenshots
{
    [AvaloniaFact]
    public void Render_UtilisationContributionChart_SavePhase8mUtilisationPng()
    {
        // The 1/2/3 six-bar case: contribution heights, the equal-share line per
        // stage, the amber overlay, the caption and the detail table.
        CaptureResults("phase-8m-utilisation.png", new[] { 1, 2, 3 }, "0.8, 0.5, 0.4", 2400, main =>
        {
            Assert.True(main.Results.HasUtilisationChart);
            Assert.NotNull(main.Results.UtilisationChart);
            // One row per server under the chart, collapsed by default.
            Assert.Equal(6, main.Results.PerServerDetailRows.Count);
        });
    }

    [AvaloniaFact]
    public void Render_UtilisationVariedServerCounts_SavePhase8mUtilisationVariedPng()
    {
        // 2/4/5 servers → eleven bars, with two genuinely deviating servers so
        // the amber overlay has something to show.
        // 2/4/5 servers → eleven bars. A real run rarely balances perfectly, so
        // this frame is also where the amber overlay is exercised for real.
        CaptureResults("phase-8m-utilisation-varied.png", new[] { 2, 4, 5 }, "0.8, 0.5, 0.4", 2400, main =>
        {
            Assert.True(main.Results.HasUtilisationChart);
            Assert.Equal(11, main.Results.PerServerDetailRows.Count);
        });
    }

    [AvaloniaFact]
    public void Render_QueueStepChart_SavePhase8mQueueStepPng()
    {
        CaptureResults("phase-8m-queue-step.png", new[] { 1, 2, 3 }, "0.8, 0.5, 0.4", 3000, main =>
        {
            Assert.True(main.Results.HasQueueLengthChart);
        });
    }

    [AvaloniaFact]
    public void Render_StageLegend_SavePhase8mLegendPng()
    {
        // The swatch row beside the Charts heading, generated from the run.
        CaptureResults("phase-8m-legend.png", new[] { 1, 2, 3 }, "0.8, 0.5, 0.4", 1500, main =>
        {
            Assert.Equal(3, main.Results.StageLegend.Count);
            Assert.All(main.Results.StageLegend, item => Assert.NotNull(item.Swatch));
        });
    }

    /// <summary>
    /// Runs the real engine, asserts what the populated panel should contain,
    /// and captures one frame of the window tall enough to show the widgets.
    /// </summary>
    private static void CaptureResults(
        string fileName,
        int[] servers,
        string mu,
        int height,
        Action<MainViewModel> assert)
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = height;
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            RunThreeStage(main, servers, mu);
            window.UpdateLayout();

            assert(main);

            long frame = Capture(window, fileName);
            Assert.True(frame >= 512, $"{fileName} missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Runs the real three-stage sample through the real coordinator, with the
    /// server counts the caller asked for.
    /// </summary>
    private static void RunThreeStage(MainViewModel main, int[] servers, string mu)
    {
        var binding = DataAnalyzer.Analyze(SamplePath("sample_3stage_clinic.csv"));
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = mu;
        for (int i = 0; i < servers.Length && i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = servers[i].ToString();
        }

        var parameters = config.TryBuildRunParameters()!;
        var outcome = SimulationCoordinator.Run(parameters, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.Equal(servers.Length, outcome.Result!.StageMetrics.Count);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome, parameters, "fitted from sample_3stage_clinic.csv");
    }

    private static long Capture(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        return new FileInfo(shotPath).Length;
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

    private static string SamplePath(string fileName) =>
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", fileName);
}
