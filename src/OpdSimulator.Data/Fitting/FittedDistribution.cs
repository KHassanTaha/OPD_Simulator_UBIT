namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// A fitted continuous distribution and the diagnostics that describe how well it fits.
/// </summary>
/// <remarks>
/// MathNet's <see cref="IContinuousDistribution"/> interface exposes densities and
/// sampling but <em>not</em> the CDF (that lives on an internal base class). We
/// therefore capture the CDF and its inverse as delegates at construction time,
/// where the concrete distribution type is known — the equality-probability
/// chi-square binning needs exactly those two functions (FR-STAT-3).
/// </remarks>
public sealed class FittedDistribution
{
    /// <summary>
    /// Creates a fitted distribution.
    /// </summary>
    /// <param name="name">Family name (also the CLI value).</param>
    /// <param name="parameters">Estimated parameters, name → value.</param>
    /// <param name="distribution">The MathNet instance (used for densities/sampling).</param>
    /// <param name="cdf">The distribution's cumulative density function P(X ≤ x).</param>
    /// <param name="inverseCdf">The quantile function, inverse of <paramref name="cdf"/>.</param>
    /// <param name="sampleSize">Number of samples the fit used.</param>
    /// <param name="logLikelihood">In-sample log-likelihood.</param>
    public FittedDistribution(
        string name,
        IReadOnlyDictionary<string, double> parameters,
        IContinuousDistribution distribution,
        Func<double, double> cdf,
        Func<double, double> inverseCdf,
        int sampleSize,
        double logLikelihood)
    {
        Name = name;
        Parameters = parameters;
        Distribution = distribution;
        Cdf = cdf;
        InverseCdf = inverseCdf;
        SampleSize = sampleSize;
        LogLikelihood = logLikelihood;
        AIC = 2 * parameters.Count - 2 * logLikelihood; // AIC = 2k − 2·LL
    }

    /// <summary>Distribution name as shown in the UI and export (e.g. "Exponential").</summary>
    public string Name { get; }

    /// <summary>Estimated parameters in stable order, name → value (e.g. "rate" → 0.5).</summary>
    public IReadOnlyDictionary<string, double> Parameters { get; }

    /// <summary>The MathNet distribution instance (densities and sampling).</summary>
    public IContinuousDistribution Distribution { get; }

    /// <summary>P(X ≤ x) for the fitted parameters.</summary>
    public Func<double, double> Cdf { get; }

    /// <summary>Quantile function x such that P(X ≤ x) = p.</summary>
    public Func<double, double> InverseCdf { get; }

    /// <summary>Number of samples the fit used.</summary>
    public int SampleSize { get; }

    /// <summary>In-sample log-likelihood (always ≤ 0 for continuous data).</summary>
    public double LogLikelihood { get; }

    /// <summary>Akaike information criterion = 2k − 2·LL; penalises extra parameters.</summary>
    public double AIC { get; }

    /// <summary>
    /// Renders parameters as a compact, human- and export-friendly string.
    /// </summary>
    /// <returns>e.g. <c>rate = 0.5000</c>.</returns>
    public string ParametersText()
        => string.Join(", ", Parameters.Select(kvp => $"{kvp.Key} = {kvp.Value:0.###}"));
}