namespace OpdSimulator.Data.Tests;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;

/// <summary>
/// Pins the protection <see cref="LognormalFitter"/> already had, so it cannot be lost.
/// </summary>
/// <remarks>
/// WHY this file exists even though no production code changed: the sibling defect fixed
/// in <see cref="GammaFitter"/> was a non-finite log-likelihood corrupting AIC, and
/// Lognormal is exposed to the same arithmetic — MathNet's <c>LogNormal.Density(0)</c>
/// is 0, so log(0) = −∞ and the likelihood sum would be −∞, giving AIC = +∞. Lognormal
/// does not need the same repair because it refuses the whole family when any sample
/// is non-positive, so the corrupted sum is never reached. That refusal was load-bearing
/// and undocumented as such; these tests make the guarantee explicit so a future
/// "optimisation" that aligns Lognormal with Gamma's exclude-and-fit behaviour has to
/// notice that it is removing a guard.
/// <para>
/// The two fitters' different behaviour is deliberate, not an inconsistency to be
/// averaged away: for Lognormal a zero is unrepresentable in the estimator itself
/// (ln 0 = −∞), whereas for Gamma the moment estimate survives the zero and only the
/// likelihood sum is corrupted. See the remarks in both fitters.
/// </para>
/// </remarks>
public class LognormalFitterTests
{
    [Fact]
    public void LognormalFitter_RefusesAnyNonPositiveSample()
    {
        // One zero among many positives is enough. The estimator takes ln x, and a
        // single ln 0 = -inf would make the whole fit meaningless, so the family is
        // refused rather than fitted on a subset.
        var samples = new List<double> { 1.0, 2.0, 3.0, 0.0, 4.0 };

        var error = Assert.Throws<ArgumentException>(() => new LognormalFitter().Fit(samples));

        Assert.Equal("Lognormal fit requires strictly positive samples.", error.Message);
    }

    [Fact]
    public void LognormalFitter_RefusesNegativeSample_ForTheSameReason()
    {
        var samples = new List<double> { 1.0, 2.0, -1.0, 4.0 };

        var error = Assert.Throws<ArgumentException>(() => new LognormalFitter().Fit(samples));

        Assert.Equal("Lognormal fit requires strictly positive samples.", error.Message);
    }

    [Fact]
    public void LognormalFitter_LogLikelihood_IsFinite_ForAllPositiveSample()
    {
        var samples = Enumerable.Range(1, 40).Select(i => 1.0 + (i % 5)).ToList();

        var fit = new LognormalFitter().Fit(samples);

        Assert.True(
            double.IsFinite(fit.LogLikelihood),
            $"Log-likelihood must be finite; was {fit.LogLikelihood}.");
    }

    [Fact]
    public void GeneralFitter_RejectsLognormal_WithTheNonPositiveReason_WhenZerosPresent()
    {
        // This is the path the G/G/c auto-fit actually takes: FitBest catches the
        // refusal and turns it into a candidate rejection, so the reason shown in the
        // badge comes from here.
        var samples = Enumerable.Range(1, 40).Select(i => 1.0 + (i % 5)).ToList();
        samples.Add(0.0);
        samples.Add(0.0);

        var result = GeneralDistributionFitter.FitBest(samples);

        var lognormal = result.AllCandidates.Single(c => c.Family == DistributionFamily.Lognormal);
        Assert.True(lognormal.Rejected);
        Assert.Equal("Lognormal fit requires strictly positive samples.", lognormal.RejectionReason);
    }

    [Fact]
    public void GeneralFitter_NoAcceptedCandidateReportsNonFiniteAic_WhenZerosPresent()
    {
        // The property the Gamma fix restored, asserted across the whole search so a
        // future family added to CandidateArray is covered by the same guarantee. Only
        // accepted candidates are scored; a rejected one carries NaN by design.
        var samples = Enumerable.Range(1, 40).Select(i => 1.0 + (i % 5)).ToList();
        samples.Add(0.0);
        samples.Add(0.0);

        var result = GeneralDistributionFitter.FitBest(samples);

        var accepted = result.AllCandidates.Where(c => !c.Rejected).ToList();
        Assert.NotEmpty(accepted);
        Assert.All(accepted, c =>
            Assert.True(
                double.IsFinite(c.Aic),
                $"{c.Family} was accepted with a non-finite AIC ({c.Aic})."));
    }
}
