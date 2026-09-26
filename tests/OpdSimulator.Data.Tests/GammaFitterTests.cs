namespace OpdSimulator.Data.Tests;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;

/// <summary>
/// Guards the log-likelihood repair in <see cref="GammaFitter"/>.
/// </summary>
/// <remarks>
/// WHY these tests exist: Gamma's density diverges at x = 0 whenever the fitted shape
/// k &lt; 1, so log pdf(0) = +∞. AIC is 2k − 2·ln L, which made a single zero service
/// time in an otherwise ordinary sample produce AIC = −∞. Because −∞ beats every real
/// number, Gamma then won the <c>G/G/c</c> family search on any dataset containing a
/// zero — a verdict decided by arithmetic rather than by fit quality. Found on
/// samples/sample_patients.csv, where 60 screening services include zeros.
/// <para>
/// Every test below asserts a property a future edit could plausibly break: that the
/// reported likelihood and the AIC derived from it are FINITE, and that the family
/// search is decided by AIC rather than by a sentinel. A test that merely asserted
/// "Gamma is returned" would have passed with the bug in place, because Gamma was in
/// fact being returned — just for the wrong reason.
/// </para>
/// </remarks>
public class GammaFitterTests
{
    /// <summary>
    /// A sample shaped like the real one: mostly short services with a few zeros in
    /// it, which is what makes the moment-matched shape fall below 1 and the density
    /// diverge at the origin. 60 points, matching samples/sample_patients.csv.
    /// </summary>
    private static List<double> SampleWithZeros(int count, int zeros)
    {
        var values = new List<double>();
        for (int i = 0; i < count - zeros; i++)
        {
            values.Add(1.0 + (i % 5));
        }

        for (int i = 0; i < zeros; i++)
        {
            values.Add(0.0);
        }

        return values;
    }

    [Fact]
    public void GammaFitter_LogLikelihood_IsFinite_ForSampleContainingZeros()
    {
        var samples = SampleWithZeros(60, 8);

        var fit = new GammaFitter().Fit(samples);

        Assert.True(
            double.IsFinite(fit.LogLikelihood),
            $"Log-likelihood must be finite; was {fit.LogLikelihood}. A non-finite value is the bug this test guards.");
    }

    [Fact]
    public void GeneralFitter_Aic_IsFinite_ForEveryAcceptedCandidate_WhenSampleContainsZeros()
    {
        // The observable consequence: any candidate that COMPETES in the search must
        // report a finite AIC, because FitBest picks the lowest one and a -inf would
        // win regardless of fit quality.
        //
        // Scoped to accepted candidates on purpose. A rejected candidate has no fit, so
        // its AIC is NaN by design and ChooseBest never reads it — asserting across all
        // candidates would be asserting that a rejected fit has a score, which is not
        // the contract. (That distinction was found by this test failing first.)
        var samples = SampleWithZeros(60, 8);

        var result = GeneralDistributionFitter.FitBest(samples);

        var accepted = result.AllCandidates.Where(c => !c.Rejected).ToList();
        Assert.NotEmpty(accepted);
        Assert.All(accepted, c =>
            Assert.True(
                double.IsFinite(c.Aic),
                $"{c.Family} was accepted with a non-finite AIC ({c.Aic}); that decides the search by arithmetic, not fit."));
    }

    [Fact]
    public void GammaFitter_Aic_IsNotMinusInfinity_SoGammaCannotWinEverySearch()
    {
        // Pin the exact regression: before the repair this was -inf, which is lower
        // than every real AIC, so Gamma won against Exponential/Normal/Uniform on any
        // zero-containing sample no matter how badly it fitted.
        var samples = SampleWithZeros(60, 8);

        var gamma = new GammaFitter().Fit(samples);

        // FittedDistribution computes AIC = 2k - 2*LogLikelihood in its constructor,
        // so this asserts the value the family search actually reads.
        Assert.True(double.IsFinite(gamma.AIC), $"AIC must be finite; was {gamma.AIC}.");
    }

    [Fact]
    public void GammaFitter_ExcludesZerosFromLikelihood_ButKeepsFullSampleForMomentMatch()
    {
        // The repair is deliberately narrow: the moment estimate is still taken from
        // every sample, and only the corrupted log-likelihood sum drops the zeros. If a
        // future edit "helpfully" switches the moment match to the positive subset
        // instead, the parameters would change and this test fails.
        var samples = SampleWithZeros(60, 8);

        var fit = new GammaFitter().Fit(samples);

        var mean = samples.Average();
        var variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Count;
        var expectedShape = (mean * mean) / variance;

        Assert.Equal(60, fit.SampleSize); // every sample still counts toward the moment match.
        Assert.Equal(expectedShape, fit.Parameters["shape"], 12);
    }

    [Fact]
    public void GammaFitter_TooFewPositiveSamples_RejectsWithNamedReason()
    {
        // 20 zeros among 40 samples: 20 positive survive, which is exactly the
        // threshold, so the fit proceeds. One more zero must cross it and be refused
        // with the reason string the search reports to the user.
        var atThreshold = SampleWithZeros(40, 20);
        var fit = new GammaFitter().Fit(atThreshold);
        Assert.True(double.IsFinite(fit.LogLikelihood));

        var belowThreshold = SampleWithZeros(40, 21);
        var error = Assert.Throws<ArgumentException>(() => new GammaFitter().Fit(belowThreshold));
        Assert.Equal("Not enough positive samples (need ≥ 20, have 19).", error.Message);
    }

    [Fact]
    public void GeneralFitter_ReportsNotEnoughPositiveSamples_AsTheRejectionReason()
    {
        // The reason must reach the user through the family search, not just the
        // exception, because the auto-fit badge shows this text verbatim.
        var samples = SampleWithZeros(40, 21);

        var result = GeneralDistributionFitter.FitBest(samples);

        var gamma = result.AllCandidates.Single(c => c.Family == DistributionFamily.Gamma);
        Assert.True(gamma.Rejected);
        Assert.Equal("Not enough positive samples (need ≥ 20, have 19).", gamma.RejectionReason);
    }

    [Fact]
    public void GammaFitter_AllPositiveSample_IsUnchangedByTheRepair()
    {
        // Guard against over-correction: with no zeros to exclude, the fit must be
        // bit-identical to what it produced before the repair, so no previously
        // correct Gamma fit moves.
        var samples = Enumerable.Range(1, 40).Select(i => 1.0 + (i % 7)).ToList();

        var fit = new GammaFitter().Fit(samples);

        Assert.Equal(40, fit.SampleSize);
        Assert.True(double.IsFinite(fit.LogLikelihood));
        Assert.True(double.IsFinite(fit.AIC));

        // Recomputing the naive all-samples sum must give the same answer, since the
        // exclusion removed nothing.
        var mean = samples.Average();
        var variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Count;
        var expected = new MathNet.Numerics.Distributions.Gamma(
            (mean * mean) / variance, mean / variance);
        var naive = samples.Sum(x => Math.Log(expected.Density(x)));
        Assert.Equal(naive, fit.LogLikelihood, 10);
    }
}
