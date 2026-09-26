namespace OpdSimulator.Core.Distributions;

/// <summary>Gamma sampler backed by MathNet.Numerics, specified with shape and scale.</summary>
/// <remarks>
/// <para>
/// The public contract is scale-based, as most texts present it: with shape k and scale θ,
/// <see cref="Mean"/> = k·θ and <see cref="Variance"/> = k·θ². Exponential is the special
/// case k = 1, which is a useful sanity check on the parameterisation.
/// </para>
/// <para>
/// MathNet's <c>Gamma(alpha, beta)</c> is <em>rate</em>-parameterised instead: its mean is
/// α/β and its variance α/β², so passing θ straight through would silently produce
/// k/θ and k/θ². This class therefore converts with <c>beta = 1/θ</c> at construction.
/// Verified against the assembly: MathNet <c>Gamma(4, 2)</c> reports Mean 2 and Variance 1
/// (the rate form), not 8 and 16.
/// </para>
/// </remarks>
public sealed class GammaSampler : IDistributionSampler
{
    private readonly MathNet.Numerics.Distributions.Gamma _gamma;

    /// <summary>
    /// Creates a gamma sampler from shape and scale.
    /// </summary>
    /// <param name="random">Source of uniform deviates, adapted for MathNet.</param>
    /// <param name="shape">k, must be strictly positive.</param>
    /// <param name="scale">θ, must be strictly positive.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="shape"/> or <paramref name="scale"/> is not positive.
    /// </exception>
    public GammaSampler(IRandomSource random, double shape, double scale)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (shape <= 0)
            throw new ArgumentOutOfRangeException(nameof(shape), shape, "Shape must be positive.");
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be positive.");

        // beta = 1/θ converts the scale parameterisation to MathNet's rate form.
        _gamma = new MathNet.Numerics.Distributions.Gamma(shape, 1.0 / scale, new MathNetRandomAdapter(random));
        Mean = shape * scale;
        Variance = shape * scale * scale;
    }

    /// <inheritdoc />
    public double Mean { get; }

    /// <summary>Variance of a gamma distribution, k·θ².</summary>
    public double Variance { get; }

    /// <summary>Draws one gamma variate.</summary>
    public double NextSample() => _gamma.Sample();

    /// <summary>Gamma pdf.</summary>
    public double Pdf(double x) => _gamma.Density(x);

    /// <summary>Gamma cdf.</summary>
    public double Cdf(double x) => _gamma.CumulativeDistribution(x);
}
