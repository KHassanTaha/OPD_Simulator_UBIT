namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Fits a continuous Uniform distribution to the observed range (FR-STAT-1).
/// </summary>
/// <remarks>
/// <c>â = min(x)</c>, <c>b̂ = max(x)</c> — the MLE for a bounded-support family.
/// Included so the "uniform" distribution dropdown option (Milestone 5) has a
/// real implementation behind it and so the goodness-of-fit comparisons in the
/// fit report always have a baseline family to contrast against.
/// </remarks>
public sealed class UniformFitter : IDistributionFitter
{
    /// <inheritdoc />
    public string Name => "Uniform";

    /// <inheritdoc />
    public FittedDistribution Fit(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Cannot fit Uniform to an empty sample.");

        double min = samples.Min();
        double max = samples.Max();
        if (max <= min)
            throw new ArgumentException("Uniform fit requires at least two distinct sample values.");

        var dist = new ContinuousUniform(min, max);
        double logLikelihood = samples.Sum(x => Math.Log(dist.Density(x)));

        var parameters = new Dictionary<string, double> { ["min"] = min, ["max"] = max };
        return new FittedDistribution("Uniform", parameters, dist, dist.CumulativeDistribution, dist.InverseCumulativeDistribution, samples.Count, logLikelihood);
    }
}