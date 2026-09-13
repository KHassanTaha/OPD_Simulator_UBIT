namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Fits a Log-normal distribution by maximum likelihood on log(x) (FR-STAT-1).
/// </summary>
/// <remarks>
/// If X ~ LogNormal(μ, σ) then ln X ~ Normal(μ, σ), so the MLE is simply the
/// Normal MLE applied to the logarithms of the samples. Requires strictly positive
/// <c>x</c>; negative or zero service/inter-arrival times make the log undefined.
/// </remarks>
public sealed class LognormalFitter : IDistributionFitter
{
    /// <inheritdoc />
    public string Name => "Lognormal";

    /// <inheritdoc />
    public FittedDistribution Fit(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Cannot fit Lognormal to an empty sample.");
        if (samples.Any(x => x <= 0))
            throw new ArgumentException("Lognormal fit requires strictly positive samples.");

        double[] logs = samples.Select(x => Math.Log(x)).ToArray();
        double mu = logs.Average();
        double sigmaSq = logs.Sum(x => (x - mu) * (x - mu)) / logs.Length;
        double sigma = Math.Sqrt(sigmaSq);
        if (sigma <= 0)
            throw new ArgumentException("Lognormal fit requires at least two distinct sample values.");

        var dist = new LogNormal(mu, sigma);
        double logLikelihood = samples.Sum(x => Math.Log(dist.Density(x)));

        var parameters = new Dictionary<string, double> { ["mu"] = mu, ["sigma"] = sigma };
        return new FittedDistribution("Lognormal", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}