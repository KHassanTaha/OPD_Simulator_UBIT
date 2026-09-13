namespace OpdSimulator.Data.Tests;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using OpdSimulator.Data.Fitting;
using Xunit;

public class FittingTests
{
    private static double[] SampleFrom(Func<RandomSource, double> draw, int count, int seed)
    {
        var rng = new MersenneTwister(seed);
        var values = new double[count];
        for (int i = 0; i < count; i++) values[i] = draw(rng);
        return values;
    }

    [Fact]
    public void Exponential_RecoversTrueRate_WithinFivePercent()
    {
        var samples = SampleFrom(rng => Exponential.Sample(rng, 0.5), 1000, 42);

        var fitted = new ExponentialFitter().Fit(samples);

        Assert.Equal("Exponential", fitted.Name);
        Assert.InRange(fitted.Parameters["rate"], 0.475, 0.525);
        Assert.Equal(1000, fitted.SampleSize);
    }

    [Fact]
    public void Normal_RecoversTrueMoments()
    {
        var samples = SampleFrom(rng => Normal.Sample(rng, 12.0, 2.5), 2000, 7);

        var fitted = new NormalFitter().Fit(samples);

        Assert.InRange(fitted.Parameters["mean"], 11.85, 12.15);
        Assert.InRange(fitted.Parameters["stddev"], 2.40, 2.60);
    }

    [Fact]
    public void Lognormal_RecoversMuSigma()
    {
        var samples = SampleFrom(rng => LogNormal.Sample(rng, 0.8, 0.4), 2000, 3);

        var fitted = new LognormalFitter().Fit(samples);

        Assert.InRange(fitted.Parameters["mu"], 0.75, 0.85);
        Assert.InRange(fitted.Parameters["sigma"], 0.36, 0.44);
    }

    [Fact]
    public void Gamma_MomentEstimatesMatchSampleMoments()
    {
        // Gamma(shape=4, rate=2) has mean 2, variance 1.
        var samples = SampleFrom(rng => Gamma.Sample(rng, 4.0, 2.0), 2000, 11);

        var fitted = new GammaFitter().Fit(samples);

        double shape = fitted.Parameters["shape"];
        double rate = fitted.Parameters["rate"];
        // Moments must be near the sample moments: shape/rate ≈ mean, shape/rate² ≈ variance.
        Assert.InRange(shape / rate, 1.9, 2.1);
        Assert.InRange(shape / (rate * rate), 0.9, 1.1);
    }

    [Fact]
    public void Uniform_UsesObservedRange()
    {
        var samples = new double[] { 1.5, 3.7, 2.2, 9.1, 4.4 };

        var fitted = new UniformFitter().Fit(samples);

        Assert.Equal(1.5, fitted.Parameters["min"], 6);
        Assert.Equal(9.1, fitted.Parameters["max"], 6);
    }

    [Fact]
    public void Factory_ResolvesEverySupportedName_CaseInsensitive()
    {
        foreach (string name in DistributionFitterFactory.SupportedNames)
        {
            Assert.True(DistributionFitterFactory.TryCreate(name.ToUpperInvariant(), out var fitter));
            Assert.Equal(name, fitter!.Name);
        }

        Assert.False(DistributionFitterFactory.TryCreate("Weibull", out _));
    }
}