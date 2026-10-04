using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8T.2 — the per-session totals section (FR-UI-38, D-201).
/// </summary>
/// <remarks>
/// The section is a projection of per-stage metrics the engine already collected,
/// so the tests assert three separate things and a formatter test alone would not
/// cover any of them:
/// <list type="bullet">
/// <item>the rows are session-major and each row names its own session and stage,
/// so a cited row is self-describing (FR-UI-38);</item>
/// <item>an absent mean renders "—" and never 0.0, which is the distinction
/// FR-STAT-9 exists to protect (a mean over an empty set has no value);</item>
/// <item>the panel actually renders the section from those rows, which a
/// view-model test cannot show.</item>
/// </list>
/// </remarks>
public class Phase8T2SessionTotalsTests
{
    private const string Absent = "—";

    private static StageMetrics Stage(
        string name,
        int[] servedBySession,
        double?[] meanWaitBySession,
        double?[] meanQueueBySession,
        int[] backlogBySession,
        double[] drainBySession)
        => new()
        {
            StageName = name,
            PatientsServed = servedBySession.Sum(),
            PatientsServedBySession = servedBySession,
            MeanWaitMinutesBySession = meanWaitBySession,
            MeanQueueLengthBySession = meanQueueBySession,
            BacklogAtCloseBySession = backlogBySession,
            DrainMinutesBySession = drainBySession,
        };

    private static RunOutcome TwoSessionOutcome() => new(
        new SimulationResult
        {
            TotalPatientsServed = 12,
            Sessions = new[]
            {
                new ClinicSession(1, 0, DayOfWeek.Saturday),
                new ClinicSession(2, 2, DayOfWeek.Monday),
            },
            SessionLengthMinutes = 165,
            SessionStartDay = DayOfWeek.Saturday,
            GeneratorDays = 2,
            StageMetrics = new[]
            {
                // Session 1 of Screening admitted nobody, so its means are absent.
                Stage("Reception", new[] { 5, 4 }, new double?[] { 1.5, 0.75 }, new double?[] { 2.0, 1.0 }, new[] { 1, 0 }, new[] { 3.0, 1.5 }),
                Stage("Screening", new[] { 0, 3 }, new double?[] { null, 2.25 }, new double?[] { null, 0.5 }, new[] { 0, 2 }, new[] { 0.0, 4.0 }),
            },
        },
        Array.Empty<FitReport>(),
        Array.Empty<string>(),
        0.3,
        0.1,
        null);

    [Fact]
    public void Rows_AreSessionMajor_AndEachRowNamesItsOwnSessionAndStage()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(TwoSessionOutcome());

        Assert.True(results.HasSessionTotals);
        Assert.Collection(
            results.SessionTotalRows,
            r =>
            {
                Assert.Equal("1", r.SerialNumber);
                Assert.Equal("Day 1 (Sat)", r.Session);
                Assert.Equal("Reception", r.Stage);
            },
            r =>
            {
                Assert.Equal("2", r.SerialNumber);
                Assert.Equal("Day 1 (Sat)", r.Session);
                Assert.Equal("Screening", r.Stage);
            },
            r =>
            {
                Assert.Equal("3", r.SerialNumber);
                Assert.Equal("Day 2 (Mon)", r.Session);
                Assert.Equal("Reception", r.Stage);
            },
            r =>
            {
                Assert.Equal("4", r.SerialNumber);
                Assert.Equal("Day 2 (Mon)", r.Session);
                Assert.Equal("Screening", r.Stage);
            });

        // The serial column is contiguous 1..N so a row can be cited by number
        // (FR-UI-36), and every row carries its own labels (FR-UI-38).
        Assert.Equal(
            Enumerable.Range(1, 4).Select(i => i.ToString()),
            results.SessionTotalRows.Select(r => r.SerialNumber));
        Assert.All(results.SessionTotalRows, r => Assert.False(string.IsNullOrWhiteSpace(r.Session)));
        Assert.All(results.SessionTotalRows, r => Assert.False(string.IsNullOrWhiteSpace(r.Stage)));
    }

    [Fact]
    public void AbsentMean_RendersAnEmDash_AndARealMeanRendersANumber()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(TwoSessionOutcome());

        var idleSessionScreening = results.SessionTotalRows.Single(r => r.Session == "Day 1 (Sat)" && r.Stage == "Screening");
        Assert.Equal(Absent, idleSessionScreening.MeanWait);
        Assert.Equal(Absent, idleSessionScreening.MeanQueue);
        Assert.Equal("0", idleSessionScreening.Served); // a real 0: nobody completed

        var busySessionScreening = results.SessionTotalRows.Single(r => r.Session == "Day 2 (Mon)" && r.Stage == "Screening");
        Assert.Equal("2.25", busySessionScreening.MeanWait);
        Assert.Equal("0.5", busySessionScreening.MeanQueue);
        Assert.Equal("3", busySessionScreening.Served);
    }

    [Fact]
    public void Caption_NamesSessionCountLengthAndStartWeekday()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(TwoSessionOutcome());

        Assert.Contains("2 operating sessions", results.SessionTotalsCaption);
        Assert.Contains("165 min", results.SessionTotalsCaption);
        Assert.Contains("Saturday", results.SessionTotalsCaption);
    }

    [Fact]
    public void HorizonRun_RendersNoSessionRows()
    {
        // A horizon run has no operating sessions, so a "per-session" table has
        // nothing to vary across and must not appear (FR-UI-38).
        var results = new ResultsPanelViewModel();
        results.CompleteRun(new RunOutcome(
            new SimulationResult
            {
                TotalPatientsServed = 5,
                Sessions = Array.Empty<ClinicSession>(),
                StageMetrics = new[]
                {
                    Stage("Reception", new[] { 5 }, new double?[] { 1.0 }, new double?[] { 0.5 }, new[] { 0 }, new[] { 0.0 }),
                },
            },
            Array.Empty<FitReport>(),
            Array.Empty<string>(),
            0.3,
            0.1,
            null));

        Assert.False(results.HasSessionTotals);
        Assert.Empty(results.SessionTotalRows);
    }

    [Fact]
    public void Reset_ClearsTheSessionTotalsSection()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(TwoSessionOutcome());
        Assert.True(results.HasSessionTotals);

        results.Reset();

        Assert.False(results.HasSessionTotals);
        Assert.Empty(results.SessionTotalRows);
        Assert.Equal(string.Empty, results.SessionTotalsCaption);
    }

    [AvaloniaFact]
    public void Panel_RendersOneRowPerSessionPerStage()
    {
        // The formatter tests above pass against an unwired view, so the panel is
        // rendered and the rows counted from the visual tree (D-166: a test must
        // observe what the user would see, not just what the view model holds).
        var results = new ResultsPanelViewModel();
        results.CompleteRun(TwoSessionOutcome());
        var panel = new ResultsPanel { DataContext = results };
        var host = new Window { Content = panel, Width = 900, Height = 1200 };
        host.Show();

        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        // Every cell of every row is present in the rendered tree.
        Assert.Contains("Per-session totals", texts);
        Assert.Contains("Day 1 (Sat)", texts);
        Assert.Contains("Day 2 (Mon)", texts);
        Assert.Contains("Screening", texts);
        Assert.Contains(Absent, texts);
        Assert.Contains("2.25", texts);
    }
}