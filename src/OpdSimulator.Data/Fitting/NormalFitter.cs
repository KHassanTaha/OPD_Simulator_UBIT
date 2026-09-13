namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Fits a Normal distribution by maximum likelihood (FR-STAT-1).
/// </summary>
/// <remarks>
/// MLE: <c>μ̂ = x̄</c>, <c>σ̂ = sqrt(Σ(x−x̄)²/n)</c> — the variance is divided by
/// <c>n</c>, not <c>n−1</c>, because MLE (not sample variance) is the estimator
/// being reported; the chi-square test that follows treats parameters as fixed.
/// </remarks>
public sealed class NormalFitter : IDistributionFitter
{
    /// <inheritdoc />
    public string Name => "Normal";

    /// <inheritdoc />
    public FittedDistribution Fit(IReadOnlyList<double> samples)
    {
        if (samples.Count < 2)
            throw new ArgumentException("Cannot fit Normal to fewer than two samples.");

        double mean = samples.Average();
        double variance = samples.Sum(s => (s - mean) * (s - mean)) / samples.Count;
        double stddev = Math.Sqrt(variance);
        if (stddev <= 0)
            throw new ArgumentException("Normal fit requires at least two distinct samples.");

        var dist = new Normal(mean, stddev);
        double logLikelihood = samples.Sum(x => Math.Log(dist.Density(x)));

        var parameters = new Dictionary<string, double> { ["mean"] = mean, ["stddev"] = stddev };
        return new FittedDistribution("Normal", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}