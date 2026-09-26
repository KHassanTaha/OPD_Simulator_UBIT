using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;

namespace OpdSimulator.Data.Tests;

/// <summary>
/// Tests for <see cref="GeneralDistributionFitter"/>: the auto-fit path that picks a
/// family from data instead of trusting a user-picked one.
/// </summary>
/// <remarks>
/// <para>
/// Synthetic samples are drawn with the Phase 8G Core samplers at seed 42, so every
/// assertion here is deterministic: the tests cannot flake, they either always pass or
/// always fail.
/// </para>
/// <para>
/// The recovery tests assert two things per family: that AIC ranks the generating
/// family first, and that the winner also survives its own chi-square sanity check.
/// The second half matters — a family can win on likelihood while the data still
/// contradicts it, and a winner that fails its own goodness-of-fit test would mean the
/// ranking is picking an illusion.
/// </para>
/// </remarks>
public class GeneralDistributionFitterTests
{
    private const int Seed = 42;
    private const int SampleCount = 5_000;

    // ---------- synthetic-family recovery ----------

    [Fact]
    public void FitBest_ExponentialSamples_SelectsExponential()
        => AssertRecovers(new DistributionSpec(DistributionFamily.Exponential, 4.0), DistributionFamily.Exponential);

    [Fact]
    public void FitBest_NormalSamples_SelectsNormal()
        => AssertRecovers(new DistributionSpec(DistributionFamily.Normal, 10.0, StdDev: 2.0), DistributionFamily.Normal);

    [Fact]
    public void FitBest_LognormalSamples_SelectsLognormal()
        => AssertRecovers(new DistributionSpec(DistributionFamily.Lognormal, 5.0, StdDev: 1.5), DistributionFamily.Lognormal);

    [Fact]
    public void FitBest_GammaSamples_SelectsGamma()
        => AssertRecovers(new DistributionSpec(DistributionFamily.Gamma, 3.0, Shape: 2.0, Scale: 1.5), DistributionFamily.Gamma);

    [Fact]
    public void FitBest_UniformSamples_SelectsUniform()
        => AssertRecovers(new DistributionSpec(DistributionFamily.Uniform, 6.0, Min: 2.0, Max: 10.0), DistributionFamily.Uniform);

    // ---------- rejection rules ----------

    [Fact]
    public void FitBest_AllCandidatesRejectedWhenSamplesLessThan20_ReturnsNullBest()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 10));

        Assert.Null(result.Best);
        Assert.Equal(5, result.AllCandidates.Count);
        Assert.All(result.AllCandidates, c => Assert.True(c.Rejected));
        Assert.All(result.AllCandidates,
            c => Assert.Equal("insufficient samples (< 20)", c.RejectionReason));
    }

    /// <summary>
    /// Pins the literal "&lt; 5 expected bin count" rule and the sample sizes where it
    /// actually fires.
    /// </summary>
    /// <remarks>
    /// ChiSquareTest bins by equal probability, so every bin expects exactly n/bins and
    /// the minimum is that same number. With bins = clamp(ceil(√n), 5, 20) the rule
    /// therefore bites at n ∈ {20…24} (n/5 &lt; 5) and n ∈ {26…29} (n/6 &lt; 5), while
    /// n = 25 and n = 30 land on exactly 5.0 and survive. This is a property of
    /// equal-probability binning rather than a defect, and the pair of tests below fix
    /// the boundary in place so a future change to the bin rule cannot move it silently.
    /// </remarks>
    [Fact]
    public void FitBest_SmallSample_ExpectedBinCountBelow5_RejectsEveryCandidate()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 20));

        Assert.Null(result.Best);
        Assert.All(result.AllCandidates, c => Assert.True(c.Rejected));
        Assert.All(result.AllCandidates,
            c => Assert.Equal("expected bin count below 5", c.RejectionReason));

        // The fit itself was fine, so the diagnostics survive the rejection.
        Assert.All(result.AllCandidates, c => Assert.True(double.IsFinite(c.Aic)));
        Assert.All(result.AllCandidates, c => Assert.True(double.IsFinite(c.Bic)));
    }

    [Fact]
    public void FitBest_SampleCount25_ExpectedBinCountExactly5_Survives()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 25));

        Assert.NotNull(result.Best);
        Assert.DoesNotContain(result.AllCandidates,
            c => c.RejectionReason == "expected bin count below 5");
    }

    [Fact]
    public void FitBest_ReturnsAllCandidates_RegardlessOfWinner()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 1_000));

        // Every candidate is reported, in the declared evaluation order, even though only
        // one wins — the losers are the evidence for the winner.
        Assert.Equal(GeneralDistributionFitter.Candidates, result.AllCandidates.Select(c => c.Family));
    }

    [Fact]
    public void FitBest_AllCandidatesHaveBothAicAndBic()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 1_000));

        Assert.All(result.AllCandidates, c =>
        {
            Assert.True(double.IsFinite(c.Aic), $"{c.Family}: AIC must be finite, was {c.Aic}.");
            Assert.True(double.IsFinite(c.Bic), $"{c.Family}: BIC must be finite, was {c.Bic}.");
            Assert.True(double.IsFinite(c.LogLikelihood));
        });
    }

    // ---------- AIC ordering ----------

    [Fact]
    public void FitBest_AicOrdering_LowerIsBetter_MatchesManualCalculation()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 1_000));

        var best = Assert.IsType<DistributionFitResult>(result.Best);

        // k = 1 for Exponential, so AIC must be exactly 2 − 2·ln L.
        Assert.Equal(2.0 - (2.0 * best.LogLikelihood), best.Aic, 6);

        // "Lower is better" is a claim about the ranking, not just the number.
        var viable = result.AllCandidates.Where(c => !c.Rejected).ToList();
        Assert.Equal(best.Aic, viable.Min(c => c.Aic));

        // BIC = k·ln n − 2·ln L, with n = 1000.
        Assert.Equal(Math.Log(1_000) - (2.0 * best.LogLikelihood), best.Bic, 6);
    }

    // ---------- candidate set ----------

    [Fact]
    public void Candidates_DoesNotContainDeterministic()
    {
        Assert.Equal(
            new[]
            {
                DistributionFamily.Exponential,
                DistributionFamily.Normal,
                DistributionFamily.Lognormal,
                DistributionFamily.Gamma,
                DistributionFamily.Uniform,
            },
            GeneralDistributionFitter.Candidates);

        Assert.DoesNotContain(DistributionFamily.Deterministic, GeneralDistributionFitter.Candidates);
    }

    /// <summary>
    /// Pins the placeholder spec carried by a candidate that never produced a fit.
    /// </summary>
    /// <remarks>
    /// <see cref="DistributionFitResult.Spec"/> is non-nullable, but a candidate rejected
    /// before fitting has no parameters. The fitter uses NaN so the placeholder cannot be
    /// mistaken for an estimate; changing it to 0 would make a rejected candidate's spec
    /// look like a real zero-valued fit, so the value is asserted here.
    /// </remarks>
    [Fact]
    public void FitBest_RejectedBeforeFit_CarriesPlaceholderSpecWithNaNMean()
    {
        var result = GeneralDistributionFitter.FitBest(Draw(
            new DistributionSpec(DistributionFamily.Exponential, 4.0), 5));

        Assert.Null(result.Best);
        Assert.All(result.AllCandidates, c =>
        {
            Assert.True(c.Rejected);
            Assert.Equal(c.Family, c.Spec.Family); // the family is still identified
            Assert.True(double.IsNaN(c.Spec.Mean)); // but it holds no estimate
            Assert.True(double.IsNaN(c.Aic));
            Assert.True(double.IsNaN(c.LogLikelihood));
        });
    }

    // ---------- helpers ----------

    /// <summary>
    /// Asserts that samples drawn from <paramref name="generator"/> are attributed to
    /// <paramref name="expected"/>, and that the winner passes its own chi-square.
    /// </summary>
    private static void AssertRecovers(DistributionSpec generator, DistributionFamily expected)
    {
        var result = GeneralDistributionFitter.FitBest(Draw(generator, SampleCount));

        var best = Assert.IsType<DistributionFitResult>(result.Best);
        Assert.Equal(expected, best.Family);

        // The ranking must genuinely be an ordering, not a lucky first entry.
        var viable = result.AllCandidates.Where(c => !c.Rejected).ToList();
        Assert.Equal(best.Aic, viable.Min(c => c.Aic));

        // A winner that fails its own goodness-of-fit test would be an illusion.
        var chiSquare = Assert.IsType<ChiSquareResult>(best.ChiSquare);
        Assert.True(chiSquare.PValue > chiSquare.Alpha,
            $"{expected} won on AIC but its chi-square p-value {chiSquare.PValue:0.####} " +
            $"does not exceed alpha {chiSquare.Alpha}.");
    }

    /// <summary>Draws <paramref name="count"/> samples from the Core sampler for a spec.</summary>
    private static List<double> Draw(DistributionSpec spec, int count)
    {
        var sampler = DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed));
        var samples = new List<double>(count);
        for (int i = 0; i < count; i++)
            samples.Add(sampler.NextSample());

        return samples;
    }
}
