using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.1 gate evidence (D-178) — the Input tab's recorded server counts and
/// the historical utilisation computed from them, through the real
/// <see cref="MainWindow"/>, per the D-089 method.
/// </summary>
/// <remarks>
/// The frame is evidence, not the assertion. Every structural claim is asserted in
/// <see cref="HistoricalMetricsTests"/> against the real objects; what is left here
/// is the picture the owner reviews. This test saves a NEW filename — D-166: an
/// existing frame is the evidence that a defect existed, and re-rendering its
/// filename deletes that evidence.
/// </remarks>
public class Phase8Q1Screenshot
{
    [AvaloniaFact]
    public void Render_InputServerCountsAndHistoricalUtilisation_SavePhase8qInputServersPng()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));

            SelectInputTab(window);

            // The user records what was actually open during collection. Screening
            // gets two tables, which is what a real clinic figure needs: at c = 1
            // the stage reads above 100% and says so.
            var counts = main.InputTab.ServerCounts;
            foreach (var row in counts)
            {
                row.Servers = row.StageName == "Screening" ? "2" : "1";
            }

            window.UpdateLayout();

            // The section is only meaningful with the figures beside the inputs, so
            // the assertions are about BOTH being on screen together.
            var text = RenderedText(window);

            Assert.Contains("Servers present during data collection", text);
            Assert.Contains("Historical utilisation", text);
            Assert.Contains("Reception", text);
            Assert.Contains("Screening", text);
            Assert.Contains("Doctor", text);

            // Every recorded count is shown as a field with a unit (AGENTS §16.7).
            Assert.Equal(3, counts.Count);
            Assert.All(counts, r => Assert.False(r.HasError));

            // The historical figures exist for each stage, and each row carries the
            // division that produced it.
            var stages = main.InputTab.HistoricalMetrics.Stages;
            Assert.NotEmpty(stages);
            Assert.All(stages, row => Assert.Contains("÷", row.CalculationText));

            // The basis line names the divisor (rulings 3) — a reader must be able
            // to reproduce a figure from what is printed.
            Assert.NotEmpty(main.InputTab.HistoricalMetrics.BasisText);

            // The per-server limitation is stated rather than implied.
            Assert.Contains("server-ID columns are not present", text);

            Save(window, "phase-8q-input-servers.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_InputHistoricalUtilisationAboveCapacity_SavePhase8qInputServersWarningPng()
    {
        // The real clinic capture at c = 1, where the recorded busy time exceeds
        // the recorded capacity. This is the only file that shows it.
        //
        // SKIPPED when the capture is absent, and it is absent by design: the
        // clinic file is real operational data and deliberately not committed
        // (ruling 1), so CI never has it. Skipping is the honest outcome — the
        // alternative is to assert the warning against a committed fixture that
        // never exceeds 100% and so never shows it, which is a green test standing
        // in for evidence that does not exist.
        //
        // The behaviour is asserted everywhere else, against constructed data, in
        // HistoricalMetrics_BusyTimeAboveCapacity_ReportsWarningAndDoesNotClamp.
        // What this frame adds is the picture of the real figure, for review (§18).
        var clinicFile = Sample("opd_collection_28_sep_2026_1.csv");
        if (!File.Exists(clinicFile))
        {
            return;
        }

        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(clinicFile);
            SelectInputTab(window);

            // One server each — which is what makes the recorded busy time exceed
            // the recorded capacity on this file.
            foreach (var row in main.InputTab.ServerCounts)
                row.Servers = "1";

            window.UpdateLayout();

            // The capture has no reception columns, so the fields follow the file:
            // Screening and Doctor only. Asserted so the frame cannot quietly drift
            // into offering a Reception field the data cannot support.
            Assert.Equal(
                new[] { "Screening", "Doctor" },
                main.InputTab.ServerCounts.Select(r => r.StageName).ToArray());

            var screening = main.InputTab.HistoricalMetrics.Stages
                .Single(r => r.StageName == "Screening");

            Assert.True(
                screening.HasCapacityWarning,
                "this file's screening time exceeds a single server's capacity, so the warning must show");
            Assert.Contains("Raise the server count", screening.CapacityWarning);

            // The raw figure survives: clamping to 100% would hide the very
            // inconsistency the section exists to surface (rulings 4).
            Assert.True(screening.Utilisation > 1.0, "the raw figure must not be clamped");

            // The division stays checkable on screen (D-176).
            Assert.Contains($"{screening.ServerCount} ×", screening.CalculationText);

            Save(window, "phase-8q-input-servers-warning.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputServerCountSection_SurvivesResizeToMinimumWindowSize()
    {
        // D-169. The main window is resizable (MinWidth 1100, MinHeight 700), so a
        // section that only lays out correctly at 1200×760 is a section that breaks
        // for a user who shrinks the window. The 8Q.1 section is new, so it is
        // checked here rather than discovered by hand.
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));
            SelectInputTab(window);
            window.UpdateLayout();

            FindByText(window, "Servers present during data collection");

            // -- assert the resize actually took effect --------------------------------
            // The headless platform applies a window resize through the dispatcher, so
            // setting Height and calling UpdateLayout() can leave the window at its old
            // size. Without this assertion the test would measure the default layout and
            // pass against the very defect it was written for.
            window.Width = 1100;
            window.Height = 700;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var narrowed = window.Bounds;
            Assert.True(
                narrowed.Width < 1200 && narrowed.Height < 760,
                $"the window did not actually shrink — still {narrowed.Width}x{narrowed.Height}, "
                + "so anything measured below would be measured at the default size");

            var after = FindByText(window, "Servers present during data collection");

            // The Input tab body is a ScrollViewer, so "below the window bottom" is
            // not by itself a defect — the user scrolls. What must hold is that the
            // region still scrolls at all, and that the new section can be brought
            // into view. A region that stops scrolling reports extent == viewport
            // and would pass any assertion that only checks a flag.
            var scroller = after.GetVisualAncestors().OfType<ScrollViewer>().First();

            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                $"the Input tab no longer scrolls at the minimum window size "
                + $"(extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}) — "
                + "content beyond the fold would be unreachable");

            // The heading must fit the window width rather than merely exist.
            Assert.True(
                after.Bounds.Width > 0 && after.Bounds.Width <= window.Bounds.Width,
                $"the heading is {after.Bounds.Width} px wide in a {window.Bounds.Width} px window");

            // Scroll the new section into view, then ask the question the user would:
            // is it on screen now? The offset is the heading's own position inside the
            // scroll content — scrolling to the extent instead would sail past it and
            // park it thousands of pixels above the viewport.
            var contentPosition = after.TranslatePoint(new Point(0, 0), scroller);
            Assert.True(contentPosition is not null, "the heading has no position inside the scroller");
            scroller.Offset = new Vector(0, Math.Max(0, contentPosition!.Value.Y - 8));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var position = after.TranslatePoint(new Point(0, 0), window);
            Assert.True(position is not null, "the heading has no position relative to the window");
            Assert.InRange(position!.Value.X, 0, window.Bounds.Width);
            Assert.InRange(position.Value.Y, 0, window.Bounds.Height);

            // And the historical figures — the reason the section exists — must be on
            // screen too, not stranded past the content extent.
            var figures = FindByText(window, "Historical utilisation");
            var figuresPosition = figures.TranslatePoint(new Point(0, 0), window);
            Assert.True(figuresPosition is not null, "the figures have no position relative to the window");
            Assert.InRange(figuresPosition!.Value.Y, 0, window.Bounds.Height);

            // Both labels are still realised exactly once — a resize that duplicated
            // or dropped the section would otherwise pass a position check.
            var fields = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Count(t => t.Text == "Servers present during data collection"
                         || t.Text == "Historical utilisation");
            Assert.Equal(2, fields);
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock FindByText(Window window, string text) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == text)
        ?? throw new InvalidOperationException($"no TextBlock with text '{text}' in the visual tree");

    private static MainViewModel RequireMain(Window window) =>
        window.DataContext as MainViewModel
        ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static void SelectInputTab(Window window)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 1;
        window.UpdateLayout();
    }

    private static string RenderedText(Window window) => string.Join(
        "\n",
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));

    private static string Sample(string fileName) =>
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", fileName);

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