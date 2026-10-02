using System.Linq;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.3 gate — the Performance Measures section and the stability verdict
/// (D-183). Verification intent: the section exists once a run finishes and is
/// absent before one, the per-stage listing carries the 8Q.5 serial-number
/// column, the bottleneck is the stage with the **highest** ρ (not the first, not
/// the last, and not the lowest — the plausible ways to get it wrong), and the
/// three bands are decided by the thresholds the owner named.
/// </summary>
/// <remarks>
/// The band thresholds themselves are asserted without a display in
/// <see cref="VerdictClassifierThresholdTests"/>, because a classifier that can
/// only be checked by looking at a coloured label is a classifier whose boundary
/// bugs nobody finds. These tests cover the wiring: that the panel uses the
/// classifier, and that what the classifier said reaches the row.
/// </remarks>
public class PerformanceMeasuresTests
{
    private static StageMetrics Stage(string name, double rho) => new()
    {
        StageName = name,
        Rho = rho,
        ServerCount = 2,
        ArrivalRate = 0.4,
        ServiceRate = 0.3,
        PatientsServed = 40,
        AverageWaitMinutes = 2.5,
        AverageQueueLength = 1.5,
        StageUtilisation = 0.55,
    };

    private static SimulationResult Result(params StageMetrics[] stages) => new()
    {
        TotalPatientsServed = 120,
        StageMetrics = stages,
    };

    private static ResultsPanelViewModel Completed(params StageMetrics[] stages)
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(new RunOutcome(
            Result(stages), System.Array.Empty<Models.FitReport>(), System.Array.Empty<string>(),
            SimulationCoordinator.DefaultExitProbability, 0.0, null));
        return vm;
    }

    [Fact]
    public void PerformanceMeasures_SectionPresent_AfterRun()
    {
        var vm = Completed(Stage("Reception", 0.4), Stage("Screening", 0.7));

        // The section is the metrics card, so its presence is proven by the rows it
        // holds rather than by a heading string: a test that greps XAML for
        // "Performance Measures" passes while the card is bound to nothing.
        Assert.NotEmpty(vm.SystemMetrics);
        Assert.Equal(2, vm.StageRows.Count);

        // A verdict about a run that has not happened is not a verdict, so the
        // stability block stays hidden until a run exists.
        Assert.True(vm.HasStability);
        Assert.NotEmpty(vm.StabilityRows);
    }

    [Fact]
    public void PerformanceMeasures_StabilityVerdict_GreenForAllStable()
    {
        var vm = Completed(Stage("Reception", 0.30), Stage("Screening", 0.55), Stage("Doctor", 0.80));

        Assert.All(vm.StabilityRows, row => Assert.Equal("Stable", row.Verdict));
        Assert.DoesNotContain("Near capacity", vm.BottleneckText);
        Assert.DoesNotContain("Unstable", vm.BottleneckText);
    }

    [Fact]
    public void PerformanceMeasures_StabilityVerdict_AmberForBottleneck()
    {
        // One stage near capacity, the rest comfortable. The point of this test is
        // that a single amber stage colours the whole picture: the reader must not
        // have to scan three rows to discover the problem.
        var vm = Completed(Stage("Reception", 0.30), Stage("Screening", 0.93), Stage("Doctor", 0.44));

        Assert.Equal("Stable", vm.StabilityRows[0].Verdict);
        Assert.Equal("Near capacity", vm.StabilityRows[1].Verdict);
        Assert.Equal("Stable", vm.StabilityRows[2].Verdict);

        // The bottleneck line reports the worst stage, not the average.
        Assert.Contains("Screening", vm.BottleneckText);
        Assert.Contains("Near capacity", vm.BottleneckText);
    }

    [Fact]
    public void PerformanceMeasures_StabilityVerdict_RedForUnstable()
    {
        // ρ >= 1 is unreachable from the running app: the engine throws
        // UnstableSystemException and the GUI gates Start (D-128). The row must
        // still say so rather than rendering nothing, because a stage that
        // saturates in the *measurement* of a stable run is information, and an
        // empty cell would read as "fine".
        var vm = Completed(Stage("Reception", 0.50), Stage("Screening", 1.04));

        Assert.Equal("Unstable", vm.StabilityRows[1].Verdict);
        Assert.Contains("Screening", vm.BottleneckText);
        Assert.Contains("Unstable", vm.BottleneckText);
    }

    [Fact]
    public void PerformanceMeasures_Bottleneck_IsHighestRho()
    {
        // The plausible wrong answers are "the first stage", "the last stage" and
        // "the stage with the lowest utilisation". The bottleneck is the stage
        // closest to saturation, so the middle row must win here.
        var vm = Completed(Stage("Reception", 0.10), Stage("Screening", 0.88), Stage("Doctor", 0.61));

        Assert.Contains("Screening", vm.BottleneckText);
        Assert.DoesNotContain("Reception", vm.BottleneckText);
        Assert.DoesNotContain("Doctor", vm.BottleneckText);
    }

    [Fact]
    public void PerformanceMeasures_TableHasSerialNumberColumn()
    {
        var vm = Completed(Stage("Reception", 0.4), Stage("Screening", 0.7), Stage("Doctor", 0.6));

        Assert.Equal(new[] { "1", "2", "3" }, vm.StageRows.Select(r => r.SerialNumber).ToArray());
    }

    [Fact]
    public void PerformanceMeasures_StabilityAbsent_BeforeAnyRun()
    {
        var vm = new ResultsPanelViewModel();

        Assert.False(vm.HasStability);
        Assert.Empty(vm.StabilityRows);
        Assert.Equal(string.Empty, vm.BottleneckText);
        Assert.Equal(string.Empty, vm.BottleneckCaption);
    }

    [Fact]
    public void PerformanceMeasures_BottleneckTie_ResolvesToTheEarlierStage()
    {
        // A tie must resolve deterministically, or the line flickers between two
        // stage names across runs with identical inputs.
        var vm = Completed(Stage("Reception", 0.80), Stage("Screening", 0.80));

        Assert.Contains("Reception", vm.BottleneckText);
        Assert.DoesNotContain("Screening", vm.BottleneckText);
    }
}

/// <summary>
/// The band thresholds as a pure function, with no display involved
/// (Phase 8Q.3, D-183).
/// </summary>
/// <remarks>
/// The four boundaries 0.89 / 0.90 / 0.99 / 1.00 are the entire content of the
/// classifier. Each is its own named test rather than one data-driven case,
/// because a failed band boundary is a finding about a specific edge and the
/// test name should say which edge without the reader cross-referencing a table.
/// </remarks>
public class VerdictClassifierThresholdTests
{
    [Fact]
    public void VerdictClassifier_Threshold_089_Green()
    {
        // Just below the threshold. The exact value 0.89 is used rather than
        // something like 0.5 so a mis-set boundary cannot pass by accident.
        Assert.Equal(StabilityBand.Green, StabilityClassifier.Classify(0.89));
    }

    [Fact]
    public void VerdictClassifier_Threshold_090_Amber()
    {
        // The threshold belongs to amber, not green. "<= 0.9" and "< 0.9" look
        // identical in the implementation and disagree at exactly this value,
        // which is why the boundary is asserted as its own case.
        Assert.Equal(StabilityBand.Amber, StabilityClassifier.Classify(0.90));
    }

    [Fact]
    public void VerdictClassifier_Threshold_099_Amber()
    {
        Assert.Equal(StabilityBand.Amber, StabilityClassifier.Classify(0.99));
    }

    [Fact]
    public void VerdictClassifier_Threshold_100_Red()
    {
        // Arrivals exactly meeting capacity is red, not amber: the band is closed
        // on the upper side, so the last stable moment is strictly below 1.
        Assert.Equal(StabilityBand.Red, StabilityClassifier.Classify(1.00));
    }

    [Fact]
    public void VerdictClassifier_ClassifySet_EmptyIsGreenWithNoBottleneck()
    {
        var (band, bottleneck) = StabilityClassifier.ClassifySet(
            System.Array.Empty<(string, double)>());

        Assert.Equal(StabilityBand.Green, band);
        Assert.Equal(string.Empty, bottleneck);
    }
}