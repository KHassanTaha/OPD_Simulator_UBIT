using OpdSimulator.App.Models;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Parameters;
using OpdSimulator.App.Services;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.2 (D-179): the GUI→coordinator path for the direct-to-Doctor
/// probability.
/// </summary>
/// <remarks>
/// The Core routing is covered in Core.Tests; what matters here is that the
/// value survives the trip from the file, through the config panel, into the
/// topology — and that a network which cannot express a bypass says so instead of
/// silently running a different experiment.
/// </remarks>
public class BypassCoordinatorTests
{
    /// <summary>
    /// Stage names matching the stage COUNT, mirroring the shapes the engine really
    /// has to run: one stage, the 2-stage clinic capture (Screening → Doctor), and
    /// the 3-stage OPD network (Reception → Screening → Doctor). Routing is by
    /// index, so the names are cosmetic — but a fixture calling the front door
    /// "Reception" in a two-stage network would quietly misdescribe what is being
    /// tested.
    /// </summary>
    private static string[] StageNamesFor(int stageCount) => stageCount switch
    {
        1 => ["Reception"],
        2 => ["Screening", "Doctor"],
        _ => ["Reception", "Screening", "Doctor"],
    };

    private static SimulationParameters Parameters(
        int stageCount = 3,
        double? pBypassOverride = null,
        double? manualLambda = 0.5) => new(
        ParameterMode.RateWise,
        "Exponential",
        manualLambda,
        StageNamesFor(stageCount),
        Enumerable.Repeat(2, stageCount).ToArray(),
        Enumerable.Repeat<double?>(null, stageCount).ToArray(),
        RunMode.ClinicDay,
        HorizonMinutes: 600,
        GeneratorDays: 1,
        StartDay: DayOfWeek.Monday,
        DailyCap: null,
        Seed: 42,
        PExitOverride: null,
        "Standard")
    {
        ServiceRates = Enumerable.Repeat<double?>(1.0, stageCount).ToArray(),
        ServiceFamilies = Enumerable.Repeat(
            new DistributionSpec(DistributionFamily.Deterministic, Mean: 1.0), stageCount).ToArray(),
        PBypassOverride = pBypassOverride,
    };

    [Fact]
    public void Override_WinsOverTheFittedValue()
    {
        var binding = new DataBindingResult("mem://x", null, Array.Empty<Data.Validation.ValidationIssue>(), null,
            0.5, Array.Empty<string>(), Array.Empty<double>(), 0.4, 0, 0, 0,
            Array.Empty<double>(), new Dictionary<string, IReadOnlyList<double>>())
        {
            FittedBypassProbability = 0.15,
        };

        var parameters = Parameters(pBypassOverride: 0.30);

        Assert.Equal(0.30, SimulationCoordinator.ResolveBypassProbability(parameters, binding));
    }

    [Fact]
    public void FittedValue_IsUsedWhenNoOverrideIsTyped()
    {
        var binding = new DataBindingResult("mem://x", null, Array.Empty<Data.Validation.ValidationIssue>(), null,
            0.5, Array.Empty<string>(), Array.Empty<double>(), 0.4, 0, 0, 0,
            Array.Empty<double>(), new Dictionary<string, IReadOnlyList<double>>())
        {
            FittedBypassProbability = 0.15,
        };

        Assert.Equal(0.15, SimulationCoordinator.ResolveBypassProbability(Parameters(), binding));
    }

    [Fact]
    public void NoOverrideAndNoData_MeansBypassOff()
    {
        // Not 0.4 the way p_exit defaults: there is no documented norm for
        // skipping screening, so an invented default would route patients through
        // a stage the user never asked for.
        Assert.Equal(0.0, SimulationCoordinator.ResolveBypassProbability(Parameters(), binding: null));
    }

    [Fact]
    public void ThreeStageNetwork_AppliesTheBypassAndReportsItBack()
    {
        var outcome = SimulationCoordinator.Run(Parameters(pBypassOverride: 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.25, outcome.EffectiveBypassProbability);

        // λ_screening loses the bypassed share, λ_doctor gains it (D-007 as
        // amended). Reception is untouched.
        var screening = outcome.Result!.StageMetrics.Single(m => m.StageName == "Screening");
        var doctor = outcome.Result.StageMetrics.Single(m => m.StageName == "Doctor");
        Assert.Equal(0.5 * 0.75, screening.ArrivalRate, 3);
        Assert.True(doctor.ArrivalRate > 0.5 * 0.25, "Doctor inflow must include the bypass share");
    }

    [Fact]
    public void TwoStageNetwork_RoutesTheBypassWithTheDrawAtArrival()
    {
        // Since 8R a two-stage network is the arrival-time case, not a refusal: the
        // skipped stage is the front door, so a quarter of arrivals never enter it
        // and land on the Doctor instead (D-189). This is the shape of the clinic
        // capture, and the 8Q coordinator refused to run it at all.
        var outcome = SimulationCoordinator.Run(Parameters(stageCount: 2, pBypassOverride: 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.25, outcome.EffectiveBypassProbability);

        var screening = outcome.Result!.StageMetrics.Single(m => m.StageName == "Screening");
        var doctor = outcome.Result.StageMetrics.Single(m => m.StageName == "Doctor");

        // λ₀ = 0.5. Screening is the skipped stage and the front door, so it sees
        // 0.5 × (1 − 0.25) = 0.375 — the bypass share never arrives at all.
        Assert.Equal(0.5 * 0.75, screening.ArrivalRate, 3);

        // The Doctor absorbs that bypass share directly and only the 60% of screened
        // patients who do not exit there: 0.5 × 0.25 + 0.375 × 0.6 = 0.35. The 0.6
        // is the coordinator's default p_exit (0.4) with no override and no file.
        Assert.Equal(0.5 * 0.25 + screening.ArrivalRate * 0.6, doctor.ArrivalRate, 3);
        Assert.True(
            doctor.ArrivalRate > 0.5 * 0.25,
            $"Doctor inflow ({doctor.ArrivalRate}) must exceed the bypass share alone ({0.5 * 0.25})");
    }

    [Fact]
    public void SingleStageNetwork_NormalisesTheBypassToZero()
    {
        // One stage can neither skip anything nor land anywhere else, so bypass is
        // genuinely meaningless. The run still happens; the outcome reports 0 rather
        // than the typed value, so the calculations text cannot claim a bypass the
        // engine did not perform.
        var outcome = SimulationCoordinator.Run(Parameters(stageCount: 1, pBypassOverride: 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.0, outcome.EffectiveBypassProbability);
    }

    [Fact]
    public void BypassOff_LeavesScreeningInflowAtTheFullArrivalRate()
    {
        var outcome = SimulationCoordinator.Run(Parameters(pBypassOverride: 0.0), binding: null);

        var screening = outcome.Result!.StageMetrics.Single(m => m.StageName == "Screening");
        Assert.Equal(0.5, screening.ArrivalRate, 3);
    }
}