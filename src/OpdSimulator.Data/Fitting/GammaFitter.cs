namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Fits a Gamma distribution by the method of moments (FR-STAT-1).
/// </summary>
/// <remarks>
/// MathNet's <see cref="Gamma"/> is parameterized as shape–rate. Equating moments:
/// <c>E[X] = shape/rate = x̄</c> and <c>Var[X] = shape/rate²</c>, so
/// <c>shape = x̄²/Var</c> and <c>rate = x̄/Var</c>. Moment matching is used instead
/// of MLE because Gamma MLE has no closed form (it needs numeric optimisation);
/// the kickoff permits MoM for this family (D-…).
/// </remarks>
public sealed class GammaFitter : IDistributionFitter
{
    /// <inheritdoc />
    public string Name => "Gamma";

    /// <inheritdoc />
    public FittedDistribution Fit(IReadOnlyList<double> samples)
    {
        if (samples.Count < 2)
            throw new ArgumentException("Cannot fit Gamma to fewer than two samples.");

        double mean = samples.Average();
        double variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Count;
        if (variance <= 0)
            throw new ArgumentException("Gamma fit requires at least two distinct samples (variance must be > 0).");

        double shape = (mean * mean) / variance;
        double rate = mean / variance;
        if (shape <= 0 || rate <= 0)
            throw new ArgumentException("Gamma moment-matching produced non-positive parameters.");

        var dist = new Gamma(shape, rate);

        // The likelihood is summed over the strictly positive samples only.
        //
        // Gamma's density is supported on x > 0 and DIVERGES at x = 0 whenever the
        // fitted shape k < 1: pdf(0) = +∞, so log pdf(0) = +∞ and a single zero in an
        // otherwise ordinary sample makes the whole log-likelihood +∞. AIC is
        // 2k − 2·ln L, so ln L = +∞ makes AIC = −∞, and because −∞ beats every real
        // number, Gamma would then win the family search on any dataset that happens
        // to contain one zero — regardless of how well it actually fits. Observed on
        // samples/sample_patients.csv, whose 60 screening services include zeros.
        //
        // Excluding the zeros from the sum is a numerical repair, not a modelling
        // claim: a zero-minute service is a data-quality artefact, and the alternative
        // is a verdict that is decided by arithmetic accident. Note the deliberate
        // asymmetry with LognormalFitter, which refuses the whole family on a
        // non-positive sample instead: there the zero is unrepresentable in the
        // estimator itself (ln 0 = −∞), whereas here the moment estimate above is
        // still computed from the full sample and only the corrupted sum is repaired.
        //
        // Consequence to state in the viva: Gamma's ln L is therefore a sum over fewer
        // terms than the other candidates', so AIC values are not perfectly comparable
        // across families when the sample contains zeros. This is strictly better than
        // the previous −∞, and the exclusion is reported rather than hidden.
        double[] positive = samples.Where(x => x > 0).ToArray();
        if (positive.Length < GeneralDistributionFitter.MinimumSampleCount)
        {
            throw new ArgumentException(
                $"Not enough positive samples (need ≥ {GeneralDistributionFitter.MinimumSampleCount}, have {positive.Length}).");
        }

        double logLikelihood = positive.Sum(x => Math.Log(dist.Density(x)));

        // A finite sum can still overflow to ±∞ (or pick up a NaN) if a density
        // underflows to 0 in a far tail. That would again produce a non-finite AIC, so
        // it is refused here with its own reason rather than being allowed to decide
        // the family search.
        if (double.IsNaN(logLikelihood) || double.IsInfinity(logLikelihood))
        {
            throw new ArgumentException("Log-likelihood is non-finite (numerator collapse).");
        }

        var parameters = new Dictionary<string, double> { ["shape"] = shape, ["rate"] = rate };
        return new FittedDistribution("Gamma", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}