using OpdSimulator.Core.Distributions;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="ExponentialSampler"/>: E[X] = 1/λ, samples non-negative,
/// and the rate must be strictly positive.
/// </summary>
public class ExponentialSamplerTests
{
    [Fact]
    public void Sample_RateMustBePositive()
    {
        var sampler = new ExponentialSampler(new SeededRandomSource(42));
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.Sample(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.Sample(-1.5));
    }

    [Fact]
    public void Sample_MeanApproachesOneOverRate()
    {
        const double rate = 4.0; // expected mean 0.25 minutes
        const int n = 100_000;
        var sampler = new ExponentialSampler(new SeededRandomSource(42));

        double sum = 0;
        for (int i = 0; i < n; i++)
            sum += sampler.Sample(rate);

        double mean = sum / n;
        const double tolerance = 0.02; // within 2% of analytical mean
        Assert.InRange(mean, (1.0 / rate) * (1 - tolerance), (1.0 / rate) * (1 + tolerance));
    }

    [Fact]
    public void Sample_IsNeverNegative()
    {
        const double rate = 0.5;
        var sampler = new ExponentialSampler(new SeededRandomSource(1));
        for (int i = 0; i < 10_000; i++)
            Assert.True(sampler.Sample(rate) >= 0, "Exponential samples must be non-negative.");
    }
}