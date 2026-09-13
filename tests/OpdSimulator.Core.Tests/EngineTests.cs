using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Servers;
using OpdSimulator.Core.Stages;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Integration tests for <see cref="Engine"/>: determinism (NFR-4) and
/// validation against the analytical M/M/1 result (CONTEXT §8).
/// </summary>
public class EngineTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    [Fact]
    public void Run_SameSeed_TwoRuns_ProduceIdenticalResults()
    {
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);

        var first = new Engine(config, new SeededRandomSource(), Log).Run();
        var second = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.Equal(first.TotalPatientsServed, second.TotalPatientsServed);
        Assert.Equal(first.AverageWaitMinutes, second.AverageWaitMinutes);
        Assert.Equal(first.AverageQueueLength, second.AverageQueueLength);
        Assert.Equal(first.AverageSystemTimeMinutes, second.AverageSystemTimeMinutes);
        Assert.Equal(first.StageUtilisation, second.StageUtilisation);
        Assert.Equal(first.ThroughputPerMinute, second.ThroughputPerMinute);
        Assert.Equal(first.PerServerUtilisation, second.PerServerUtilisation);
    }

    [Fact]
    public void Run_MatchesAnalyticalMM1_AverageWaitWithin15Percent()
    {
        // Analytical M/M/1 (λ = 3, μ = 4):  ρ = 0.75, Wq = ρ/(μ−λ) = 0.75 min.
        // Long horizon ⇒ simulated Wq must approach the analytical value.
        const double lambda = 3.0;
        const double mu = 4.0;
        const double horizon = 10000;
        var config = new EngineConfig(lambda, mu, serverCount: 1, horizonMinutes: horizon, seed: 42);

        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        double rho = lambda / mu;
        double analyticalWait = rho / (mu - lambda);

        Assert.InRange(result.AverageWaitMinutes,
            analyticalWait * 0.85, analyticalWait * 1.15);
    }

    [Fact]
    public void Run_StableConfig_ProducesSaneUtilisation()
    {
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);
        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.True(result.TotalPatientsServed > 0);
        Assert.InRange(result.StageUtilisation, 0.0, 1.0);
        Assert.All(result.PerServerUtilisation, u => Assert.InRange(u, 0.0, 1.0));
        Assert.Equal(1, result.PerServerUtilisation.Count);
    }

    [Fact]
    public void Run_StableConfig_ThroughputApproachesArrivalRate()
    {
        // In steady state, throughput → λ (patients per minute).
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);
        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.InRange(result.ThroughputPerMinute, 2.7, 3.3);
    }

    [Fact]
    public void Run_SingleStage_ByteForByteRegression()
    {
        // Guard on the Milestone-1 delegation (kickoff G2): after the engine
        // became N-stage generic, the legacy single-stage path must still
        // reproduce the exact known output — seed 42, λ=3, μ=4, c=1, h=10000.
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);
        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.Equal(29892, result.TotalPatientsServed);
        Assert.Equal(0.724, result.AverageWaitMinutes, 3);
        Assert.Equal(1, result.StageMetrics.Count);
        Assert.Equal(config.StageName, result.StageMetrics[0].StageName);
    }

    [Fact]
    public void Run_ClinicNetwork_ProducesPerStageMetrics()
    {
        // Reception(c=1, μ=10) → Screening(c=2, μ=4) → Doctor(c=3, μ=1.6),
        // p_exit = 0.4. All stable: ρ_R = 0.30, ρ_S = 0.375, ρ_D = 3·0.6/4.8 = 0.375.
        var topology = new NetworkTopology(3.0, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 10.0),
            new StageSpec("Screening", serverCount: 2, serviceRate: 4.0),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 1.6),
        }, exitStageIndex: 1, exitProbability: 0.4);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 1000);

        Assert.Equal(3, result.StageMetrics.Count);
        Assert.Equal("Reception", result.StageMetrics[0].StageName);
        Assert.Equal(0.30, result.StageMetrics[0].Rho, 3);

        // Routing-derived effective rates (D-007): λ_screening = λ0, λ_doctor = λ0·(1−p_exit).
        Assert.Equal(3.0, result.StageMetrics[1].ArrivalRate, 3);
        Assert.Equal(1.8, result.StageMetrics[2].ArrivalRate, 3);

        // Every stage saw work and every utilisation stays inside [0, 1] (FR-VAL-2).
        Assert.All(result.StageMetrics, m => Assert.True(m.PatientsServed > 0, $"{m.StageName} must serve at least one patient"));
        Assert.All(result.StageMetrics, m => Assert.InRange(m.StageUtilisation, 0.0, 1.0));
        Assert.All(result.StageMetrics, m => Assert.All(m.PerServerUtilisation, u => Assert.InRange(u, 0.0, 1.0)));
    }

    [Fact]
    public void Run_ClinicNetwork_ExitProbability_RoutesOnlyFractionToDoctor()
    {
        // p_exit = 0.6 ⇒ 40% of screening completions continue to Doctor, so the
        // doctor stage must see strictly fewer patients than screening.
        var topology = new NetworkTopology(3.0, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 10.0),
            new StageSpec("Screening", serverCount: 2, serviceRate: 4.0),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 1.6),
        }, exitStageIndex: 1, exitProbability: 0.6);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 2000);

        int screening = result.StageMetrics[1].PatientsServed;
        int doctor = result.StageMetrics[2].PatientsServed;

        Assert.True(doctor > 0, "some patients must reach the doctor stage");
        Assert.True(doctor < screening, "only the continuation fraction may reach the doctor stage");
        Assert.InRange((double)doctor / screening, 0.30, 0.50); // ≈ 0.40, wide band for sampling noise
        Assert.True(result.TotalPatientsServed >= doctor, "completed patients = doctor completions + screening exits");
    }

    [Fact]
    public void Run_NetworkSymmetricServers2_RandomSelection_BalancesUtilisation()
    {
        // D-017 latent-bug regression: ρ = 0.25 at a 2-server single stage
        // (λ = 2, μ = 4, c = 2) — low load, so most arrivals find both servers
        // idle and assignment bias is at its strongest. Random-among-idle must
        // spread the load, keeping |util₀ − util₁| under the 0.10 flag.
        var topology = NetworkTopology.CreateSingleStage(2.0, 4.0, serverCount: 2, stageName: "Reception");
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 10000);

        double diff = Math.Abs(result.PerServerUtilisation[0] - result.PerServerUtilisation[1]);
        Assert.True(diff < 0.10, $"random selection must balance identical servers (|util0 − util1| = {diff:F4})");
    }

    [Fact]
    public void Run_NetworkSymmetricServers2_LowestIdPolicy_RecreatesImbalance()
    {
        // Negative control for the same threshold: the old lowest-ID policy
        // (latent M1 bug, D-017) must exceed 0.10 in the same low-load regime,
        // proving the flag catches it.
        var topology = NetworkTopology.CreateSingleStage(2.0, 4.0, serverCount: 2, stageName: "Reception");
        var engine = new Engine(new SeededRandomSource(), Log, serverSelection: new LowestIdSelection());
        var result = engine.Run(topology, seed: 42, horizonMinutes: 10000);

        double diff = Math.Abs(result.PerServerUtilisation[0] - result.PerServerUtilisation[1]);
        Assert.True(diff >= 0.10, $"lowest-ID assignment must produce a palpable imbalance (|util0 − util1| = {diff:F4})");
    }
}