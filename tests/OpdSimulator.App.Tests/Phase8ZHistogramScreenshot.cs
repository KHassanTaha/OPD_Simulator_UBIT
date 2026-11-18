using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Preprocess;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Z screenshot evidence for the reshaped waiting-time histogram
/// (D-224). Both frames come from a real 3-stage run through
/// <see cref="MainWindow"/>; the linear and log Y axes are asserted on the
/// actual LiveCharts control before the PNG is written. New filenames only —
/// no earlier evidence frame is overwritten (D-166).
/// </summary>
public class Phase8ZHistogramScreenshot
{
    [AvaloniaFact]
    public void Render_WaitHistogramLinear_SavePhase8ZLinearPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            RunThreeStage(main);
            window.UpdateLayout();

            Assert.NotNull(main.Results.WaitHistogram);
            var data = main.Results.WaitHistogram!;
            var chart = Assert.IsType<CartesianChart>(main.Results.WaitHistogramChart);

            Assert.Equal("Waiting time (minutes)", chart.XAxes.Single().Name);
            Assert.Equal("Patients per bin", chart.YAxes.Single().Name);
            Assert.False(chart.YAxes.Single() is LogarithmicAxis);
            Assert.InRange(data.Categories.Count, 1, 12);
            Assert.DoesNotContain("[", data.Categories);
            Assert.Contains("had no wait", main.Results.WaitHistogramCaption);
            Assert.DoesNotContain(WaitHistogramService.LogScaleNote, main.Results.WaitHistogramCaption);

            ScrollHistogramIntoView(window);
            Save(window, "phase-8z-wait-histogram-linear.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_WaitHistogramLog_SavePhase8ZLogPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            RunThreeStage(main);
            main.Results.IsWaitLogScale = true;
            window.UpdateLayout();

            var chart = Assert.IsType<CartesianChart>(main.Results.WaitHistogramChart);
            var y = Assert.IsType<LogarithmicAxis>(chart.YAxes.Single());

            Assert.Equal("Patients per bin (log scale)", y.Name);
            Assert.Equal("Waiting time (minutes)", chart.XAxes.Single().Name);
            Assert.Contains(WaitHistogramService.LogScaleNote, main.Results.WaitHistogramCaption);

            ScrollHistogramIntoView(window);
            Save(window, "phase-8z-wait-histogram-log.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Scrolls the results panel to the Stage dropdown so the histogram widget
    /// is visible in the frame. The dropdown is unique to this widget (the queue
    /// chart uses "Operating session").
    /// </summary>
    private static void ScrollHistogramIntoView(Window window)
    {
        var dropdown = window.GetVisualDescendants().OfType<DropdownField>()
            .Single(d => d.Label == "Stage");
        var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Name == "ResultsScroller");
        scroller.Offset = new Vector(
            0,
            dropdown.TranslatePoint(new Point(0, 0), scroller)!.Value.Y + scroller.Offset.Y - 40);
        window.UpdateLayout();
    }

    private static void RunThreeStage(MainViewModel main)
    {
        var binding = DataAnalyzer.Analyze(SamplePath("sample_3stage_clinic.csv"));
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        for (int i = 0; i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = new[] { 2, 4, 5 }[i].ToString();
        }

        var parameters = config.TryBuildRunParameters()!;
        var outcome = SimulationCoordinator.Run(parameters, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome, parameters, "fitted from sample_3stage_clinic.csv");
    }

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);

        var length = new FileInfo(shotPath).Length;
        Assert.True(length >= 512, $"{fileName} missing or suspiciously small ({length} bytes)");
    }

    private static string SamplePath(string fileName) =>
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", fileName);

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("solution root not found");
    }
}
