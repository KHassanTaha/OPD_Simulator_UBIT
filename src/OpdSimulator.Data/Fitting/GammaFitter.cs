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
        double logLikelihood = samples.Sum(x => Math.Log(dist.Density(x)));

        var parameters = new Dictionary<string, double> { ["shape"] = shape, ["rate"] = rate };
        return new FittedDistribution("Gamma", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}