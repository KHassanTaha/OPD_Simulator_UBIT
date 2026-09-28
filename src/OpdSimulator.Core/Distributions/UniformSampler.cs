namespace OpdSimulator.Core.Distributions;

/// <summary>Continuous uniform sampler backed by MathNet.Numerics.</summary>
public sealed class UniformSampler : IDistributionSampler
{
    private readonly MathNet.Numerics.Distributions.ContinuousUniform _uniform;

    /// <summary>
    /// Creates a continuous uniform sampler over [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    /// <param name="random">Source of uniform deviates, adapted for MathNet.</param>
    /// <param name="min">Lower bound; must be strictly below <paramref name="max"/>.</param>
    /// <param name="max">Upper bound.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="min"/> is not below <paramref name="max"/>.</exception>
    public UniformSampler(IRandomSource random, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (min >= max)
            throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be strictly less than Max.");

        _uniform = new MathNet.Numerics.Distributions.ContinuousUniform(min, max, new MathNetRandomAdapter(random));
        Min = min;
        Max = max;
    }

    /// <summary>Lower bound of the support.</summary>
    public double Min { get; }

    /// <summary>Upper bound of the support.</summary>
    public double Max { get; }

    /// <summary>Midpoint of the support, (Min + Max) / 2.</summary>
    public double Mean => (Min + Max) / 2.0;

    /// <summary>Variance of a continuous uniform, (Max − Min)² / 12.</summary>
    public double Variance => (Max - Min) * (Max - Min) / 12.0;

    /// <summary>Draws one uniform variate from [Min, Max).</summary>
    public double NextSample() => _uniform.Sample();

    /// <summary>Uniform pdf, 1/(Max − Min) inside the support and 0 outside.</summary>
    public double Pdf(double x) => _uniform.Density(x);

    /// <summary>Uniform cdf, (x − Min) / (Max − Min) inside the support.</summary>
    public double Cdf(double x) => _uniform.CumulativeDistribution(x);
}
