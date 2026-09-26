namespace OpdSimulator.Core.Distributions;

/// <summary>Normal (Gaussian) sampler backed by MathNet.Numerics.</summary>
public sealed class NormalSampler : IDistributionSampler
{
    private readonly MathNet.Numerics.Distributions.Normal _normal;

    /// <summary>
    /// Creates a normal sampler.
    /// </summary>
    /// <param name="random">Source of uniform deviates, adapted for MathNet.</param>
    /// <param name="mean">μ, the centre of the distribution.</param>
    /// <param name="stdDev">σ, must be strictly positive.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="stdDev"/> is not positive.</exception>
    public NormalSampler(IRandomSource random, double mean, double stdDev)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (stdDev <= 0)
            throw new ArgumentOutOfRangeException(nameof(stdDev), stdDev, "StdDev must be positive.");

        _normal = new MathNet.Numerics.Distributions.Normal(mean, stdDev, new MathNetRandomAdapter(random));
        Mean = mean;
        Variance = stdDev * stdDev;
    }

    /// <inheritdoc />
    public double Mean { get; }

    /// <summary>Variance of a normal distribution, σ².</summary>
    public double Variance { get; }

    /// <summary>Draws one normal variate.</summary>
    public double NextSample() => _normal.Sample();

    /// <summary>Normal pdf, (1/σ√(2π))e^(−(x−μ)²/2σ²).</summary>
    public double Pdf(double x) => _normal.Density(x);

    /// <summary>Normal cdf, Φ((x−μ)/σ).</summary>
    public double Cdf(double x) => _normal.CumulativeDistribution(x);
}
