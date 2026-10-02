using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.4 gate evidence (D-089) — the Results panel's bottom buffer and the
/// pinned event trace, through the real <see cref="MainWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// The frame is evidence, not the assertion. Every structural claim about the
/// buffer and the trace is asserted in <see cref="ResultsPanelBufferTraceTests"/>
/// against the real objects; what is left here is the picture the owner reviews.
/// This test saves a NEW filename — D-166: an existing frame is the evidence that
/// a defect existed, and re-rendering its filename deletes that evidence.
/// </para>
/// <para>
/// No Width/Height is set: the window is the one <c>MainWindow.axaml</c> declares,
/// because a frame taken at any other size is a picture of a different program
/// (D-166).
/// </para>
/// <para>
/// <see cref="MainWindow"/>'s constructor calls <c>WidgetPreferences.Load()</c>
/// with no argument, which resolves to the real per-user
/// <c>~/.config/OpdSimulator/ui.json</c>. Showing every widget is a visibility
/// change, and any visibility change persists — so the file is snapshotted and
/// restored around this test. A test that quietly rewrote the developer's own
/// preferences would make the next run depend on the last one.
/// </para>
/// </remarks>
public class Phase8Q4Screenshot
{
    [AvaloniaFact]
    public void Render_ResultsBottomBufferAndPinnedTrace_SavePhase8qBufferTracePng()
    {
        string prefsPath = WidgetPreferences.DefaultFilePath;
        string? backup = File.Exists(prefsPath) ? File.ReadAllText(prefsPath) : null;

        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);

            // Every widget on, so the frame shows the buffer's job: clearing the
            // last card off the pinned trace.
            foreach (string key in new[]
            {
                "metrics", "chiSquare", "utilisation", "queueLength",
                "waitHistogram", "simulationVerification", "analyticalValidation",
            })
            {
                if (!main.Results.VisibleWidgets.Contains(key))
                {
                    main.Results.ToggleWidget(key);
                }
            }

            RunSimulation(main);
            window.UpdateLayout();

            var text = RenderedText(window);

            // The trace is pinned and labelled, not scrolled away with the widgets.
            Assert.Contains("Event Trace", text);
            Assert.Contains("Every event of the last run", text);

            // The last widget ends above the trace instead of hiding behind it.
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Name == "WidgetScroller");
            var buffer = scroller.GetVisualDescendants().OfType<Border>()
                .Single(b => b.Name == "ResultsBottomBuffer");
            var lastWidget = buffer.GetVisualAncestors().OfType<StackPanel>().First();
            var cards = lastWidget.Children.OfType<Border>().ToArray();
            Assert.NotEmpty(cards);

            scroller.Offset = new Vector(0, scroller.Extent.Height);
            window.UpdateLayout();

            var widgetBottom = cards[^1]
                .TranslatePoint(new Point(0, cards[^1].Bounds.Height), window)!.Value.Y;
            var traceTop = FindByText(window, "Event Trace")
                .TranslatePoint(new Point(0, 0), window)!.Value.Y;

            Assert.True(
                widgetBottom <= traceTop + 1,
                $"the last widget ends at y={widgetBottom:0} but the pinned trace "
                    + $"starts at y={traceTop:0} — the buffer did not clear it");

            Save(window, "phase-8q-results-buffer-trace.png");
        }
        finally
        {
            window.Close();
            if (backup is not null)
            {
                File.WriteAllText(prefsPath, backup);
            }
        }
    }

    /// <summary>Runs a real three-stage simulation and hands the outcome to the panel.</summary>
    private static void RunSimulation(MainViewModel main)
    {
        var sample = Path.Combine(
            FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv");
        var binding = DataAnalyzer.Analyze(sample);
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.5, 0.25, 0.2";
        for (int i = 0; i < 3 && i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = new[] { "1", "2", "2" }[i];
        }

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome);
    }

    private static TextBlock FindByText(Window window, string text) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == text)
        ?? throw new InvalidOperationException($"no TextBlock with text '{text}' in the visual tree");

    private static MainViewModel RequireMain(Window window) =>
        window.DataContext as MainViewModel
        ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static string RenderedText(Window window) => string.Join(
        "\n",
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        Assert.True(
            File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
            $"{fileName} missing or suspiciously small");
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