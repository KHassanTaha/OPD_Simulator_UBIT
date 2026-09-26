namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Exponential sampler parameterised by its mean, drawing via the inverse-CDF transform.
/// </summary>
/// <remarks>
/// For U ~ Uniform(0, 1), X = −ln(U) / λ is Exponential(λ), with mean 1/λ. This is the
/// same transform the older <see cref="ExponentialSampler"/> uses, so a caller migrating to
/// this sampler gets an identical sequence for the same seed and rate — which is why the
/// transform is repeated here instead of delegating to MathNet.
/// </remarks>
public sealed class ExponentialDistributionSampler : IDistributionSampler
{
    private readonly IRandomSource _random;
    private readonly double _rate;

    /// <summary>
    /// Creates a sampler with the given mean.
    /// </summary>
    /// <param name="random">Source of uniform deviates.</param>
    /// <param name="mean">Desired mean, in the same time units the caller works in.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="mean"/> is not positive.</exception>
    public ExponentialDistributionSampler(IRandomSource random, double mean)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        if (mean <= 0)
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be positive.");

        _rate = 1.0 / mean;
        Mean = mean;
    }

    /// <inheritdoc />
    public double Mean { get; }

    /// <summary>Variance of an exponential with rate λ, which is 1/λ² — equivalently mean².</summary>
    public double Variance => 1.0 / (_rate * _rate);

    /// <summary>Draws one exponential variate.</summary>
    public double NextSample()
    {
        double u = _random.NextDouble();
        if (u <= 0)
            u = double.Epsilon; // guard against -ln(0) = +infinity

        return -Math.Log(u) / _rate;
    }

    /// <summary>Exponential pdf, λe^(−λx) for x ≥ 0 and 0 otherwise.</summary>
    public double Pdf(double x) => x < 0 ? 0.0 : _rate * Math.Exp(-_rate * x);

    /// <summary>Exponential cdf, 1 − e^(−λx) for x ≥ 0 and 0 otherwise.</summary>
    public double Cdf(double x) => x < 0 ? 0.0 : 1.0 - Math.Exp(-_rate * x);
}
