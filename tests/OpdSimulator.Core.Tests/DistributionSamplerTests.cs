using OpdSimulator.Core.Distributions;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for the six <see cref="IDistributionSampler"/> families, the
/// <see cref="DistributionSamplerFactory"/>, and the <see cref="MathNetRandomAdapter"/>
/// that lets MathNet.Numerics draw from the project's <see cref="IRandomSource"/>.
/// </summary>
/// <remarks>
/// <para>
/// Statistical tests draw <see cref="SampleCount"/> samples from a source seeded with
/// <see cref="Seed"/> and compare the empirical mean and variance to the theoretical values
/// within <see cref="RelativeTolerance"/>. Because the seed is fixed the comparison is fully
/// deterministic: these tests either always pass or always fail, so they cannot flake.
/// </para>
/// <para>
/// The tolerance is far wider than sampling error. At n = 100,000 the relative standard
/// error of the sample mean is about 0.3% even in the worst family here, so a 5% band sits
/// roughly 15 standard errors out. A failure therefore means the parameterisation is wrong,
/// not that the draw was unlucky.
/// </para>
/// </remarks>
public class DistributionSamplerTests
{
    private const int Seed = 42;
    private const int SampleCount = 100_000;
    private const int SequenceLength = 1_000;
    private const double RelativeTolerance = 0.05;

    // ---------- Exponential ----------

    [Fact]
    public void Exponential_Mean_Within5Percent()
    {
        const double mean = 4.0;
        var sampler = new ExponentialDistributionSampler(new SeededRandomSource(Seed), mean);

        AssertClose(sampler.Mean, mean);
        AssertClose(EmpiricalMean(sampler), mean);
    }

    [Fact]
    public void Exponential_Variance_Within5Percent()
    {
        const double mean = 4.0;
        var sampler = new ExponentialDistributionSampler(new SeededRandomSource(Seed), mean);

        // Var = 1/λ² = mean² for an exponential.
        AssertClose(sampler.Variance, mean * mean);
        AssertClose(EmpiricalVariance(sampler), mean * mean);
    }

    // ---------- Deterministic ----------

    [Fact]
    public void Deterministic_AllSamplesEqualMean()
    {
        const double mean = 7.5;
        var sampler = new DeterministicSampler(new SeededRandomSource(Seed), mean);

        for (int i = 0; i < SampleCount; i++)
            Assert.Equal(mean, sampler.NextSample(), 10);

        Assert.Equal(0.0, sampler.Variance);
    }

    // ---------- Normal ----------

    [Fact]
    public void Normal_Mean_Within5Percent()
    {
        var sampler = new NormalSampler(new SeededRandomSource(Seed), 10.0, 2.0);

        AssertClose(sampler.Mean, 10.0);
        AssertClose(EmpiricalMean(sampler), 10.0);
    }

    [Fact]
    public void Normal_Variance_Within5Percent()
    {
        var sampler = new NormalSampler(new SeededRandomSource(Seed), 10.0, 2.0);

        // Var = σ².
        AssertClose(sampler.Variance, 4.0);
        AssertClose(EmpiricalVariance(sampler), 4.0);
    }

    // ---------- Lognormal ----------

    [Fact]
    public void Lognormal_Mean_Within5Percent()
    {
        // Mean and sd are given in the units of X, not of ln X.
        var sampler = new LognormalSampler(new SeededRandomSource(Seed), 5.0, 1.5);

        AssertClose(sampler.Mean, 5.0);
        AssertClose(EmpiricalMean(sampler), 5.0);
    }

    [Fact]
    public void Lognormal_Variance_Within5Percent()
    {
        var sampler = new LognormalSampler(new SeededRandomSource(Seed), 5.0, 1.5);

        // Var(X) = StdDev².
        AssertClose(sampler.Variance, 2.25);
        AssertClose(EmpiricalVariance(sampler), 2.25);
    }

    // ---------- Gamma ----------

    [Fact]
    public void Gamma_Mean_Within5Percent()
    {
        var sampler = new GammaSampler(new SeededRandomSource(Seed), shape: 4.0, scale: 2.0);

        AssertClose(sampler.Mean, 8.0);
        AssertClose(EmpiricalMean(sampler), 8.0);
    }

    [Fact]
    public void Gamma_Variance_Within5Percent()
    {
        var sampler = new GammaSampler(new SeededRandomSource(Seed), shape: 4.0, scale: 2.0);

        // Var = k·θ².
        AssertClose(sampler.Variance, 16.0);
        AssertClose(EmpiricalVariance(sampler), 16.0);
    }

    /// <summary>
    /// Pins the public contract to the scale parameterisation, independently of sampling.
    /// </summary>
    /// <remarks>
    /// MathNet's <c>Gamma(alpha, beta)</c> is rate-parameterised (mean α/β), so wiring θ
    /// straight through would report a mean of k/θ = 2.0 instead of k·θ = 8.0. The
    /// statistical tests above would also catch that, but they only say "something is
    /// wrong"; this test names the exact pitfall for the next reader.
    /// </remarks>
    [Fact]
    public void Gamma_MeanIsShapeTimesScale_NotShapeDividedByScale()
    {
        var sampler = new GammaSampler(new SeededRandomSource(Seed), shape: 4.0, scale: 2.0);

        Assert.Equal(8.0, sampler.Mean, 10);
        Assert.NotEqual(2.0, sampler.Mean, 10);
        Assert.Equal(16.0, sampler.Variance, 10);
    }

    // ---------- Uniform ----------

    [Fact]
    public void Uniform_Mean_Within5Percent()
    {
        var sampler = new UniformSampler(new SeededRandomSource(Seed), 2.0, 10.0);

        AssertClose(sampler.Mean, 6.0);
        AssertClose(EmpiricalMean(sampler), 6.0);
    }

    [Fact]
    public void Uniform_Variance_Within5Percent()
    {
        var sampler = new UniformSampler(new SeededRandomSource(Seed), 2.0, 10.0);

        // Var = (Max − Min)²/12 = 64/12.
        AssertClose(sampler.Variance, 64.0 / 12.0);
        AssertClose(EmpiricalVariance(sampler), 64.0 / 12.0);
    }

    // ---------- Reproducibility ----------

    [Fact]
    public void AllFamilies_SameSeed_IdenticalSequence()
    {
        foreach (var (name, a, b) in SamplerPairs())
        {
            for (int i = 0; i < SequenceLength; i++)
                Assert.Equal(a.NextSample(), b.NextSample());
        }
    }

    // ---------- Factory validation ----------

    [Fact]
    public void Factory_MissingStdDevForNormal_Throws()
    {
        var spec = new DistributionSpec(DistributionFamily.Normal, 5.0);

        var ex = Assert.Throws<ArgumentException>(
            () => DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed)));
        Assert.Contains("StdDev", ex.Message);
    }

    [Fact]
    public void Factory_NonPositiveMean_Throws()
    {
        var spec = new DistributionSpec(DistributionFamily.Exponential, 0.0);

        var ex = Assert.Throws<ArgumentException>(
            () => DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed)));
        Assert.Contains("Mean", ex.Message);
    }

    [Fact]
    public void Factory_UniformMinGreaterThanMax_Throws()
    {
        var spec = new DistributionSpec(DistributionFamily.Uniform, 0.0, Min: 10.0, Max: 2.0);

        var ex = Assert.Throws<ArgumentException>(
            () => DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed)));
        Assert.Contains("Min", ex.Message);
    }

    [Fact]
    public void Factory_AllSixFamilies_ReturnsSampler()
    {
        var cases = new (DistributionFamily Family, DistributionSpec Spec, Type Expected)[]
        {
            (DistributionFamily.Exponential,
                new DistributionSpec(DistributionFamily.Exponential, 3.0),
                typeof(ExponentialDistributionSampler)),

            (DistributionFamily.Deterministic,
                new DistributionSpec(DistributionFamily.Deterministic, 3.0),
                typeof(DeterministicSampler)),

            (DistributionFamily.Normal,
                new DistributionSpec(DistributionFamily.Normal, 3.0, StdDev: 1.0),
                typeof(NormalSampler)),

            (DistributionFamily.Lognormal,
                new DistributionSpec(DistributionFamily.Lognormal, 3.0, StdDev: 1.0),
                typeof(LognormalSampler)),

            (DistributionFamily.Gamma,
                new DistributionSpec(DistributionFamily.Gamma, 3.0, Shape: 2.0, Scale: 1.5),
                typeof(GammaSampler)),

            (DistributionFamily.Uniform,
                new DistributionSpec(DistributionFamily.Uniform, 3.0, Min: 1.0, Max: 5.0),
                typeof(UniformSampler))
        };

        foreach (var (family, spec, expected) in cases)
        {
            var sampler = DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed));

            Assert.IsAssignableFrom<IDistributionSampler>(sampler);
            Assert.IsType(expected, sampler);

            // Every family must also answer pdf and cdf, and produce a finite draw.
            Assert.True(sampler.Pdf(sampler.Mean) >= 0, $"{family}: pdf must be non-negative.");
            Assert.InRange(sampler.Cdf(sampler.Mean), 0.0, 1.0);
            Assert.True(double.IsFinite(sampler.NextSample()), $"{family}: draw must be finite.");
        }
    }

    // ---------- Adapter ----------

    /// <summary>
    /// Exercises every method <see cref="MathNetRandomAdapter"/> overrides and proves each one
    /// reads the wrapped <see cref="IRandomSource"/>.
    /// </summary>
    /// <remarks>
    /// The scripted source returns a value the base <see cref="Random"/> would essentially
    /// never produce at that position, so a method that silently fell through to base state
    /// would fail here instead of hiding inside a statistical tolerance. This is the test that
    /// catches a forgotten override.
    /// </remarks>
    [Fact]
    public void MathNetRandomAdapter_DelegatesEveryOverriddenMethodToSource()
    {
        const double scripted = 0.75;
        const byte expectedByte = 192; // (byte)(0.75 * 256)

        var source = new ScriptedRandomSource(scripted);
        var adapter = new MathNetRandomAdapter(source);

        // Each block asserts both the value derived from the scripted deviate and the number
        // of draws consumed so far, so an override that forwards twice is caught too.
        Assert.Equal(scripted, adapter.NextDouble());
        Assert.Equal(1, source.Calls);

        Assert.Equal((int)(scripted * int.MaxValue), adapter.Next());
        Assert.Equal(75, adapter.Next(100));
        Assert.Equal(75, adapter.Next(0, 100));
        Assert.Equal(4, source.Calls);

        Assert.Equal((long)(scripted * long.MaxValue), adapter.NextInt64());
        Assert.Equal(75L, adapter.NextInt64(100L));
        Assert.Equal(75L, adapter.NextInt64(0L, 100L));
        Assert.Equal(7, source.Calls);

        Assert.Equal(0.75f, adapter.NextSingle());
        Assert.Equal(8, source.Calls);

        // One draw per byte, so four bytes cost four draws each way.
        var bytes = new byte[4];
        adapter.NextBytes(bytes);
        Assert.All(bytes, b => Assert.Equal(expectedByte, b));
        Assert.Equal(12, source.Calls);

        var spanTarget = new byte[4];
        adapter.NextBytes(spanTarget.AsSpan());
        Assert.All(spanTarget, b => Assert.Equal(expectedByte, b));
        Assert.Equal(16, source.Calls);
    }

    // ---------- helpers ----------

    private static List<(string Name, IDistributionSampler A, IDistributionSampler B)> SamplerPairs()
    {
        static IDistributionSampler Make(DistributionSpec spec) =>
            DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed));

        return new List<(string, IDistributionSampler, IDistributionSampler)>
        {
            ("Exponential",
                Make(new DistributionSpec(DistributionFamily.Exponential, 3.0)),
                Make(new DistributionSpec(DistributionFamily.Exponential, 3.0))),

            ("Deterministic",
                Make(new DistributionSpec(DistributionFamily.Deterministic, 3.0)),
                Make(new DistributionSpec(DistributionFamily.Deterministic, 3.0))),

            ("Normal",
                Make(new DistributionSpec(DistributionFamily.Normal, 3.0, StdDev: 1.0)),
                Make(new DistributionSpec(DistributionFamily.Normal, 3.0, StdDev: 1.0))),

            ("Lognormal",
                Make(new DistributionSpec(DistributionFamily.Lognormal, 3.0, StdDev: 1.0)),
                Make(new DistributionSpec(DistributionFamily.Lognormal, 3.0, StdDev: 1.0))),

            ("Gamma",
                Make(new DistributionSpec(DistributionFamily.Gamma, 3.0, Shape: 2.0, Scale: 1.5)),
                Make(new DistributionSpec(DistributionFamily.Gamma, 3.0, Shape: 2.0, Scale: 1.5))),

            ("Uniform",
                Make(new DistributionSpec(DistributionFamily.Uniform, 3.0, Min: 1.0, Max: 5.0)),
                Make(new DistributionSpec(DistributionFamily.Uniform, 3.0, Min: 1.0, Max: 5.0)))
        };
    }

    private static double EmpiricalMean(IDistributionSampler sampler)
    {
        double sum = 0;
        for (int i = 0; i < SampleCount; i++)
            sum += sampler.NextSample();

        return sum / SampleCount;
    }

    private static double EmpiricalVariance(IDistributionSampler sampler)
    {
        double sum = 0;
        double sumSquares = 0;
        for (int i = 0; i < SampleCount; i++)
        {
            double x = sampler.NextSample();
            sum += x;
            sumSquares += x * x;
        }

        double mean = sum / SampleCount;
        return (sumSquares / SampleCount) - (mean * mean);
    }

    private static void AssertClose(double actual, double expected)
    {
        Assert.InRange(
            actual,
            expected * (1 - RelativeTolerance),
            expected * (1 + RelativeTolerance));
    }

    /// <summary>An <see cref="IRandomSource"/> that returns a fixed value and counts draws.</summary>
    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly double _value;

        public ScriptedRandomSource(double value) => _value = value;

        public int Calls { get; private set; }

        public void SetSeed(int seed) => Calls = 0;

        public double NextDouble()
        {
            Calls++;
            return _value;
        }
    }
}
