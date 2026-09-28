using System;
using System.Linq;
using System.Reflection;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Parameters;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8J gate — per-stage service families flow from the App to Core.
/// </summary>
/// <remarks>
/// <para>
/// 8I gave Core the ability to carry an explicit service distribution per stage
/// (<c>StageSpec</c>) but nothing produced one: the App passed a bare rate, so the
/// feature was latent and every run silently sampled Exponential. These tests pin the
/// three things that had to become true for it to be real — the App supplies a family
/// per stage, the rate reaches the spec without being inverted, and a caller who
/// contradicts the two is stopped rather than trusted.
/// </para>
/// <para>
/// The business risk being defended against is specific: <c>StageSpec</c> computes its
/// stability check (rho) from <c>ServiceRate</c> but samples from
/// <c>ServiceDistribution</c>. If those two disagree, the engine validates one model
/// and simulates another and reports metrics that look plausible. Every test below is
/// written to fail the day that separation is broken.
/// </para>
/// </remarks>
public sealed class Phase8JTests
{
    /// <summary>
    /// The single service-family string must no longer be a stored input. If it were
    /// still a primary-constructor parameter, it would compete with
    /// <c>ServiceFamilies</c> as a second source of truth — and the two could disagree,
    /// leaving the question "which family does stage 2 use?" unanswerable.
    /// </summary>
    /// <remarks>
    /// Asserted by reflection because the requirement is about a shape that no
    /// compiling reference can state: you cannot write code that names a member which is
    /// supposed to be gone. The name survives as a derived, setter-less display shim
    /// (see <c>SimulationParameters.ServiceDistribution</c>) because
    /// <c>MainViewModel</c> reads it for stage labels and is outside this phase's file
    /// set; Phase 8K removes those call sites. So the invariant pinned here is the
    /// meaningful one — it is not a constructor parameter, and nothing can set it.
    /// </remarks>
    [Fact]
    public void SimulationParameters_NoLongerHasServiceDistribution()
    {
        var ctor = typeof(SimulationParameters)
            .GetConstructors()
            .Single(c => c.GetParameters().Length > 0);

        Assert.DoesNotContain(
            ctor.GetParameters(),
            p => p.Name == "ServiceDistribution");

        var property = typeof(SimulationParameters).GetProperty("ServiceDistribution");
        Assert.NotNull(property);
        Assert.Null(property!.SetMethod);
        Assert.Empty(typeof(SimulationParameters)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is not null)
            .Select(p => p.Name)
            .Intersect(["ServiceDistribution"]));
    }

    /// <summary>
    /// An unset record must say "no families" rather than inventing one, so that a
    /// misconfigured record is rejected by the coordinator instead of being quietly run
    /// as an all-Exponential network the user never asked for.
    /// </summary>
    [Fact]
    public void ServiceFamilies_DefaultToEmpty()
    {
        var parameters = NewParameters();

        Assert.Empty(parameters.ServiceFamilies);
        Assert.Empty(parameters.ServiceRates);
        Assert.Equal("Exponential", parameters.ServiceDistribution);
    }

    /// <summary>
    /// μ must reach Core as the number the user typed. Inverting it into a mean and
    /// re-deriving the rate would multiply the conversion error, and 1/(1/0.8) is not
    /// 0.8 bit-for-bit — which would silently perturb every metric.
    /// </summary>
    [Fact]
    public void ServiceRates_AreNotInverted()
    {
        var config = ManualRun(muPerStage: [0.8, 0.6, 0.4]);

        var parameters = config.TryBuildRunParameters()!;

        Assert.Equal(new double?[] { 0.8, 0.6, 0.4 }, parameters.ServiceRates);
        Assert.Equal(parameters.ManualServiceRates, parameters.ServiceRates);

        foreach (var rate in parameters.ServiceRates)
        {
            // A round trip through the mean is deliberately not asserted to be exact;
            // what must hold is that the rate itself was never touched.
            Assert.NotEqual(1.0 / rate!.Value, rate.Value);
        }
    }

    /// <summary>
    /// A stage that names a distribution is no longer forced into M/M/c: the spec it was
    /// given is the spec that is used, and the rate is still the rate.
    /// </summary>
    [Fact]
    public void StageSpec_AcceptsPerStageServiceSpec()
    {
        var spec = new DistributionSpec(DistributionFamily.Gamma, Mean: 4.0, StdDev: 1.0);

        var stage = new StageSpec("Reception", 2, 0.25, spec);

        Assert.Same(spec, stage.ServiceDistribution);
        Assert.Same(spec, stage.EffectiveServiceDistribution);
        Assert.Equal(0.25, stage.ServiceRate);
    }

    /// <summary>
    /// A caller who supplies a rate and a spec describing a different rate must be
    /// stopped in a Debug build, because the resulting metrics are plausible and
    /// wrong. In Release the assertion is compiled out by design (it is a developer
    /// safety net, not a production guard), so this test pins that the construction
    /// still succeeds there and that the mismatch survives to be visible.
    /// </summary>
#if DEBUG
    [Fact]
    public void StageSpec_RejectsSpecRateMismatchInDebug()
    {
        // Mean 10 implies μ = 0.1, but the rate says 0.5: five times apart.
        var mismatched = new DistributionSpec(DistributionFamily.Exponential, Mean: 10.0);

        var ex = Assert.Throws<InvalidOperationException>(
            () => new StageSpec("Reception", 1, 0.5, mismatched));

        Assert.Contains("0.5", ex.Message, StringComparison.Ordinal);
    }
#else
    [Fact]
    public void StageSpec_RejectsSpecRateMismatchInDebug()
    {
        var mismatched = new DistributionSpec(DistributionFamily.Exponential, Mean: 10.0);

        var stage = new StageSpec("Reception", 1, 0.5, mismatched);

        Assert.Equal(0.5, stage.ServiceRate);
        Assert.Equal(10.0, stage.EffectiveServiceDistribution.Mean);
    }
#endif

    /// <summary>
    /// Per-stage lists that do not line up with the stage names mean the record was
    /// built wrong. Indexing it anyway would either throw mid-run or silently drop a
    /// stage, producing a network the user never configured but reporting it as valid.
    /// </summary>
    [Fact]
    public void Coordinator_FailsFastOnPerStageListCountMismatch()
    {
        var parameters = ManualRun(muPerStage: [0.8, 0.6, 0.4]).TryBuildRunParameters()!;

        var shortFamilies = parameters with { ServiceFamilies = parameters.ServiceFamilies.Take(2).ToList() };
        var familiesError = Assert.Throws<ArgumentException>(
            () => SimulationCoordinator.Run(shortFamilies, binding: null));
        Assert.Contains("3 stage name(s)", familiesError.Message, StringComparison.Ordinal);
        Assert.Contains("ServiceFamilies has 2", familiesError.Message, StringComparison.Ordinal);

        var shortRates = parameters with { ServiceRates = parameters.ServiceRates.Take(2).ToList() };
        var ratesError = Assert.Throws<ArgumentException>(
            () => SimulationCoordinator.Run(shortRates, binding: null));
        Assert.Contains("3 stage name(s)", ratesError.Message, StringComparison.Ordinal);
        Assert.Contains("ServiceRates has 2", ratesError.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The UI still offers one service-family dropdown, so the reachable configuration
    /// is "same family, every stage". Each stage must receive its own spec carrying that
    /// family and the mean belonging to that stage's own μ — the two arrive together
    /// from the same loop, which is what makes a mismatch unrepresentable.
    /// </summary>
    [Fact]
    public void ConfigPanel_SingleDistribution_MapsToIdenticalSpecsPerStage()
    {
        var config = ManualRun(muPerStage: [0.8, 0.6, 0.4], serviceFamily: DistributionFamily.Exponential);

        var parameters = config.TryBuildRunParameters()!;

        Assert.Equal(3, parameters.ServiceFamilies.Count);
        Assert.All(parameters.ServiceFamilies, spec => Assert.Equal(DistributionFamily.Exponential, spec.Family));
        Assert.Equal(
            parameters.ServiceRates.Select(rate => 1.0 / rate!.Value).ToArray(),
            parameters.ServiceFamilies.Select(spec => spec.Mean).ToArray());
    }

    /// <summary>
    /// The link the first seven tests missed: a configured family must survive the
    /// coordinator and be the family the engine actually samples from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added after a mutation test exposed the gap. Dropping the per-stage spec at
    /// coordinator construction — the precise defect Phase 8J exists to fix — left all
    /// 480 tests green, because an all-Exponential network is indistinguishable from the
    /// configured one when every stage is Exponential anyway. Asserting the three links
    /// separately (the view model produces it, Core accepts it, the rate is not inverted)
    /// therefore proved nothing about whether they were connected.
    /// </para>
    /// <para>
    /// Deterministic is the probe, not Gamma or Normal: a constant service time has zero
    /// variance, a signature no other family can produce. Gamma and Exponential differ
    /// only statistically, and with a fixed seed a test built on that difference could
    /// sit inside a noise band and pass or fail for reasons unrelated to the plumbing.
    /// Zero variance is unambiguous.
    /// </para>
    /// </remarks>
    [Fact]
    public void Coordinator_DeterministicFamily_ReachesEngineSampler()
    {
        const double meanMinutes = 1.25;
        const double mu = 0.8; // 1 / meanMinutes

        var parameters = new SimulationParameters(
            ParameterMode.RateWise,
            "Exponential",
            ManualArrivalRate: 0.5,
            StageNames: ["Reception"],
            ServerCounts: [1],
            ManualServiceRates: new double?[] { mu },
            RunMode.DiagnosticTrace,
            HorizonMinutes: 60,
            GeneratorDays: 1,
            StartDay: DayOfWeek.Monday,
            DailyCap: null,
            Seed: 42,
            PExitOverride: null,
            TraceLevelName: "Standard")
        {
            ServiceFamilies = [new DistributionSpec(DistributionFamily.Deterministic, Mean: meanMinutes)],
            ServiceRates = new double?[] { mu },
        };

        var outcome = SimulationCoordinator.Run(parameters, binding: null);

        Assert.Null(outcome.Error);
        var samples = outcome.Result!.GeneratedServiceSamplesByStage[0];
        Assert.NotEmpty(samples);

        // Every draw is the configured mean...
        Assert.All(samples, sample => Assert.Equal(meanMinutes, sample, 10));

        // ...so the spread is zero. If the coordinator had dropped the spec, the
        // fallback Exponential sampler would give a strictly positive standard
        // deviation here and this assertion would fail.
        double sampleMean = samples.Average();
        double standardDeviation = Math.Sqrt(
            samples.Select(s => (s - sampleMean) * (s - sampleMean)).Average());

        Assert.Equal(0.0, standardDeviation, 12);
    }

    /// <summary>Builds the empty-list record that <see cref="ServiceFamilies_DefaultToEmpty"/> pins.</summary>
    private static SimulationParameters NewParameters() => new(
        ParameterMode.RateWise,
        "Exponential",
        0.5,
        ["Reception", "Screening", "Doctor"],
        [1, 2, 3],
        new double?[] { 0.8, 0.6, 0.4 },
        RunMode.ClinicDay,
        120,
        1,
        DayOfWeek.Monday,
        null,
        42,
        0.4,
        "Standard");

    /// <summary>
    /// A config panel in the same state as the launch-gate walkthrough: parameters
    /// supplied by hand, one μ per stage, no data file.
    /// </summary>
    private static ConfigPanelViewModel ManualRun(
        double[] muPerStage,
        DistributionFamily serviceFamily = DistributionFamily.Exponential)
    {
        var config = new ConfigPanelViewModel
        {
            ParametersIsOptionalEnabled = true,
        };

        // Phase 8L: "Exponential throughout" is per-stage state now, so it is
        // written to the rows. The old global string is gone.
        foreach (var row in config.StageRows)
        {
            row.ServiceFamily = serviceFamily;
        }

        config.ManualLambda.Value = "0.5";
        config.PExit.Value = "0.4";
        config.ManualMuPerStage.Value = string.Join(", ", muPerStage);

        return config;
    }
}
