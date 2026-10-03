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
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8T.1 — the run caption that names the operating sessions a request
/// resolved to (FR-SIM-12, D-199).
/// </summary>
/// <remarks>
/// The defect this closes is not cosmetic: a user asking for 4 days from a
/// Saturday used to get Saturday, Monday, Tuesday and an empty Sunday, with no
/// surface anywhere saying so. The caption is the only place the resolved
/// weekday list is shown before the per-session totals table of 8T.2 arrives,
/// so these tests assert the text AND that the panel is wired to render it —
/// a formatter test alone would pass against an unwired view.
/// </remarks>
public class Phase8T1CaptionTests
{
    private static RunOutcome OutcomeWithSessions(params ClinicSession[] sessions)
        => new RunOutcome(
            new SimulationResult
            {
                TotalPatientsServed = 10,
                Sessions = sessions,
                GeneratorDays = sessions.Length,
            },
            Array.Empty<OpdSimulator.App.Models.FitReport>(),
            Array.Empty<string>(),
            0.3,
            0.1,
            null);

    private static ClinicSession Session(int ordinal, int block, DayOfWeek day)
        => new ClinicSession(ordinal, block, day);

    [Fact]
    public void SessionSummary_NamesEveryResolvedSessionWithItsWeekday()
    {
        var results = new ResultsPanelViewModel();

        results.CompleteRun(OutcomeWithSessions(
            Session(1, 0, DayOfWeek.Saturday),
            Session(2, 2, DayOfWeek.Monday),
            Session(3, 3, DayOfWeek.Tuesday),
            Session(4, 4, DayOfWeek.Wednesday)));

        Assert.True(results.HasSessionSummary);
        Assert.Equal(
            "4 operating sessions: Day 1 (Sat) · Day 2 (Mon) · Day 3 (Tue) · Day 4 (Wed)",
            results.SessionSummaryText);
    }

    [Fact]
    public void SessionSummary_SingleSessionIsNotPluralised()
    {
        var results = new ResultsPanelViewModel();

        results.CompleteRun(OutcomeWithSessions(Session(1, 0, DayOfWeek.Monday)));

        Assert.Equal("1 operating session: Day 1 (Mon)", results.SessionSummaryText);
    }

    [Fact]
    public void SessionSummary_LongHorizonIsAbbreviatedAndKeepsTheLastSession()
    {
        // A 30-day request must not wrap the header into a paragraph, but the LAST
        // session has to survive the abbreviation — it is the day whose figures
        // BacklogAtClose reports.
        var sessions = Enumerable.Range(1, 30)
            .Select(i => Session(i, i - 1, (DayOfWeek)(((int)DayOfWeek.Monday + (i - 1)) % 7)))
            .ToArray();

        var results = new ResultsPanelViewModel();
        results.CompleteRun(OutcomeWithSessions(sessions));

        Assert.StartsWith("30 operating sessions: Day 1 (Mon)", results.SessionSummaryText);
        Assert.Contains("Day 5 (Fri)", results.SessionSummaryText);
        Assert.Contains("… Day 30 (", results.SessionSummaryText);
        Assert.DoesNotContain("Day 6 (", results.SessionSummaryText);
    }

    [Fact]
    public void SessionSummary_HorizonRunHasNoCaption()
    {
        // A diagnostic trace is a minutes horizon: no sessions resolved, so there
        // is nothing to name and the caption must not leave an empty line.
        var results = new ResultsPanelViewModel();

        results.CompleteRun(OutcomeWithSessions());

        Assert.False(results.HasSessionSummary);
        Assert.Equal(string.Empty, results.SessionSummaryText);
    }

    [Fact]
    public void SessionSummary_RefusedRunHasNoCaption()
    {
        var results = new ResultsPanelViewModel();

        results.CompleteRun(new RunOutcome(null, Array.Empty<OpdSimulator.App.Models.FitReport>(),
            Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability, 0.0,
            SimulationCoordinator.MissingArrivalRateMessage));

        Assert.False(results.HasSessionSummary);
    }

    [Fact]
    public void SessionSummary_IsClearedWhenThePanelReturnsToItsEmptyState()
    {
        // The caption must not survive into the welcome state, or a later visit to
        // the panel would show a previous run's weekdays under no run at all.
        var results = new ResultsPanelViewModel();
        results.CompleteRun(OutcomeWithSessions(Session(1, 0, DayOfWeek.Saturday)));

        results.Reset();

        Assert.False(results.HasSessionSummary);
        Assert.Equal(string.Empty, results.SessionSummaryText);
    }

    [AvaloniaFact]
    public void ResultsPanel_RendersTheSessionCaptionUnderTheRunHeader()
    {
        // The formatter tests pass against an unwired view, and reading
        // vm.SessionSummaryText would pass against a stale binding (the D-194
        // failure mode). So assert the RENDERED control the user sees, found by
        // name in the visual tree of a hosted panel.
        var results = new ResultsPanelViewModel(
            new WidgetPreferences(Path.Combine(
                Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json")));
        results.StartRun();
        results.CompleteRun(OutcomeWithSessions(
            Session(1, 0, DayOfWeek.Saturday),
            Session(2, 2, DayOfWeek.Monday),
            Session(3, 3, DayOfWeek.Tuesday),
            Session(4, 4, DayOfWeek.Wednesday)));

        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = new ResultsPanel { DataContext = results },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var caption = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(t => t.Name == "SessionSummaryText");

            Assert.Equal(
                "4 operating sessions: Day 1 (Sat) · Day 2 (Mon) · Day 3 (Tue) · Day 4 (Wed)",
                caption.Text);

            // It must be inside the run header, above the widgets: a caption the
            // user has to scroll to does not answer "which days did I get?".
            Assert.True(caption.IsEffectivelyVisible, "the session caption must be on screen after a run");
            Assert.True(caption.Bounds.Height > 0, "the session caption must occupy a line");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsPanel_HidesTheSessionCaptionForAHorizonRun()
    {
        var results = new ResultsPanelViewModel(
            new WidgetPreferences(Path.Combine(
                Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json")));
        results.StartRun();
        results.CompleteRun(OutcomeWithSessions());

        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = new ResultsPanel { DataContext = results },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var caption = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(t => t.Name == "SessionSummaryText");

            // An empty caption must not leave a blank line in the header.
            Assert.False(caption.IsVisible, "a horizon run has no sessions and must show no caption line");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsPanel_SessionCaption_SavesEvidenceFrame()
    {
        // §18 evidence for the 8T.1 caption. Written under a NEW filename (D-166):
        // an existing frame is the evidence that some earlier defect existed and must
        // never be overwritten by a later capture.
        //
        // The window is sized to the app's real results column at its declared default
        // (1200 window − 380 config − 6 splitter = 814), because the question this frame
        // answers is "does the caption read correctly in the width the user gets", and a
        // frame rendered wider than the real column would answer a different question.
        var results = new ResultsPanelViewModel(
            new WidgetPreferences(Path.Combine(
                Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json")));
        results.StartRun();
        results.CompleteRun(OutcomeWithSessions(
            Session(1, 0, DayOfWeek.Saturday),
            Session(2, 2, DayOfWeek.Monday),
            Session(3, 3, DayOfWeek.Tuesday),
            Session(4, 4, DayOfWeek.Wednesday)));

        var window = new Window
        {
            Width = 814,
            Height = 760,
            Content = new ResultsPanel { DataContext = results },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            // Position, not a flag (D-169): a TextBlock with NoWrap measures its
            // desired width, so a caption too long for the column is clipped rather
            // than wrapped, and IsVisible stays true while the text runs off the edge.
            // The right edge is translated into window coordinates and compared with
            // the window's own width.
            var caption = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(t => t.Name == "SessionSummaryText");
            var origin = caption.TranslatePoint(new Point(0, 0), window);
            Assert.NotNull(origin);
            double rightEdge = origin!.Value.X + caption.Bounds.Width;
            Assert.True(rightEdge <= 814,
                $"the caption ends at {rightEdge:0} px in an 814 px column — the tail is clipped");
            Assert.True(origin.Value.Y >= 0 && origin.Value.Y < 760,
                $"the caption sits at y = {origin.Value.Y:0} in the window");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-8t1-session-caption.png");
            HeadlessScreenshot.Capture(window).Save(path);

            Assert.True(File.Exists(path), $"evidence frame missing: {path}");
            Assert.True(new FileInfo(path).Length >= 256,
                $"evidence frame suspiciously small: {new FileInfo(path).Length} bytes");
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
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
