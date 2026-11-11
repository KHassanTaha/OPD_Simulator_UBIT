namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
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

    // ------------------------------------------------------------- issue 2 (8T.5)

    [AvaloniaFact]
    public void Render_ShortWindowOneSequence_SavePhase8T5ResultsSequencePng()
    {
        // The size the owner reported. Phase 8T.5 withdraws the pinned rows: at
        // 420 px there is no "scrolling middle" left to starve — the whole panel
        // scrolls as one sequence, header first and trace last (FR-UI-37, D-200).
        // New filename: the phase-8s frame remains evidence of the layout it
        // recorded (D-166).
        Capture("phase-8t5-results-one-sequence.png", 420, main =>
        {
            var window = WindowOf(main);
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Name == "ResultsScroller");
            var trace = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("results-trace-panel"));

            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                $"the sequence must overflow a 420px window: extent {scroller.Extent.Height:F0}, "
                    + $"viewport {scroller.Viewport.Height:F0}");

            // The header is the first item of the sequence, not a band above it.
            double headerTop = mainHeaderTop(main, window);
            Assert.InRange(headerTop, 0, scroller.Viewport.Height);

            // The trace is last: below the fold at offset 0, reachable by the one
            // scrollbar, and not owned by a scroller of its own.
            double traceTop = trace.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            Assert.True(
                traceTop > scroller.Viewport.Height,
                $"the trace starts at y={traceTop:F0} — it should be the last item, below the fold");
            Assert.Empty(trace.GetVisualDescendants().OfType<ScrollViewer>());

            scroller.Offset = new Vector(0, scroller.Extent.Height);
            window.UpdateLayout();
            double traceBottom = trace.TranslatePoint(
                new Point(0, trace.Bounds.Height), window)!.Value.Y;
            Assert.True(
                traceBottom <= window.Bounds.Height + 0.5,
                $"after scrolling to the end the trace ends at y={traceBottom:F0} in a "
                    + $"{window.Bounds.Height:F0}px window");

            // Back to the top for the frame: it records the header-first sequence.
            scroller.Offset = new Vector(0, 0);
            window.UpdateLayout();
        });
    }

    /// <summary>The run summary's top edge, in window coordinates.</summary>
    private static double mainHeaderTop(MainViewModel main, Window window)
    {
        var text = window.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Text == main.Results.RunSummary);
        return text.TranslatePoint(new Point(0, 0), window)!.Value.Y;
    }

    // ------------------------------------------------------------- issue 3

    [AvaloniaFact]
    public void Render_QueueSessionSelected_SavePhase8T3QueueSessionPng()
    {
        // FR-UI-40 / D-203 verification: a 3-session run, then the user opens the
        // "Operating session" dropdown with the keyboard, types the session name
        // and presses Enter. The commit path is the production one; the chart is
        // asserted to be rebased to 0…165 and the frame records the selector in
        // its selected state.
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            RunThreeStageMultiDay(main, new[] { 2, 4, 5 }, "0.8, 0.5, 0.4");
            window.UpdateLayout();

            var dropdown = window.GetVisualDescendants().OfType<DropdownField>()
                .Single(d => d.Label == "Operating session");
            Assert.True(dropdown.IsEffectivelyVisible, "the session selector must be offered after a multi-session run");

            // The results body is one sequence now, so the selector is below the
            // fold at offset 0. Put it on screen before capturing it.
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Name == "ResultsScroller");
            scroller.Offset = new Vector(
                0,
                dropdown.TranslatePoint(new Point(0, 0), scroller)!.Value.Y
                    + scroller.Offset.Y - 40);
            window.UpdateLayout();

            // Phase 8X: the session list is a standard ComboBox; choosing an
            // item is the control's own two-way SelectedItem path.
            var combo = dropdown.FindDescendantOfType<ComboBox>()!;
            Assert.True(combo.ItemCount > 0, "the session selector must offer its options");
            combo.IsDropDownOpen = true;
            window.UpdateLayout();
            Assert.True(combo.IsDropDownOpen, "the session ComboBox must open");

            combo.SelectedItem = "Day 3 (Tue)";
            combo.IsDropDownOpen = false;
            window.UpdateLayout();

            Assert.Equal("Day 3 (Tue)", dropdown.SelectedItem);
            Assert.Equal("Day 3 (Tue)", main.Results.SelectedQueueSession);

            var chart = Assert.IsType<CartesianChart>(main.Results.QueueLengthChart);
            var axis = chart.XAxes.Single();
            Assert.Equal(0d, axis.MinLimit);
            Assert.Equal(165d, axis.MaxLimit);

            Save(window, "phase-8t3-queue-session-selected.png");
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------- issue 3 (cont.)

    [AvaloniaFact]
    public void Render_TraceAfterRun_SavePhase8T4EventTraceTablePng()
    {
        // The window is on screen BEFORE the run, which is the order the app is in and
        // the order the old test got wrong. Phase 8T.4 replaces the single text block
        // with a virtualised table, so this frame is written under a NEW name: the
        // old phase-8s frame is evidence that the pre-table defect existed (D-166).
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

            var table = window.GetVisualDescendants().OfType<ItemsControl>()
                .Single(c => c.Name == "TraceRowList");
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Name == "ResultsScroller");
            var trace = window.GetVisualDescendants().OfType<Border>()
                .First(b => b.Classes.Contains("results-trace-panel"));

            // FR-UI-37: the trace is last in the one sequence, so the frame must
            // scroll the single region to it before the table realises any rows.
            scroller.Offset = new Vector(
                0,
                trace.TranslatePoint(new Point(0, 0), scroller)!.Value.Y + scroller.Offset.Y);
            window.UpdateLayout();

            var realised = table.GetVisualDescendants()
                .OfType<Avalonia.Controls.Presenters.ContentPresenter>()
                .Where(p => p.Content is TraceRow)
                .ToList();
            Assert.NotEmpty(realised);
            Assert.Empty(table.GetVisualDescendants().OfType<ScrollViewer>());

            var rendered = string.Join(
                " ",
                realised[0].GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Select(t => t.Text ?? string.Empty));
            Assert.Contains("ARRIVAL", rendered, StringComparison.Ordinal);
            Assert.True(main.Results.TraceRows.Count > 100,
                $"the trace table holds only {main.Results.TraceRows.Count} rows — it is not the run's trace");

            Save(window, "phase-8t4-event-trace-table.png");
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

    /// <summary>
    /// A three-session run from Saturday, so the queue widget offers one entry
    /// per open session (FR-UI-40, D-203).
    /// </summary>
    private static void RunThreeStageMultiDay(MainViewModel main, int[] servers, string mu)
    {
        var binding = DataAnalyzer.Analyze(SamplePath("sample_3stage_clinic.csv"));
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel
        {
            IsSingleDay = false,
            IsMultiDay = true,
            StartDay = "Saturday",
        };
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = mu;
        config.Days.Value = "3";
        for (int i = 0; i < servers.Length && i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = servers[i].ToString();
        }

        var parameters = config.TryBuildRunParameters()!;
        Assert.Equal(RunMode.MultiDay, parameters.RunMode);
        var outcome = SimulationCoordinator.Run(parameters, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.True(outcome.Result!.Sessions.Count >= 3,
            $"the frame needs at least three open sessions, got {outcome.Result.Sessions.Count}");

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
