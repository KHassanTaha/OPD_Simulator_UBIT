namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Fits an Exponential distribution by maximum likelihood (FR-STAT-1).
/// </summary>
/// <remarks>
/// For Exp(λ) the MLE is <c>λ̂ = 1/x̄</c>. The parameter name is <c>rate</c>
/// because MathNet's <see cref="Exponential"/> constructor takes the rate — this
/// keeps the fitted object directly usable as an engine parameter (λ).</remarks>
public sealed class ExponentialFitter : IDistributionFitter
{
    /// <inheritdoc />
    public string Name => "Exponential";

    /// <inheritdoc />
    public FittedDistribution Fit(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Cannot fit Exponential to an empty sample.");
        double mean = samples.Average();
        if (mean <= 0)
            throw new ArgumentException("Exponential fit requires strictly positive samples (mean must be > 0).");

        double rate = 1.0 / mean;
        var dist = new Exponential(rate);

        double logLikelihood = samples.Sum(x => Math.Log(dist.Density(x)));
        var parameters = new Dictionary<string, double> { ["rate"] = rate };

        return new FittedDistribution("Exponential", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}