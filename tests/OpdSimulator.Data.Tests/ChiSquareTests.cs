namespace OpdSimulator.Data.Tests;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;
using OpdSimulator.Data.Fitting;
using Xunit;

public class ChiSquareTests
{
    [Fact]
    public void BinCount_FollowsSquareRootRule_ClampedToFiveAndTwenty()
    {
        Assert.Equal(5, BinSelector.BinCount(4));     // ceil(√4)=2 → clamp up to 5
        Assert.Equal(8, BinSelector.BinCount(60));    // ceil(√60)=8
        Assert.Equal(20, BinSelector.BinCount(10000)); // ceil(√10000)=100 → clamp down to 20
    }

    [Fact]
    public void ObservedFrequencies_EverySampleFallsInABin()
    {
        var samples = new[] { 1.0, 2.0, 3.0, 9.0, 10.0 };
        var edges = new[] { 1.0, 4.0, 7.0, 10.0 };

        int[] counts = BinSelector.ObservedFrequencies(samples, edges);

        Assert.Equal(new[] { 3, 0, 2 }, counts);
        Assert.Equal(samples.Length, counts.Sum());
    }

    [Fact]
    public void ExpData_FittedToExponential_IsNotRejected()
    {
        var samples = new Exponential(0.5) { RandomSource = new MersenneTwister(9) }
            .Samples()
            .Take(2000)
            .ToArray();
        var fitted = new ExponentialFitter().Fit(samples);

        var result = ChiSquareTest.Run(samples, fitted, 0.05);

        Assert.False(result.RejectFit);
        Assert.Equal(20, result.Observed.Count);   // 2000 samples → 20 bins (clamped)
        Assert.Equal(18, result.DegreesOfFreedom);  // k − 1 − p = 20 − 1 − 1
        Assert.Equal(samples.Length, result.Observed.Sum());
        Assert.Equal(samples.Length, result.Expected.Sum());
    }

    [Fact]
    public void UniformData_FittedToExponential_IsRejected()
    {
        // Uniform data is dramatically non-exponential: the statistic is huge and
        // the test must say so rather than report an "acceptable" fit.
        var samples = new ContinuousUniform(0.0, 10.0) { RandomSource = new MersenneTwister(11) }
            .Samples()
            .Take(500)
            .ToArray();
        var fitted = new ExponentialFitter().Fit(samples); // mean 5 ⇒ rate 0.2 — a bad fit

        var result = ChiSquareTest.Run(samples, fitted, 0.05);

        Assert.True(result.RejectFit);
        Assert.True(result.Statistic > 20);
    }
}