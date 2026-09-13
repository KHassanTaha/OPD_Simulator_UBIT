namespace OpdSimulator.Data.Fitting;

/// <summary>
/// A statistical distribution family that can be fitted to a sample (FR-STAT-1).
/// </summary>
/// <remarks>
/// One implementation per supported family (Exponential, Normal, Log-normal,
/// Gamma, Uniform). Fitting is MLE or method-of-moments (see each fitter's docs);
/// the returned <see cref="FittedDistribution"/> bundles parameters, likelihood
/// and AIC so downstream consumers and the chi-square test need no special cases
/// per family.
/// </remarks>
public interface IDistributionFitter
{
    /// <summary>Friendly name of the distribution family (also the CLI value).</summary>
    string Name { get; }

    /// <summary>
    /// Fits the distribution to the sample.
    /// </summary>
    /// <param name="samples">Strictly positive observations, e.g. service times in minutes.</param>
    /// <returns>The fitted distribution.</returns>
    /// <exception cref="ArgumentException">If the sample is unusable for this family
    /// (empty, non-positive for log-normal, degenerate variance for gamma).</exception>
    FittedDistribution Fit(IReadOnlyList<double> samples);
}