using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Phase 8A: the engine must retain the inter-arrival and service times the RNG
/// actually produced, so a later goodness-of-fit pass can test the simulation
/// output itself against the configured distributions.
/// </summary>
public class GeneratedSamplesTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    private static NetworkTopology ThreeStageTopology()
        => new(0.5, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 1.5),
            new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 0.8),
        }, exitStageIndex: 1, exitProbability: 0.3);

    [Fact]
    public void Run_HorizonMode_RetainsInterArrivalSamples_CountMatches()
    {
        // The t = 0 seed arrival has no preceding gap, so a run with N served
        // patients scheduled exactly N−1 further arrivals — and therefore drew
        // exactly N−1 inter-arrival times.
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);

        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.Equal(result.TotalPatientsServed - 1, result.GeneratedInterArrivalSamples.Count);
        Assert.All(result.GeneratedInterArrivalSamples, value => Assert.True(value > 0));
    }

    [Fact]
    public void Run_HorizonMode_RetainsServiceSamples_PerStage()
    {
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStageTopology(), seed: 42, horizonMinutes: 500);

        Assert.Equal(result.StageMetrics.Count, result.GeneratedServiceSamplesByStage.Count);
        Assert.All(result.GeneratedServiceSamplesByStage, samples => Assert.NotEmpty(samples));
    }

    [Fact]
    public void Run_CalendarMode_RetainsSamples()
    {
        // Calendar arrivals are gated: many scheduled inter-arrivals fall in
        // closed periods and never become patients, so the sample count is not
        // tied to patients served. What must hold is that both streams were
        // recorded and every stage that ran produced service samples.
        var calendar = new ClinicCalendar();

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStageTopology(), calendar, generatorDays: 7, seed: 42);

        Assert.NotEmpty(result.GeneratedInterArrivalSamples);
        Assert.Equal(result.StageMetrics.Count, result.GeneratedServiceSamplesByStage.Count);
        Assert.All(result.GeneratedServiceSamplesByStage, samples => Assert.NotEmpty(samples));
    }

    [Fact]
    public void Run_SameSeed_ProducesIdenticalSamples()
    {
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 2000, seed: 42);

        var first = new Engine(config, new SeededRandomSource(), Log).Run();
        var second = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.True(first.GeneratedInterArrivalSamples.SequenceEqual(second.GeneratedInterArrivalSamples));
        Assert.Equal(first.GeneratedServiceSamplesByStage.Count, second.GeneratedServiceSamplesByStage.Count);
        for (int i = 0; i < first.GeneratedServiceSamplesByStage.Count; i++)
            Assert.True(first.GeneratedServiceSamplesByStage[i].SequenceEqual(second.GeneratedServiceSamplesByStage[i]));
    }

    [Fact]
    public void Run_DifferentSeed_ProducesDifferentSamples()
    {
        var config42 = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 2000, seed: 42);
        var config43 = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 2000, seed: 43);

        var first = new Engine(config42, new SeededRandomSource(), Log).Run();
        var second = new Engine(config43, new SeededRandomSource(), Log).Run();

        Assert.False(first.GeneratedInterArrivalSamples.SequenceEqual(second.GeneratedInterArrivalSamples));
    }
}
