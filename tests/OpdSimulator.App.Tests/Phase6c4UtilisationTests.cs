using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.4 gate (feat/milestone-6c-input-analysis-charts) — the per-server
/// utilisation widget (FR-STAT-7). Verification intent: the service turns a
/// finished run into exactly one bar per server in stage order; a server is
/// flagged only when it deviates from its stage mean by more than 0.15 (that
/// is when "imbalance" must be surfaced to the user); the caption names the
/// threshold; a refused run yields an empty card, and the widget is a results
/// widget that defaults on and participates in the FR-UI-14 visibility strip
/// (D-121 — run-derived figures belong on the Results tab, not Input Analysis).
/// </summary>
public class Phase6c4UtilisationTests
{
    private static SimulationResult TwoStageResult()
    {
        return new SimulationResult
        {
            TotalPatientsServed = 100,
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Screening",
                    ServerCount = 3,
                    StageUtilisation = 0.5,
                    PerServerUtilisation = new[] { 0.82, 0.50, 0.18 },
                },
                new StageMetrics
                {
                    StageName = "Doctor",
                    ServerCount = 2,
                    StageUtilisation = 0.42,
                    PerServerUtilisation = new[] { 0.42, 0.42 },
                },
            },
        };
    }

    [Fact]
    public void UtilisationChart_OneBarPerServerAcrossAllStages()
    {
        var chart = UtilisationChartService.Build(TwoStageResult());

        Assert.True(chart.HasSeries, "a finished run must contribute bars");
        // 3 Screening + 2 Doctor servers = 5 bars, in stage order, server 1..n each.
        Assert.Equal(5, chart.Bars.Count);
        Assert.Equal(
            new[] { "Screening", "Screening", "Screening", "Doctor", "Doctor" },
            chart.Bars.Select(b => b.StageName).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 1, 2 }, chart.Bars.Select(b => b.ServerNumber).ToArray());
        Assert.Equal(new[] { 0.82, 0.50, 0.18, 0.42, 0.42 }, chart.Bars.Select(b => b.Utilisation));

        // One reference line per stage, spanning exactly that stage's bars.
        Assert.Equal(2, chart.ReferenceLines.Count);
        Assert.Equal(0, chart.ReferenceLines[0].FirstBarIndex);
        Assert.Equal(2, chart.ReferenceLines[0].LastBarIndex);
        Assert.Equal(3, chart.ReferenceLines[1].FirstBarIndex);
        Assert.Equal(4, chart.ReferenceLines[1].LastBarIndex);
        Assert.Equal(0.5, chart.ReferenceLines[0].Average);
    }

    [Fact]
    public void UtilisationChart_ImbalanceFlag_HighlightsOutlierServer()
    {
        var chart = UtilisationChartService.Build(TwoStageResult());
        var screening = chart.Bars.Take(3).ToList();

        // Stage mean 0.50; server 1 is 0.32 busier, server 2 is exactly at the
        // mean, server 3 is 0.32 idler. Only the two deviators are flagged.
        Assert.True(screening[0].IsOutlier, "0.82 vs 0.50 mean (>0.15) must flag");
        Assert.Equal(0.32, screening[0].DeltaFromAverage, precision: 10);
        Assert.False(screening[1].IsOutlier, "exactly at the mean is not an imbalance");
        Assert.Equal(0.0, screening[1].DeltaFromAverage, precision: 10);
        Assert.True(screening[2].IsOutlier, "0.18 vs 0.50 mean (>0.15) must flag");
        Assert.Equal(-0.32, screening[2].DeltaFromAverage, precision: 10);

        // Doctor stage is perfectly balanced → nothing flagged there.
        Assert.All(chart.Bars.Skip(3), b => Assert.False(b.IsOutlier));
    }

    [Fact]
    public void UtilisationChart_Caption_MentionsImbalanceThreshold()
    {
        Assert.Contains("imbalance", UtilisationChartService.Caption);
        Assert.Contains("0.15", UtilisationChartService.Caption);
    }

    [Fact]
    public void UtilisationChart_NullResult_YieldsEmptyCard()
    {
        var empty = UtilisationChartService.Build(null);

        Assert.False(empty.HasSeries);
        Assert.Empty(empty.Bars);
        Assert.Empty(empty.ReferenceLines);

        // Refused run → the results panel keeps the empty state visible.
        var results = new ResultsPanelViewModel();
        results.StartRun();
        results.CompleteRun(new RunOutcome(null, Array.Empty<Models.FitReport>(),
            Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability,
            SimulationCoordinator.MissingArrivalRateMessage));

        Assert.False(results.HasUtilisationChart);
        Assert.True(results.ShowUtilisationEmptyState);
    }

    [Fact]
    public void UtilisationWidget_DefaultsOnAndTogglesIndependently()
    {
        var results = new ResultsPanelViewModel();

        // Default-on like every FR-UI-14 widget, and its key is in the list.
        Assert.True(results.ShowUtilisation);
        Assert.Contains("utilisation", results.VisibleWidgets);

        results.ToggleWidget("utilisation");

        Assert.False(results.ShowUtilisation);
        Assert.DoesNotContain("utilisation", results.VisibleWidgets);

        // Toggling an unrelated widget must not re-enable utilisation.
        results.ToggleWidget("trace");
        Assert.False(results.ShowUtilisation);
    }
}