using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Tests for Phase 8I: the Engine dispatching each stage on its own
/// <see cref="DistributionFamily"/> rather than assuming exponential service everywhere.
/// </summary>
/// <remarks>
/// <para>
/// Two properties are load-bearing and both are asserted here. First, that a stage with
/// no explicit distribution still runs M/M/c — <see cref="StageSpec"/>'s fallback must
/// keep every pre-8I caller bit-for-bit identical. Second, that an explicit family
/// actually changes what the engine does, which is the entire point of the phase: a
/// deterministic stage must produce constant service times, so a mixed M/M/1 + M/D/2 +
/// M/M/3 network is genuinely heterogeneous rather than three copies of the same queue.
/// </para>
/// <para>
/// The golden hashes in <see cref="Engine_ExponentialStage_StillByteIdenticalToLegacy"/>
/// were captured from the pre-8I build at commit a192ea2 and cover every individual draw
/// of a 30,015-patient run, not just the aggregate totals.
/// </para>
/// </remarks>
public class StageFamilyDispatchTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    // ---------- 8I.1: the StageSpec fallback ----------

    [Fact]
    public void StageSpec_NoDistribution_UsesExponentialDefault()
    {
        var spec = new StageSpec("R", 1, 0.5);

        Assert.Null(spec.ServiceDistribution);
        Assert.Equal(DistributionFamily.Exponential, spec.EffectiveServiceDistribution.Family);
        Assert.Equal(2.0, spec.EffectiveServiceDistribution.Mean);
    }

    [Fact]
    public void StageSpec_ExplicitDistribution_UsesIt()
    {
        var spec = new StageSpec("R", 1, 0.5, new DistributionSpec(DistributionFamily.Deterministic, Mean: 2.0));

        Assert.Equal(DistributionFamily.Deterministic, spec.ServiceDistribution!.Family);
        Assert.Equal(DistributionFamily.Deterministic, spec.EffectiveServiceDistribution.Family);

        // The explicit spec wins outright — the rate is not used to override it.
        Assert.Equal(2.0, spec.EffectiveServiceDistribution.Mean);
    }

    // ---------- 8I.2: the Engine honours the per-stage family ----------

    [Fact]
    public void Engine_SingleStage_DeterministicService_ProducesConstantTimes()
    {
        // M/D/1: constant 5-minute service, so ServiceRate must stay consistent with
        // that mean (1/5 = 0.2) or the analytical ρ check would describe a different
        // model than the one being simulated. λ = 0.1 keeps ρ = 0.5.
        var topology = new NetworkTopology(0.1, new[]
        {
            new StageSpec("Desk", serverCount: 1, serviceRate: 0.2,
                serviceDistribution: new DistributionSpec(DistributionFamily.Deterministic, Mean: 5.0)),
        });

        var result = new Engine(new SeededRandomSource(), Log).Run(topology, seed: 42, horizonMinutes: 1000);

        var serviceTimes = result.GeneratedServiceSamplesByStage.Single();
        Assert.NotEmpty(serviceTimes);
        Assert.All(serviceTimes, t => Assert.Equal(5.0, t));
    }

    [Fact]
    public void Engine_DeterministicService_ZeroVariance()
    {
        var deterministic = new NetworkTopology(0.1, new[]
        {
            new StageSpec("Desk", 1, 0.2,
                serviceDistribution: new DistributionSpec(DistributionFamily.Deterministic, Mean: 5.0)),
        });
        var deterministicResult = new Engine(new SeededRandomSource(), Log)
            .Run(deterministic, seed: 42, horizonMinutes: 1000);

        double sd = StdDev(deterministicResult.GeneratedServiceSamplesByStage.Single());
        Assert.Equal(0.0, sd);

        // Control: without the assertion below, a zero could just mean "no draws were
        // recorded". An exponential stage on the same setup must have real spread.
        //
        // The two runs deliberately serve different patient counts, so the counts are not
        // compared: a Deterministic sampler never reads the RNG, so its run advances the
        // stream differently from the moment the first patient is served. They are
        // different models, not the same model sampled twice.
        var exponential = new NetworkTopology(0.1, new[] { new StageSpec("Desk", 1, 0.2) });
        var exponentialResult = new Engine(new SeededRandomSource(), Log)
            .Run(exponential, seed: 42, horizonMinutes: 1000);

        var exponentialTimes = exponentialResult.GeneratedServiceSamplesByStage.Single();
        Assert.NotEmpty(exponentialTimes);
        Assert.True(StdDev(exponentialTimes) > 0.0,
            "Control failed: the exponential stage should show spread, so the zero above is caused by determinism.");
    }

    [Fact]
    public void Engine_HeterogeneousStages_MM1_MD2_MM3_Runs()
    {
        // Reception M/M/1, Screening M/D/2, Doctor M/M/3. The middle stage is the
        // one that could not be expressed before 8I.
        var topology = new NetworkTopology(3.0, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 10.0),
            new StageSpec("Screening", serverCount: 2, serviceRate: 4.0,
                serviceDistribution: new DistributionSpec(DistributionFamily.Deterministic, Mean: 0.25)),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 1.6),
        });

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 10_000);

        Assert.Equal(3, result.StageMetrics.Count);
        Assert.Equal(new[] { "Reception", "Screening", "Doctor" }, result.StageMetrics.Select(m => m.StageName));
        Assert.Equal(new[] { 1, 2, 3 }, result.StageMetrics.Select(m => m.ServerCount));

        // Every stage must actually have done work, at its own mean service time.
        for (int i = 0; i < 3; i++)
        {
            var metrics = result.StageMetrics[i];
            Assert.True(metrics.PatientsServed > 0, $"{metrics.StageName} served nobody.");
            Assert.NotEmpty(metrics.WaitingTimeSamples);
            Assert.NotEmpty(result.GeneratedServiceSamplesByStage[i]);
        }

        // The heterogeneity itself: screening is deterministic, its neighbours are not.
        Assert.Equal(0.0, StdDev(result.GeneratedServiceSamplesByStage[1]));
        Assert.True(StdDev(result.GeneratedServiceSamplesByStage[0]) > 0.0);
        Assert.True(StdDev(result.GeneratedServiceSamplesByStage[2]) > 0.0);
    }

    // ---------- 8I.2: the RNG-stream compatibility invariant ----------

    [Fact]
    public void Engine_ExponentialStage_StillByteIdenticalToLegacy()
    {
        // Before 8I a single shared ExponentialSampler served every stage. Now each
        // stage gets its own sampler built from EffectiveServiceDistribution, and for a
        // stage with no explicit distribution that resolves to Exponential with mean
        // 1/μ. The draw must be the same -ln(U)/λ inverse CDF over the same RNG stream,
        // or every recorded trace and every M/M/c result in this project silently shifts.
        //
        // These FNV-1a hashes were captured from the pre-8I build at commit a192ea2 and
        // fold in the exact 64 bits of every individual draw, so a single ulp anywhere in
        // ~30,000 service times and ~30,000 inter-arrivals would change them.
        var topology = new NetworkTopology(3.0, new[]
        {
            new StageSpec("Reception", 1, 10.0),
            new StageSpec("Screening", 2, 4.0),
            new StageSpec("Doctor", 3, 1.6),
        });

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, seed: 42, horizonMinutes: 10_000);

        // The recorded Milestone-1 totals, unchanged (D-054: served = 29892, wait = 0.724
        // is asserted on the single-stage path by Run_M1Regression_SingleStage_GoldenValues).
        Assert.Equal(30015, result.TotalPatientsServed);
        Assert.Equal(0.29072393484830333, result.AverageWaitMinutes);
        Assert.Equal(1.2625194639032111, result.AverageSystemTimeMinutes);

        Assert.Equal("E4544942AB19C7AF", Fnv1A64(result.GeneratedServiceSamplesByStage[0]));
        Assert.Equal("A8726414FAA25BED", Fnv1A64(result.GeneratedServiceSamplesByStage[1]));
        Assert.Equal("11B4D2C4BDB359E7", Fnv1A64(result.GeneratedServiceSamplesByStage[2]));
        Assert.Equal("942FBE0638F46C09", Fnv1A64(result.GeneratedInterArrivalSamples!));

        // Asking for Exponential explicitly must be indistinguishable from not asking:
        // otherwise a caller migrating to 8I would get different numbers for the same model.
        var explicitExponential = new NetworkTopology(3.0, new[]
        {
            new StageSpec("Reception", 1, 10.0,
                serviceDistribution: new DistributionSpec(DistributionFamily.Exponential, Mean: 0.1)),
            new StageSpec("Screening", 2, 4.0,
                serviceDistribution: new DistributionSpec(DistributionFamily.Exponential, Mean: 0.25)),
            new StageSpec("Doctor", 3, 1.6,
                serviceDistribution: new DistributionSpec(DistributionFamily.Exponential, Mean: 0.625)),
        });
        var explicitResult = new Engine(new SeededRandomSource(), Log)
            .Run(explicitExponential, seed: 42, horizonMinutes: 10_000);

        Assert.Equal(result.TotalPatientsServed, explicitResult.TotalPatientsServed);
        for (int i = 0; i < 3; i++)
            Assert.Equal(Fnv1A64(result.GeneratedServiceSamplesByStage[i]), Fnv1A64(explicitResult.GeneratedServiceSamplesByStage[i]));
    }

    // ---------- helpers ----------

    /// <summary>Population standard deviation; exactly 0 for a constant series.</summary>
    private static double StdDev(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0.0;

        double mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    /// <summary>
    /// FNV-1a over the raw little-endian bytes of each double, as 16 uppercase hex digits.
    /// </summary>
    /// <remarks>
    /// Chosen over a sum because a sum of ~30,000 doubles loses the low bits that make this
    /// a byte-level check. Folding the bytes means any change to any draw, by even one ulp,
    /// produces a different digest.
    /// </remarks>
    private static string Fnv1A64(IReadOnlyList<double> values)
    {
        const ulong offset = 0xcbf29ce484222325;
        const ulong prime = 0x100000001b3;

        ulong hash = offset;
        foreach (double value in values)
        {
            foreach (byte b in BitConverter.GetBytes(value))
            {
                hash ^= b;
                hash *= prime;
            }
        }

        return hash.ToString("X16");
    }
}
