using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
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
}