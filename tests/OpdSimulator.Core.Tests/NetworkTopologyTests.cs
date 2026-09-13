using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Unit tests for <see cref="NetworkTopology"/>: routing-derived arrival rates
/// (D-007), per-stage ρ, and the all-stage stability refusal (FR-VAL-1).
/// </summary>
public class NetworkTopologyTests
{
    private static NetworkTopology Clinic() => new(
        arrivalRate: 2.0,
        new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 10.0),
            new StageSpec("Screening", serverCount: 2, serviceRate: 4.0),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 1.6),
        },
        exitStageIndex: 1,
        exitProbability: 0.25);

    [Fact]
    public void EffectiveArrivalRates_FollowRoutingDerivedRule()
    {
        // D-007: λ_reception = λ0, λ_screening = λ0, λ_doctor = λ0·(1 − p_exit).
        var topology = Clinic();

        Assert.Equal(2.0, topology.EffectiveArrivalRate(0), 9);
        Assert.Equal(2.0, topology.EffectiveArrivalRate(1), 9);
        Assert.Equal(1.5, topology.EffectiveArrivalRate(2), 9); // 2.0 · (1 − 0.25)
    }

    [Fact]
    public void RhoFor_MatchesEffectiveLambdaOverCtimesMu()
    {
        var topology = Clinic();

        Assert.Equal(0.3125, topology.RhoFor(2), 9); // 1.5 / (3 · 1.6) = 0.3125
    }

    [Fact]
    public void Validate_StableTopology_DoesNotThrow()
    {
        var topology = Clinic(); // all ρ < 1
        topology.Validate();
    }

    [Fact]
    public void Validate_UnstableStages_ListsEveryOffender()
    {
        // Screening μ = 1 → ρ = 2/(2·1) = 1.0 AND Doctor μ = 0.8 → ρ = 1.5/(2.4) = 0.625 (stable).
        // Force two unstable stages: Reception slow (μ=1, ρ=2.0) and Screening slow (μ=1, ρ=1.0).
        var topology = new NetworkTopology(2.0, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 1.0),  // ρ = 2.0 ≥ 1
            new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),  // ρ = 1.0 ≥ 1
            new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),     // ρ = 1.5/(3·1) = 0.5
        }, exitStageIndex: 1, exitProbability: 0.25);

        var ex = Assert.Throws<UnstableSystemException>(() => topology.Validate());

        Assert.Contains("Reception", ex.Message);
        Assert.Contains("Screening", ex.Message);
        Assert.Contains("2.00", ex.Message);  // Reception ρ
        Assert.Contains("1.00", ex.Message);  // Screening ρ
        Assert.Equal("Reception", ex.StageName);
        Assert.Equal(2.0, ex.Rho, 3);
    }

    [Fact]
    public void SingleStageFactory_TreatsAsOneStageWithNoProbabilisticExit()
    {
        var topology = NetworkTopology.CreateSingleStage(3.0, 4.0, serverCount: 1, stageName: "Reception");

        Assert.Single(topology.StageSpecs);
        Assert.Equal(-1, topology.ExitStageIndex);
        Assert.Equal(0.0, topology.ExitProbability);
        Assert.Equal(3.0, topology.EffectiveArrivalRate(0), 9);
        Assert.Equal(0.75, topology.RhoFor(0), 9);
        topology.Validate(); // ρ = 0.75 < 1 → stable
    }

    [Fact]
    public void Constructor_EmptyStages_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new NetworkTopology(1.0, Array.Empty<StageSpec>()));
    }

    [Fact]
    public void Constructor_ZeroExitProbability_NormalisesExitStageAway()
    {
        var topology = new NetworkTopology(1.0, new[]
        {
            new StageSpec("Only", serverCount: 1, serviceRate: 1.0),
        }, exitStageIndex: 0, exitProbability: 0.0);

        Assert.Equal(-1, topology.ExitStageIndex);
        Assert.Equal(0.0, topology.ExitProbability);
    }
}