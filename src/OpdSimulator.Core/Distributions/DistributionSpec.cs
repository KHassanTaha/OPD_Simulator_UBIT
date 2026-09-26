namespace OpdSimulator.Core.Distributions;

/// <summary>Declarative specification of a distribution family and its parameters.
/// <para>Not every family uses every field; the factory reads only the fields its
/// family requires and validates that they are present and in range.</para></summary>
/// <param name="Family">Which distribution family to sample.</param>
/// <param name="Mean">The mean of the distribution, in time units for the queue model.</param>
/// <param name="StdDev">Standard deviation. Used by Normal and (via the log-mean) Lognormal.</param>
/// <param name="Shape">Gamma shape parameter (k, must be > 0).</param>
/// <param name="Scale">Gamma scale parameter (θ, must be > 0).</param>
/// <param name="Min">Lower bound for the Uniform family.</param>
/// <param name="Max">Upper bound for the Uniform family.</param>
public sealed record DistributionSpec(
    DistributionFamily Family,
    double Mean,
    double? StdDev = null,
    double? Shape = null,
    double? Scale = null,
    double? Min = null,
    double? Max = null);
