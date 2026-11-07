namespace OpdSimulator.Core.Tests;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using Serilog;
using Xunit;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Verifies the general arrival process (Phase 8V, D-211): a topology can carry
/// any supported distribution as its inter-arrival process, the engine samples
/// exactly that distribution, and the null case keeps the legacy Poisson path.
/// </summary>
/// <remarks>
/// These tests assert the <b>generated sample</b>, not the spec that was passed
/// in. A sampler wired to the wrong spec would still accept a correct-looking
/// topology, so asserting the configuration would pass against the very bug the
/// feature exists to prevent.
/// </remarks>
public class ArrivalDistributionTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    private const double Lambda = 0.5;
    private const double Mu = 1.0;

    [Theory]
    [InlineData(DistributionFamily.Deterministic, 0.0)]
    [InlineData(DistributionFamily.Exponential, 1.0)]
    [InlineData(DistributionFamily.Gamma, 0.25)]
    [InlineData(DistributionFamily.Lognormal, 2.25)]
    [InlineData(DistributionFamily.Uniform, 0.083333333333)]
    public void WithMean_PreservesTheSquaredCoefficientOfVariation(
        DistributionFamily family,
        double expectedCa2)
    {
        DistributionSpec spec = BuildSpec(family);
        DistributionSpec relocated = DistributionSpecOperations.WithMean(spec, mean: 7.5);

        Assert.Equal(7.5, relocated.Mean, 9);
        Assert.Equal(expectedCa2, DistributionSpecOperations.SquaredCoefficientOfVariation(relocated), 6);
    }

    [Fact]
    public void WithMean_PreservesTheShapeOfAGamma_NotItsAbsoluteSpread()
    {
        var spec = new DistributionSpec(DistributionFamily.Gamma, Mean: 2.0, Shape: 4.0, Scale: 0.5);

        var relocated = DistributionSpecOperations.WithMean(spec, mean: 10.0);

        Assert.Equal(4.0, relocated.Shape!.Value);
        Assert.Equal(2.5, relocated.Scale!.Value, 9); // mean / shape = 10 / 4
    }

    [Fact]
    public void Topology_RejectsAnArrivalSpecWhoseMeanDisagreesWithTheRate()
    {
        // Mean 1.0 implies λ = 1, but the topology is built for λ = 0.5.
        var spec = new DistributionSpec(DistributionFamily.Gamma, Mean: 1.0, Shape: 2.0, Scale: 0.5);

        var error = Assert.Throws<ArgumentException>(() => new NetworkTopology(
            arrivalRate: 0.5,
            new[] { new StageSpec("Reception", 1, Mu) },
            arrivalDistribution: spec));

        Assert.Contains("implies a rate", error.Message);
        Assert.Contains("WithMean", error.Message);
    }

    [Fact]
    public void DeterministicArrivals_ProduceExactlySpacedArrivals()
    {
        DistributionSpec spec = DistributionSpecOperations.Create(
            DistributionFamily.Deterministic, mean: 1.0 / Lambda, spread: null);
        var topology = NetworkTopology.CreateSingleStage(
            Lambda, Mu, serverCount: 1, "Reception", arrivalDistribution: spec);

        SimulationResult result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 20_000);

        Assert.NotEmpty(result.GeneratedInterArrivalSamples);
        Assert.All(result.GeneratedInterArrivalSamples, sample => Assert.Equal(2.0, sample, 9));
    }

    [Fact]
    public void GammaArrivals_ProduceTheRequestedCaSquared()
    {
        // Gamma with shape k has Ca² = 1/k. k = 4 → 0.25, which is a genuinely
        // non-Poisson process: this is the sample G/G verification rests on.
        DistributionSpec spec = DistributionSpecOperations.Create(
            DistributionFamily.Gamma, mean: 1.0 / Lambda, spread: 4.0);
        var topology = NetworkTopology.CreateSingleStage(
            Lambda, Mu, serverCount: 1, "Reception", arrivalDistribution: spec);

        SimulationResult result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 100_000);

        Assert.InRange(SampleCa2(result.GeneratedInterArrivalSamples), 0.22, 0.28);
    }

    [Fact]
    public void LognormalArrivals_ProduceCaSquaredAboveOne()
    {
        // StdDev = 3 min on a mean of 2 min → CV = 1.5 → Ca² = 2.25.
        DistributionSpec spec = DistributionSpecOperations.Create(
            DistributionFamily.Lognormal, mean: 1.0 / Lambda, spread: 3.0);
        var topology = NetworkTopology.CreateSingleStage(
            Lambda, Mu, serverCount: 1, "Reception", arrivalDistribution: spec);

        SimulationResult result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 100_000);

        Assert.InRange(SampleCa2(result.GeneratedInterArrivalSamples), 2.0, 2.55);
    }

    [Fact]
    public void NullArrivalSpec_KeepsTheLegacyPoissonProcess()
    {
        var topology = NetworkTopology.CreateSingleStage(Lambda, Mu, serverCount: 1, "Reception");

        SimulationResult result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 100_000);

        Assert.Null(topology.ArrivalDistribution);
        Assert.Equal(DistributionFamily.Exponential, topology.EffectiveArrivalDistribution.Family);
        Assert.InRange(SampleCa2(result.GeneratedInterArrivalSamples), 0.9, 1.1);
    }

    [Fact]
    public void ConfiguredArrivals_AreReproducibleFromTheSeed()
    {
        DistributionSpec spec = DistributionSpecOperations.Create(
            DistributionFamily.Gamma, mean: 1.0 / Lambda, spread: 2.0);
        var topology = NetworkTopology.CreateSingleStage(
            Lambda, Mu, 1, "Reception", arrivalDistribution: spec);
        var engine = new Engine(new SeededRandomSource(), Log);

        SimulationResult first = engine.Run(topology, seed: 7, horizonMinutes: 5_000);
        SimulationResult second = engine.Run(topology, seed: 7, horizonMinutes: 5_000);

        Assert.Equal(first.GeneratedInterArrivalSamples, second.GeneratedInterArrivalSamples);
    }

    private static DistributionSpec BuildSpec(DistributionFamily family) => family switch
    {
        DistributionFamily.Exponential => new DistributionSpec(family, Mean: 2.0),
        DistributionFamily.Deterministic => new DistributionSpec(family, Mean: 2.0),
        DistributionFamily.Gamma => new DistributionSpec(family, Mean: 2.0, Shape: 4.0, Scale: 0.5),
        DistributionFamily.Lognormal => new DistributionSpec(family, Mean: 2.0, StdDev: 3.0),
        DistributionFamily.Uniform => new DistributionSpec(family, Mean: 2.0, Min: 1.0, Max: 3.0),
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static double SampleCa2(IReadOnlyList<double> samples)
    {
        double mean = samples.Average();
        double variance = samples.Sum(value => (value - mean) * (value - mean)) / (samples.Count - 1);
        return variance / (mean * mean);
    }
}
