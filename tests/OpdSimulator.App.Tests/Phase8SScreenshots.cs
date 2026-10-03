namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Preprocess;
using Xunit;


/// <summary>
/// Phase 8S evidence frames — one per owner-reported issue.
/// </summary>
/// <remarks>
/// New filenames, never overwrites. These are the frames the owner reviews, so the
/// pre-fix frames stay on disk as the evidence that the defects existed (D-166).
/// </remarks>
public sealed class Phase8SScreenshots
{
    // ------------------------------------------------------------- issue 1

    [AvaloniaFact]
    public void Render_OverviewAlignment_SavePhase8sOverviewAlignmentPng()
    {
        Capture("phase-8s-overview-alignment.png", 900, main =>
        {
            var vm = main.Results;
            Assert.NotEmpty(vm.SystemMetrics);

            var host = vm.SystemMetrics[0].Label;
            var listing = WindowOf(main).GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == host)
                .GetVisualAncestors().OfType<ItemsControl>().First();

            // The value sits beside its label: the cell hugs its text rather than
            // stretching to the panel's far edge.
            foreach (var row in listing.GetVisualDescendants().OfType<Grid>())
            {
                var cells = row.Children.OfType<TextBlock>().ToList();
                if (cells.Count != 2) continue;

                var value = cells[1];
                Assert.True(
                    value.Bounds.Width <= value.TextLayout.Width + 8,
                    $"'{cells[0].Text}' value cell is {value.Bounds.Width:F0}px wide for " +
                    $"{value.TextLayout.Width:F0}px of text");
            }
        });
    }

    // ------------------------------------------------------------- issue 2

    [AvaloniaFact]
    public void Render_ShortWindowLayout_SavePhase8sResultsLayoutPng()
    {
        // The size the owner reported: short enough that the pinned rows used to
        // squeeze the scrolling middle down to 101 px.
        Capture("phase-8s-results-layout.png", 420, main =>
        {
            var window = WindowOf(main);
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Name == "WidgetScroller");
            var trace = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("results-trace-panel"));

            Assert.True(scroller.Bounds.Height >= 120,
                $"middle row was {scroller.Bounds.Height:F0}px at a 420px window");
            Assert.True(trace.Bounds.Height <= 208.5,
                $"trace panel was {trace.Bounds.Height:F0}px, over its 208px ceiling");
        });
    }

    // ------------------------------------------------------------- issue 3

    [AvaloniaFact]
    public void Render_TraceAfterRun_SavePhase8sEventTracePopulatedPng()
    {
        // The window is on screen BEFORE the run, which is the order the app is in and
        // the order the old test got wrong.
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            Assert.Equal(
                "No trace was recorded for this run.",
                main.Results.TraceBody);

            RunThreeStage(main, new[] { 2, 4, 5 }, "0.8, 0.5, 0.4");
            window.UpdateLayout();

            var rendered = window.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Single(t => t.Name == "TraceBodyText")
                .Text ?? string.Empty;

            Assert.Contains("ARRIVAL", rendered, StringComparison.Ordinal);
            Assert.NotEqual("No trace was recorded for this run.", rendered);
            Assert.True(rendered.Length > 1000,
                $"the rendered trace is only {rendered.Length} characters — it is not the run's trace");

            Save(window, "phase-8s-event-trace-populated.png");
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------- issue 4

    [AvaloniaFact]
    public void Render_NoPerServerTable_SavePhase8sUtilisationBenchmarkPng()
    {
        Capture("phase-8s-utilisation-benchmark.png", 2400, main =>
        {
            Assert.True(main.Results.HasUtilisationChart);
            Assert.NotNull(main.Results.UtilisationChart);

            // The removed section really is gone from the rendered panel, not hidden.
            var sections = WindowOf(main).GetVisualDescendants()
                .OfType<OpdSimulator.App.Controls.CollapsibleSection>()
                .Select(s => s.Title ?? string.Empty)
                .ToList();
            Assert.DoesNotContain(
                sections,
                t => t.Contains("Per-server detail", StringComparison.OrdinalIgnoreCase));
        });
    }

    // ------------------------------------------------------------- harness

    private static Window Last = null!;

    private static Window WindowOf(MainViewModel _) => Last;

    private static void Capture(string fileName, int height, Action<MainViewModel> assert)
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = height;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            Last = window;

            RunThreeStage(main, new[] { 2, 4, 5 }, "0.8, 0.5, 0.4");
            window.UpdateLayout();

            assert(main);

            Save(window, fileName);
        }
        finally
        {
            window.Close();
        }
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

        main.Results.StartRun();
        main.Results.CompleteRun(outcome, parameters, "fitted from sample_3stage_clinic.csv");
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
