namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Draws samples from a single probability distribution and evaluates its density
/// and cumulative distribution.
/// </summary>
/// <remarks>
/// One sampler represents one distribution family with fixed parameters. Families
/// that require a shape parameter (Gamma) encode it in the sampler's constructor
/// and are therefore distinct instances rather than runtime arguments to
/// <see cref="NextSample"/>.
/// <para>
/// The contract is intentionally small: mean, variance, next sample, density, CDF.
/// This keeps the engine free of per-family branching.
/// </para>
/// </remarks>
public interface IDistributionSampler
{
    /// <summary>Theoretical mean of the distribution, E[X].</summary>
    double Mean { get; }

    /// <summary>Theoretical variance of the distribution, Var[X].</summary>
    double Variance { get; }

    /// <summary>Draws the next random variate from this distribution.</summary>
    /// <returns>A single sample. Implementations must not use a shared or global
    /// random source; all randomness comes from the injected source.</returns>
    double NextSample();

    /// <summary>Probability density (or mass) at <paramref name="x"/>.</summary>
    /// <param name="x">Point at which to evaluate the density.</param>
    /// <returns>Density at <paramref name="x"/>; 0 where the distribution has no support.</returns>
    double Pdf(double x);

    /// <summary>Cumulative distribution function at <paramref name="x"/>.</summary>
    /// <param name="x">Point at which to evaluate the CDF.</param>
    /// <returns>P(X ≤ <paramref name="x"/>), in the range [0, 1].</returns>
    double Cdf(double x);
}
