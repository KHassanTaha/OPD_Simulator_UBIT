using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8B — simulation-output chi-square verification. The Results panel's
/// "Simulation verification" widget chi-square tests the samples the engine
/// itself generated against the configured distribution (Banks / Law &amp;
/// Kelton model verification). These tests cover the pure verification service
/// (the <c>VerifyAll_*</c> cases, including the 8K configured-spec contract) and the widget lifecycle (empty before a
/// run, populated on completion, cleared by Clear All) plus the FR-UI-14 widget
/// picker count now that the widget is the seventh.
/// </summary>
public class Phase8BVerificationTests
{
    private const double Alpha = 0.05;

    /// <summary>Builds <paramref name="count"/> Exponential(rate) variates through the Core inverse-CDF sampler.</summary>
    private static IReadOnlyList<double> ExponentialSamples(int count, double rate, int seed)
    {
        var sampler = new ExponentialSampler(new SeededRandomSource(seed));
        return Enumerable.Range(0, count).Select(_ => sampler.Sample(rate)).ToList();
    }

    /// <summary>
    /// The configured Exponential spec whose mean matches a sampler rate: mean = 1/rate.
    /// Verification now tests the configured spec, so a test that wants a stage to PASS
    /// must configure the spec the samples were actually drawn from.
    /// </summary>
    private static DistributionSpec Exp(double rate) => new(DistributionFamily.Exponential, Mean: 1.0 / rate);

    /// <summary>Builds <paramref name="count"/> genuine Normal(mean, stdDev) variates through the Core sampler.</summary>
    private static IReadOnlyList<double> NormalSamples(int count, double mean, double stdDev, int seed)
    {
        var sampler = new NormalSampler(new SeededRandomSource(seed), mean, stdDev);
        return Enumerable.Range(0, count).Select(_ => sampler.NextSample()).ToList();
    }

    /// <summary>
    /// Verifies one stage against <paramref name="spec"/> and returns the p-value, or
    /// null when no verdict could be computed (a note was produced instead).
    /// </summary>
    /// <remarks>
    /// WHY THESE TESTS USE SEVERAL SEEDS. Under a correct specification a chi-square test
    /// rejects 5% of the time BY CONSTRUCTION — that is what α means. A single sample can
    /// therefore legitimately return p = 0.036 for a perfectly matching distribution, and
    /// asserting p &gt; 0.05 once is a coin flip dressed up as a requirement. Averaging
    /// p over independent seeds (whose mean is far more stable than any one draw) and
    /// demanding a catastrophic value for the mismatched case is what makes these tests
    /// measure the behaviour instead of the luck of a seed.
    /// </remarks>
    private static double? PValueAgainst(
        DistributionSpec spec,
        IReadOnlyList<double> samples,
        double alpha = Alpha)
    {
        var stages = new[] { new StageMetrics { StageName = "Reception" } };
        var result = ResultWith(
            Array.Empty<double>(), new IReadOnlyList<double>[] { samples }, stages);

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(
                result, "Exponential", new[] { spec }, alpha),
            r => r.Label == "Reception service");

        return report.ChiSquare?.PValue;
    }

    private static SimulationResult ResultWith(
        IReadOnlyList<double> interArrivals,
        IReadOnlyList<IReadOnlyList<double>>? serviceByStage = null,
        IReadOnlyList<StageMetrics>? stages = null)
        => new()
        {
            GeneratedInterArrivalSamples = interArrivals,
            GeneratedServiceSamplesByStage = serviceByStage ?? Array.Empty<IReadOnlyList<double>>(),
            StageMetrics = stages ?? Array.Empty<StageMetrics>(),
        };

    [Fact]
    public void VerifyAll_ExponentialSamples_PassesChiSquare()
    {
        // Genuine Exponential(λ=0.5) output must pass its own goodness-of-fit —
        // the whole point of output-side verification. If the engine or the
        // service accidentally changed the family, p would collapse and this
        // test would fail.
        var result = ResultWith(ExponentialSamples(1000, 0.5, 42));

        var reports = SimulationVerificationService.VerifyAll(result, "Exponential", Array.Empty<DistributionSpec>(), Alpha);

        var report = Assert.Single(reports);
        Assert.Equal(1000, report.SampleCount);
        Assert.NotNull(report.ChiSquare);
        Assert.True(
            report.ChiSquare!.PValue > Alpha,
            $"genuine Exponential output must clear α={Alpha}; observed p={report.ChiSquare.PValue}");
        Assert.True(report.Histogram.HasSeries);
        Assert.Null(report.Note);
    }

    [Fact]
    public void VerifyAll_DeterministicFamily_SkipsChiSquare()
    {
        // A constant stream has no distribution to fit, so verification must
        // skip the test and explain why rather than inventing a verdict.
        var result = ResultWith(Enumerable.Repeat(2.0, 50).ToList());

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "Deterministic", Array.Empty<DistributionSpec>(), Alpha));

        Assert.Null(report.ChiSquare);
        Assert.NotNull(report.Note);
        Assert.Contains("Deterministic", report.Note!);
    }

    [Fact]
    public void VerifyAll_ReturnsOneReportPerSeries()
    {
        // A three-stage run must verify four series: inter-arrival plus one per
        // stage, in stage order — the report count drives the widget's card count.
        var stages = new[]
        {
            new StageMetrics { StageName = "Reception" },
            new StageMetrics { StageName = "Screening" },
            new StageMetrics { StageName = "Doctor" },
        };
        var services = new IReadOnlyList<double>[]
        {
            ExponentialSamples(500, 0.8, 1),
            ExponentialSamples(500, 0.6, 2),
            ExponentialSamples(500, 0.4, 3),
        };
        var result = ResultWith(ExponentialSamples(500, 0.4, 9), services, stages);

        var reports = SimulationVerificationService.VerifyAll(
            result,
            "Exponential",
            new[] { Exp(0.8), Exp(0.6), Exp(0.4) },
            Alpha);

        Assert.Equal(4, reports.Count);
        Assert.Equal(
            new[] { "Inter-arrival", "Reception service", "Screening service", "Doctor service" },
            reports.Select(r => r.Label));
    }

    [Fact]
    public void VerifyAll_InsufficientSamples_ReturnsNullChiSquare()
    {
        // Fewer than two samples cannot fill a chi-square bin; honesty over a
        // fabricated verdict (AGENTS §12 — fail loud, never silently skip).
        var result = ResultWith(new[] { 1.0 });

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "Exponential", Array.Empty<DistributionSpec>(), Alpha));

        Assert.Null(report.ChiSquare);
        Assert.NotNull(report.Note);
        Assert.Contains("Insufficient", report.Note!);
    }

    /// <summary>
    /// THE 8K REGRESSION TEST, and the reason it is written with the SAME family on both
    /// sides. The pre-8K path refitted the configured family to the engine's output, so
    /// it re-estimated the very parameters under test and then declared the result a good
    /// fit. Only a scenario that separates the configured parameters from the generated
    /// ones can tell the two implementations apart:
    /// <list type="bullet">
    /// <item>configured spec: Normal(mean 20, sd 1)</item>
    /// <item>engine output:   genuine Normal(mean 2, sd 1)</item>
    /// </list>
    /// A refit re-estimates mean ≈ 2 from the samples and PASSES. The configured spec puts
    /// essentially all of its mass near 20, so the output must be reported as NOT matching
    /// what was configured — either a rejected chi-square or an honest "cannot be
    /// computed" note. If this test ever sees p &gt; α, a refit is back in the stage path.
    /// </summary>
    [Fact]
    public void VerifyAll_SameFamilyButDifferentParameters_RejectsTheOutput()
    {
        // Configured spec: Normal(mean 20, sd 1). Engine output: genuine Normal(mean 2, sd 1).
        // The refit would re-estimate mean ≈ 2 and pass with p spread uniformly over (0,1).
        // The configured spec leaves the output in a tail the spec says is empty, so the
        // p-value must be catastrophically small — not merely below α — for every seed.
        var spec = new DistributionSpec(DistributionFamily.Normal, Mean: 20.0, StdDev: 1.0);

        foreach (int seed in new[] { 11, 12, 13, 14, 15 })
        {
            double? p = PValueAgainst(spec, NormalSamples(1000, 2.0, 1.0, seed));

            if (p is null)
            {
                // No verdict computable (the expected bin counts collapse) is also a
                // rejection: the output does not match what was configured.
                continue;
            }

            Assert.True(
                p < 0.001,
                $"output centred on 2 must not pass a spec centred on 20 (that is a refit); seed {seed} gave p={p}");
        }
    }

    /// <summary>
    /// The converse: a stage configured to match its output must still PASS. Without this
    /// the suite would be satisfiable by simply refusing every stage, which is why the
    /// 8K change needs both directions — a matching spec is not made to fail by testing
    /// the configuration instead of a refit.
    /// </summary>
    [Fact]
    public void VerifyAll_StageConfiguredToMatchItsOutput_Passes()
    {
        // The converse of the regression test, and the guard that stops 8K being
        // "satisfied" by refusing every stage: a spec that genuinely describes the output
        // must not be systematically rejected. The mean p over five seeds must clear α;
        // the mean of several uniform draws is stable where a single draw is a coin flip.
        var spec = new DistributionSpec(DistributionFamily.Normal, Mean: 2.0, StdDev: 1.0);
        var pValues = new[] { 11, 12, 13, 14, 15 }
            .Select(seed => PValueAgainst(spec, NormalSamples(1000, 2.0, 1.0, seed)))
            .ToList();

        Assert.DoesNotContain(null, pValues);
        double mean = pValues.Average(v => v!.Value);
        Assert.True(
            mean > Alpha,
            $"a spec matching its own output must not be systematically rejected; mean p={mean:F4} over {pValues.Count} seeds");
    }

    /// <summary>
    /// Proves the spec reaches the RIGHT stage. Both stages emit the SAME family of
    /// samples; only the configured mean differs. Stage 0 matches and must pass, stage 1 is
    /// configured at 20 and must not — a service that ignored the list order, reused one
    /// stage's spec for every stage, or fell back to a default family cannot produce this
    /// pass/fail pattern.
    /// </summary>
    [Fact]
    public void VerifyAll_MixedStageParameters_VerifyEachStageAgainstItsOwnSpec()
    {
        // Both stages emit the same family of samples; only the configured mean differs.
        // Stage 0 is configured at 2 (matches), stage 1 at 20 (does not). A service that
        // ignored the list order, reused one stage's spec for all stages, or fell back to
        // a default family would give the SAME verdict for both — which is what this
        // asserts never happens, for any seed.
        var matching = new DistributionSpec(DistributionFamily.Normal, Mean: 2.0, StdDev: 1.0);
        var mismatched = new DistributionSpec(DistributionFamily.Normal, Mean: 20.0, StdDev: 1.0);

        foreach (int seed in new[] { 21, 22, 23, 24, 25 })
        {
            var stages = new[]
            {
                new StageMetrics { StageName = "Reception" },
                new StageMetrics { StageName = "Screening" },
            };
            var services = new IReadOnlyList<double>[]
            {
                NormalSamples(1000, 2.0, 1.0, seed),
                NormalSamples(1000, 2.0, 1.0, seed),
            };
            var result = ResultWith(Array.Empty<double>(), services, stages);

            var reports = SimulationVerificationService.VerifyAll(
                result, "Exponential", new[] { matching, mismatched }, Alpha);

            var good = Assert.Single(reports, r => r.Label == "Reception service");
            var bad = Assert.Single(reports, r => r.Label == "Screening service");

            // Identical input samples, two different specs, two different verdicts.
            // This is the whole contract in one assertion.
            Assert.NotNull(good.ChiSquare);
            Assert.True(
                good.ChiSquare!.PValue > 0.001,
                $"the stage configured to match must not be rejected catastrophically; seed {seed} gave p={good.ChiSquare.PValue}");

            if (bad.ChiSquare is not null)
            {
                Assert.True(
                    bad.ChiSquare.PValue < 0.001,
                    $"the stage configured at 20 must be rejected; seed {seed} gave p={bad.ChiSquare.PValue}");
            }
        }
    }

    [Fact]
    public void VerifyAll_DeterministicStageSpec_KeepsTheD128Note()
    {
        var stages = new[] { new StageMetrics { StageName = "Reception" } };
        var services = new IReadOnlyList<double>[] { Enumerable.Repeat(2.0, 50).ToList() };
        var result = ResultWith(Array.Empty<double>(), services, stages);

        var stage = Assert.Single(
            SimulationVerificationService.VerifyAll(
                result,
                "Exponential",
                new[] { new DistributionSpec(DistributionFamily.Deterministic, Mean: 2.0) },
                Alpha),
            r => r.Label == "Reception service");

        Assert.Null(stage.ChiSquare);
        Assert.Equal("Deterministic — chi-square not applicable.", stage.Note);
        Assert.Equal("Deterministic", stage.IntendedFamily);
    }

    /// <summary>
    /// A stage with no spec is a configuration gap. The pre-8K code silently substituted
    /// "Exponential", which is how a misconfigured stage could show a green chi-square.
    /// It must now say so, and must not fabricate a verdict.
    /// </summary>
    [Fact]
    public void VerifyAll_StageWithNoConfiguredSpec_ReportsNotConfiguredInsteadOfDefaulting()
    {
        var stages = new[] { new StageMetrics { StageName = "Reception" } };
        var services = new IReadOnlyList<double>[] { ExponentialSamples(1000, 0.5, 31) };
        var result = ResultWith(Array.Empty<double>(), services, stages);

        var stage = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "Exponential", Array.Empty<DistributionSpec>(), Alpha),
            r => r.Label == "Reception service");

        Assert.Null(stage.ChiSquare);
        Assert.Equal("not configured", stage.IntendedFamily);
        Assert.Contains("No service distribution was configured", stage.Note!);
    }

    /// <summary>
    /// A spec that cannot produce a distribution must degrade to a reason on the card,
    /// never take the results panel down. This is the bin-count crash a narrower-than-
    /// output spec can trigger inside ChiSquareTest.
    /// </summary>
    [Fact]
    public void VerifyAll_UnusableSpec_YieldsANoteAndLeavesTheOtherCardsIntact()
    {
        var stages = new[]
        {
            new StageMetrics { StageName = "Reception" },
            new StageMetrics { StageName = "Screening" },
        };
        var services = new IReadOnlyList<double>[]
        {
            ExponentialSamples(300, 0.5, 41),
            ExponentialSamples(300, 0.5, 42),
        };
        var result = ResultWith(Array.Empty<double>(), services, stages);

        // A Normal with no sigma: unusable, and must not throw.
        var reports = SimulationVerificationService.VerifyAll(
            result,
            "Exponential",
            new[]
            {
                new DistributionSpec(DistributionFamily.Normal, Mean: 2.0),
                Exp(0.5),
            },
            Alpha);

        // Inter-arrival card plus one card per stage: a bad spec must not remove cards.
        Assert.Equal(3, reports.Count);
        var broken = Assert.Single(reports, r => r.Label == "Reception service");
        Assert.Null(broken.ChiSquare);
        Assert.Contains("standard deviation", broken.Note!);

        // The healthy stage must still be verified: one bad card cannot blank the widget.
        var healthy = Assert.Single(reports, r => r.Label == "Screening service");
        Assert.NotNull(healthy.ChiSquare);
    }

    [AvaloniaFact]
    public void VerificationWidget_EmptyState_BeforeRun()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;

            Assert.True(main.SimulationVerification.IsEmpty);
            Assert.Equal("Run a simulation to verify its output.", main.SimulationVerification.EmptyMessage);
            Assert.Empty(main.SimulationVerification.Charts);

            // Once a run starts, the Results stack replaces the welcome card and
            // the widget renders its empty state.
            main.Results.StartRun();
            window.UpdateLayout();

            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var empty = panel.GetVisualDescendants().OfType<TextBlock>()
                .SingleOrDefault(t => t.Text == main.SimulationVerification.EmptyMessage);
            Assert.NotNull(empty);
            Assert.True(empty!.IsVisible, "the verification widget must show its empty state before data exists");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task VerificationWidget_PopulatesOnRunCompletion()
    {
        var window = new MainWindow();
        window.Width = 1100;
        window.Height = 2000;
        window.Show();

        var main = (MainViewModel)window.DataContext!;
        var originalVisible = main.Results.VisibleWidgets.ToArray();
        try
        {
            // Gate configuration (Phase 8B): manual three-stage clinic
            // Reception 1 / Screening 2 / Doctor 3, λ=0.5/min, μ=0.8/0.6/0.4,
            // p_exit=0.4, seed 42.
            main.Config.SourceMode = OpdSimulator.App.Models.DataSourceMode.EnterManually;
            main.Config.ManualLambda.Value = "0.5";
            // EnterManually sources μ from the per-stage field, not the comma
            // list (AGENTS §19.4).
            main.Config.StageRows[0].MuValue = "0.8";
            main.Config.StageRows[1].MuValue = "0.6";
            main.Config.StageRows[2].MuValue = "0.4";
            main.Config.StageRows[0].Servers.Value = "1";
            main.Config.StageRows[1].Servers.Value = "2";
            main.Config.StageRows[2].Servers.Value = "3";
            main.Config.PExit.Value = "0.4";
            Assert.True(main.Config.StartCalculationCommand.CanExecute(null), main.Config.StartBlockedMessage);

            // Drive the real MainViewModel run path (Start button → background
            // run → posted completion) so this proves the completion wiring, not
            // just the service.
            main.Config.StartCalculationCommand.Execute(null);
            await WaitUntilAsync(() => !main.SimulationVerification.IsEmpty, TimeSpan.FromSeconds(15));

            Assert.False(main.SimulationVerification.IsEmpty, "run completion must populate the verification widget");
            Assert.Equal(4, main.SimulationVerification.Charts.Count);

            // The gate's high arrival rate must leave every series with enough
            // service samples to fit: output-side chi-square is only meaningful
            // when each card carries real content.
            Assert.All(main.SimulationVerification.Charts, c => Assert.NotNull(c.ChartContent));
            Assert.All(
                main.SimulationVerification.Charts,
                c => Assert.False(string.IsNullOrWhiteSpace(c.Caption), "every verification card must caption its verdict"));

            main.Results.ShowSimulationVerification = true;
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            Assert.Contains(
                panel.GetVisualDescendants().OfType<TextBlock>(),
                t => t.Text == "Simulation verification");

            // Phase 8B gate evidence (D-089 headless): isolate the verification
            // widget, then save the frame the gate references.
            main.Results.ShowMetrics = false;
            main.Results.ShowChiSquare = false;
            main.Results.ShowTrace = false;
            main.Results.ShowUtilisation = false;
            main.Results.ShowQueueLength = false;
            main.Results.ShowWaitHistogram = false;
            window.UpdateLayout();

            var frame = HeadlessScreenshot.Capture(window);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8b-verification.png");
            frame.Save(shotPath);
            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "verification frame missing or suspiciously small");
        }
        finally
        {
            RestoreVisibility(main.Results, originalVisible);
            window.Close();
        }
    }

    private static void RestoreVisibility(ResultsPanelViewModel results, IReadOnlyList<string> originalVisible)
    {
        results.ShowMetrics = originalVisible.Contains("metrics");
        results.ShowChiSquare = originalVisible.Contains("chiSquare");
        results.ShowTrace = originalVisible.Contains("trace");
        results.ShowUtilisation = originalVisible.Contains("utilisation");
        results.ShowQueueLength = originalVisible.Contains("queueLength");
        results.ShowWaitHistogram = originalVisible.Contains("waitHistogram");
        results.ShowSimulationVerification = originalVisible.Contains("simulationVerification");
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

    [AvaloniaFact]
    public void VerificationWidget_ClearedByClearAll()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;
            var stages = new[]
            {
                new StageMetrics { StageName = "Reception" },
                new StageMetrics { StageName = "Screening" },
                new StageMetrics { StageName = "Doctor" },
            };
            var services = new IReadOnlyList<double>[]
            {
                ExponentialSamples(300, 0.8, 1),
                ExponentialSamples(300, 0.6, 2),
                ExponentialSamples(300, 0.4, 3),
            };
            main.SimulationVerification.Apply(
                ResultWith(ExponentialSamples(300, 0.4, 9), services, stages),
                "Exponential",
                new[] { Exp(0.8), Exp(0.6), Exp(0.4) },
                Alpha);
            Assert.False(main.SimulationVerification.IsEmpty);
            Assert.Equal(4, main.SimulationVerification.Charts.Count);

            main.ResetAll();

            Assert.True(main.SimulationVerification.IsEmpty, "Clear All must return the widget to its empty state");
            Assert.Empty(main.SimulationVerification.Charts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void WidgetSelector_ListsExactlyEightWidgets_After8C()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var picker = panel.GetVisualDescendants().OfType<Border>()
                .Single(b => string.Equals(b.Name, "WidgetPicker", StringComparison.Ordinal));
            picker.IsVisible = true;
            window.UpdateLayout();

            string[] contents = picker.GetVisualDescendants().OfType<CheckBox>()
                .Select(c => c.Content?.ToString() ?? string.Empty)
                .ToArray();

            // 7→8 in Phase 8C: analytical validation widget added.
            Assert.Equal(8, contents.Length);
            Assert.Contains("Simulation verification", contents);
            Assert.Contains("Analytical validation", contents);
            Assert.Equal(8, new ResultsPanelViewModel().VisibleWidgets.Count);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}