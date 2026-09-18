using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.6 gate evidence (feat/milestone-6c-input-analysis-charts): the three
/// consolidated screenshots describing the finished 6C chart suite. Each test
/// drives the real <see cref="MainWindow"/> through the same seam
/// <see cref="MainViewModel"/> uses and saves one frame into
/// <c>logs/screenshots/</c>:
/// <list type="bullet">
/// <item><c>phase-6c-input-analysis.png</c> — Input tab: histogram +
/// fitted-PDF card and chi-square observed-vs-expected card per fit.</item>
/// <item><c>phase-6c-results-all.png</c> — Simulation tab after a real engine
/// run with the sample multi-stage clinic loaded: all seven widgets (metrics,
/// utilisation, queue length, waiting-time histogram, chi-square, simulation
/// verification, trace) in one frame.</item>
/// <item><c>phase-6c-widget-toggled.png</c> — same run with the "Customise
/// results" picker open and one widget (waiting-time history) switched off.</item>
/// </list>
/// </summary>
public class Phase6c6Screenshots
{
    [AvaloniaFact]
    public void Render_InputAnalysisOneFrame_SavePhase6cInputAnalysisPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // 4 cards (2 histograms + 2 chi-square) fit vertically
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
            Assert.True(binding.IsUsable, "the sample CSV must analyse cleanly for the screenshot");
            // Phase 7D: the fit analysis lives inside the Input tab, which only
            // renders it once a file is loaded.
            main.InputTab.SetLoadedFile(binding);
            main.InputAnalysis.Apply(binding, "Exponential", "Exponential", 0.05);
            Assert.Equal(4, main.InputAnalysis.Charts.Count); // histogram + chi-square per fit
            Assert.Equal("Chi-square: Inter-arrival", main.InputAnalysis.Charts[1].Title);
            Assert.Equal("Chi-square: Screening service", main.InputAnalysis.Charts[3].Title);
            Assert.All(main.InputAnalysis.Charts, c => Assert.NotNull(c.ChartContent));

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var frame = Capture(window, "phase-6c-input-analysis.png");
            Assert.True(frame >= 512, "input-analysis frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_ResultsAllWidgetsFromRealRun_SavePhase6cResultsAllPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 5600; // every widget, including the event trace, fits one frame
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            string[] originalWidgets = ForceAllWidgetsVisible(main.Results);
            try
            {
                (var outcome, _) = RunRealThreeStage(main);

                // Scroll-viewer content realises during layout; the widget stack
                // is not in the visual tree until a layout pass runs.
                window.UpdateLayout();

                AssertWidgetsFromRealRun(main, outcome);
                AssertAllWidgetsInsideOneFrame(window);

                long frame = Capture(window, "phase-6c-results-all.png");
                Assert.True(frame >= 512, "results-all frame missing or suspiciously small");
            }
            finally
            {
                RestoreVisibility(main.Results, originalWidgets);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_WidgetToggledOffWithPickerOpen_SavePhase6cWidgetToggledPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 5600;
        window.Show();
        try
        {
if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            string[] originalWidgets = ForceAllWidgetsVisible(main.Results);
            try
            {
                (var outcome, _) = RunRealThreeStage(main);

                // Open the "Customise results" picker and switch one widget off —
                // the TwoWay checkbox follows the view-model flag (FR-UI-14).
                var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
                var picker = panel.GetVisualDescendants().OfType<Border>()
                    .Single(b => string.Equals(b.Name, "WidgetPicker", StringComparison.Ordinal));
                picker.IsVisible = true;
                main.Results.ToggleWidget("waitHistogram");
                Assert.False(main.Results.ShowWaitHistogram);
                Assert.DoesNotContain("waitHistogram", main.Results.VisibleWidgets);

                window.UpdateLayout();
                var waitBox = picker.GetVisualDescendants().OfType<CheckBox>()
                    .Single(c => string.Equals(c.Content?.ToString(), "Wait histogram", StringComparison.Ordinal));
                Assert.False(waitBox.IsChecked, "the picker checkbox must mirror the toggled-off widget");

                // The widget itself must be hidden from the view (not just the flag).
                var waitCard = panel.GetVisualDescendants().OfType<TextBlock>()
                    .Single(t => t.Text == "Waiting-time distribution")
                    .GetVisualAncestors().OfType<Border>().First(); // nearest ancestor Border IS the widget card
                Assert.False(waitCard.IsVisible, "the wait-histogram card must disappear when its widget is off");

                long frame = Capture(window, "phase-6c-widget-toggled.png");
                Assert.True(frame >= 512, "widget-toggled frame missing or suspiciously small");
            }
            finally
            {
                RestoreVisibility(main.Results, originalWidgets);
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static readonly string[] WidgetKeys =
    {
        "metrics", "chiSquare", "trace",
        "utilisation", "queueLength", "waitHistogram", "simulationVerification",
    };

    /// <summary>
    /// The results screenshots must show a stable all-widgets frame regardless of
    /// the developer machine's real <c>ui.json</c> (FR-UI-14 preferences are
    /// persisted per-user and MainViewModel loads them), so this test forces
    /// every widget visible for the capture and restores the original set in
    /// <c>finally</c> — leaving the machine's preferences exactly as they were.
    /// </summary>
    private static string[] ForceAllWidgetsVisible(ResultsPanelViewModel results)
    {
        string[] original = results.VisibleWidgets.ToArray();
        foreach (string key in WidgetKeys)
        {
            if (!results.VisibleWidgets.Contains(key))
            {
                results.ToggleWidget(key);
            }
        }

        return original;
    }

    private static void RestoreVisibility(ResultsPanelViewModel results, string[] original)
    {
        foreach (string key in WidgetKeys)
        {
            if (original.Contains(key) != results.VisibleWidgets.Contains(key))
            {
                results.ToggleWidget(key);
            }
        }
    }

    /// <summary>
    /// Runs a stable manual three-stage clinic (Reception 1 / Screening 2 /
    /// Doctor 3, exponential arrivals λ=0.1/min) against the loaded sample data,
    /// so the chi-square table and the data preview both carry real content.
    /// </summary>
    private static (RunOutcome Outcome, DataBindingResult Binding) RunRealThreeStage(MainViewModel main)
    {
        var binding = DataAnalyzer.Analyze(SamplePath("sample_3stage_clinic.csv"));
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        config.StageRows[0].Servers.Value = "1";
        config.StageRows[1].Servers.Value = "2";
        config.StageRows[2].Servers.Value = "3";

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.Equal(3, outcome.Result!.StageMetrics.Count);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome);

        // Phase 8B: the production completion path also verifies the engine's
        // generated output samples; mirror it here so the seventh widget carries
        // real content in the frame (RunRealThreeStage bypasses OnRunRequested).
        main.SimulationVerification.Apply(
            outcome.Result,
            config.InterArrivalDistribution ?? "Exponential",
            Enumerable.Repeat(config.ServiceDistribution ?? "Exponential", outcome.Result.StageMetrics.Count).ToList(),
            0.05);
        return (outcome, binding);
    }

    /// <summary>
    /// Proves every one of the seven widgets carries real, non-empty content
    /// after the run wiring (the screenshot is evidence, the asserts are the
    /// proof). Phase 7D removed the data-preview widget from this panel;
    /// Phase 8B added the simulation-verification widget.
    /// </summary>
    private static void AssertWidgetsFromRealRun(MainViewModel main, RunOutcome outcome)
    {
        var r = main.Results;

        // 1. Metrics table (system totals + 3 stage rows).
        Assert.Equal(6, r.SystemMetrics.Count);
        Assert.Equal(3, r.StageRows.Count);

        // 2. Per-server utilisation: 6 bars (1 + 2 + 3 servers) + 3
        // stage-mean reference lines (D-121).
        Assert.True(r.HasUtilisationChart);
        var utilisation = r.UtilisationChart as CartesianChart
            ?? throw new InvalidOperationException("UtilisationChart must be a CartesianChart");
        Assert.Equal(9, utilisation.Series.Count());
        Assert.Equal(6, utilisation.Series.OfType<ColumnSeries<double?>>().Count());
        Assert.Equal(3, utilisation.Series.OfType<LineSeries<double?>>().Count());

        // 3. Queue length over time (one line per stage).
        Assert.True(r.HasQueueLengthChart);
        var queue = r.QueueLengthChart as CartesianChart
            ?? throw new InvalidOperationException("QueueLengthChart must be a CartesianChart");
        Assert.Equal(3, queue.Series.Count());

        // 4. Waiting-time histogram (16 bins, first stage selected by default).
        Assert.True(r.HasWaitHistogramChart);
        Assert.NotNull(r.WaitHistogram);
        Assert.Equal(16, r.WaitHistogram!.Categories.Count);
        Assert.Equal(outcome.Result!.StageMetrics[0].StageName, r.SelectedWaitStage);

        // 5. Chi-square table: Inter-arrival + one row per stage (4 fits).
        Assert.Equal(4, r.ChiSquareRows.Count);

        // 6. Event trace is populated at State level.
        Assert.False(string.IsNullOrWhiteSpace(r.TraceText));

        // 7. Simulation verification (Phase 8B): one report card per series
        // (inter-arrival + 3 stages). The inter-arrival card is always drawable;
        // the low-arrival sample run can legitimately leave a downstream stage
        // with too few service samples for a fit (the card then explains why).
        Assert.False(r.SimulationVerification.IsEmpty);
        Assert.Equal(4, r.SimulationVerification.Charts.Count);
        Assert.NotNull(r.SimulationVerification.Charts[0].ChartContent);

        // And the FR-UI-14 contract: all seven widgets are visible together.
        Assert.Equal(new[] { "metrics", "chiSquare", "trace",
            "utilisation", "queueLength", "waitHistogram", "simulationVerification" }, r.VisibleWidgets);
    }

    /// <summary>
    /// The widget stack scrolls (5c.1), so a tall window alone does not prove the
    /// capture shows everything — a widget below the results viewport would be
    /// clipped. The event trace is the last widget in the stack, so if its card
    /// bottom fits inside the window, every widget above it does too.
    /// </summary>
    private static void AssertAllWidgetsInsideOneFrame(Window window)
    {
        var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
        var headers = panel.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        var traceHeader = panel.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == "Event trace");
        Assert.True(traceHeader is not null,
            $"event-trace header not realised; headers found were: [{string.Join(", ", headers)}]");
        var traceCard = traceHeader.GetVisualAncestors().OfType<Border>().First();
        Point? bottom = traceCard.TranslatePoint(new Point(0, traceCard.Bounds.Height), window);
        Point mapped = bottom ?? throw new InvalidOperationException("the trace card must map into the window coordinate space");
        double cardBottom = mapped.Y;
        Assert.True(cardBottom <= window.ClientSize.Height + 1,
            $"the last widget ends at y={cardBottom:0} but the window client is only {window.ClientSize.Height:0} tall — the frame would clip it");
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

    private static string SamplePath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"Sample file {fileName} not found above {AppContext.BaseDirectory}");
    }
}