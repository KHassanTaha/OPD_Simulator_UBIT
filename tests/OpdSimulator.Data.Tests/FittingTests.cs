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

    /// <summary>
    /// 8K / D-154: a spec must become a distribution whose CDF matches the numbers the
    /// user configured, because the whole verification verdict is computed from that CDF.
    /// These assert the CONFIGURED mean, not a refitted one — if FromSpec ever started
    /// fitting, the mean assertions below would drift onto the sample's mean and fail.
    /// </summary>
    [Fact]
    public void FromSpec_Exponential_HasTheConfiguredMeanNotASampleEstimate()
    {
        var samples = SampleFrom(rng => Normal.Sample(rng, 99.0, 1.0), 500, 1);

        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Exponential, Mean: 2.5),
            samples.Length);

        // P(X <= 2.5) for Exponential(mean 2.5) is 1 - e^-1. The absurd sample is
        // irrelevant on purpose: it proves nothing was fitted from it.
        Assert.Equal(1.0 - Math.Exp(-1.0), built.Cdf(2.5), 6);
        Assert.Equal(0.5, built.Cdf(2.5 * Math.Log(2.0)), 6);
    }

    [Fact]
    public void FromSpec_Normal_ConvertsTheConfiguredMeanAndStdDev()
    {
        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Normal, Mean: 4.0, StdDev: 2.0),
            100);

        Assert.Equal(0.5, built.Cdf(4.0), 6);
        Assert.Equal(0.8413447, built.Cdf(4.0 + 2.0), 5);
    }

    /// <summary>
    /// The Lognormal conversion is the one FromSpec derives itself rather than reading a
    /// single field, so it gets its own check: Var(ln X) = ln(1 + σ²/μ²). A silent error
    /// here would still "work" (the chi-square would run) while testing the wrong
    /// distribution, which is the worst possible failure for this factory.
    /// </summary>
    [Fact]
    public void FromSpec_Lognormal_MapsArithmeticSigmaOntoLogScaleSigma()
    {
        const double mean = 3.0;
        const double stdDev = 1.5;
        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Lognormal, Mean: mean, StdDev: stdDev),
            100);

        double sigmaOfLog = Math.Sqrt(Math.Log(1.0 + (stdDev * stdDev) / (mean * mean)));
        var expected = new LogNormal(Math.Log(mean) - (sigmaOfLog * sigmaOfLog / 2.0), sigmaOfLog);

        Assert.Equal(expected.CumulativeDistribution(2.0), built.Cdf(2.0), 9);
        Assert.Equal(expected.CumulativeDistribution(5.0), built.Cdf(5.0), 9);
        // The arithmetic mean is the median reference point: P(X <= mean) sits above 0.5
        // for a right-skewed log-normal, which the sample-free construction preserves.
        Assert.True(built.Cdf(mean) > 0.5, $"P(X<={mean}) should exceed 0.5; was {built.Cdf(mean)}");
    }

    [Fact]
    public void FromSpec_Gamma_UsesShapeAsSpreadAndDerivesScaleFromTheMean()
    {
        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Gamma, Mean: 6.0, Shape: 3.0),
            100);

        // Gamma(shape 3, scale 2) is the reference. The mean of 6 is reproduced only if
        // scale = mean/shape was applied, so the CDFs must agree across the range.
        var reference = new Gamma(3.0, 2.0);
        foreach (double x in new[] { 1.0, 4.0, 6.0, 9.0, 15.0 })
        {
            Assert.Equal(reference.CumulativeDistribution(x), built.Cdf(x), 9);
        }

        Assert.Equal(2.0, built.Parameters["scale"], 9);
        Assert.Equal(3.0, built.Parameters["shape"], 9);
    }

    [Fact]
    public void FromSpec_Uniform_UsesTheConfiguredBounds()
    {
        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Uniform, Mean: 5.0, Min: 2.0, Max: 8.0),
            100);

        var reference = new ContinuousUniform(2.0, 8.0);
        foreach (double x in new[] { 0.0, 2.0, 3.5, 5.0, 8.0, 12.0 })
        {
            Assert.Equal(reference.CumulativeDistribution(x), built.Cdf(x), 9);
        }

        // The bounds must be honoured exactly, including the clamping outside them —
        // an unbounded CDF here would put all of the tail mass in the last chi-square bin.
        Assert.Equal(0.0, built.Cdf(2.0), 9);
        Assert.Equal(1.0, built.Cdf(8.0), 9);
        Assert.Equal(0.5, built.Cdf(5.0), 6);
    }

    /// <summary>
    /// A spec-derived distribution was not estimated from any sample, so it has no
    /// likelihood. NaN is this codebase's "no value" sentinel (a rejected candidate
    /// carries a NaN AIC), and the verification path must never read it.
    /// </summary>
    [Fact]
    public void FromSpec_CarriesNoLikelihood_BecauseNothingWasFitted()
    {
        var built = FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Exponential, Mean: 2.0),
            42);

        Assert.True(double.IsNaN(built.LogLikelihood));
        Assert.True(double.IsNaN(built.AIC));
        Assert.Equal(42, built.SampleSize);
    }

    [Fact]
    public void FromSpec_RejectsAnUnusableSpec_WithAReasonNamingTheMissingField()
    {
        var noSigma = Assert.Throws<ArgumentException>(() => FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Normal, Mean: 2.0),
            10));
        Assert.Contains("standard deviation", noSigma.Message);

        var noShape = Assert.Throws<ArgumentException>(() => FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Gamma, Mean: 2.0),
            10));
        Assert.Contains("shape", noShape.Message);

        var badMean = Assert.Throws<ArgumentException>(() => FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Exponential, Mean: 0.0),
            10));
        Assert.Contains("mean", badMean.Message);

        var badBounds = Assert.Throws<ArgumentException>(() => FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Uniform, Mean: 5.0, Min: 8.0, Max: 2.0),
            10));
        Assert.Contains("Max > Min", badBounds.Message);
    }

    [Fact]
    public void FromSpec_Deterministic_RefusesBecauseAConstantHasNoDistributionToTest()
    {
        var ex = Assert.Throws<ArgumentException>(() => FittedDistribution.FromSpec(
            new Core.Distributions.DistributionSpec(
                Core.Distributions.DistributionFamily.Deterministic, Mean: 2.0),
            100));

        Assert.Contains("Deterministic", ex.Message);
    }

    [Fact]
    public void FromSpec_RejectsANullSpec()
    {
        Assert.Throws<ArgumentNullException>(() => FittedDistribution.FromSpec(null!, 10));
    }
}
