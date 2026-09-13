namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Generates exponential random variates via the inverse-CDF transform.
/// </summary>
/// <remarks>
/// For U ~ Uniform(0,1), X = −ln(U)/rate follows Exponential(rate) (CONTEXT §4.4).
/// This is the workhorse for inter-arrival and service times in the M/M/c model.
/// For U ≈ 0, ln(U) → −∞, so U is clamped to <see cref="double.Epsilon"/>.
/// </remarks>
public sealed class ExponentialSampler
{
    private readonly IRandomSource _random;

    /// <summary>
    /// Creates the sampler bound to a random source.
    /// </summary>
    /// <param name="random">The source of uniform deviates.</param>
    public ExponentialSampler(IRandomSource random)
    {
        _random = random;
    }

    /// <summary>
    /// Draws one exponential variate with the given rate.
    /// </summary>
    /// <param name="rate">Rate λ (events per minute). Must be strictly positive.</param>
    /// <returns>A non-negative exponential sample with mean 1/rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="rate"/> is not strictly positive.</exception>
    public double Sample(double rate)
    {
        if (rate <= 0)
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "Rate must be strictly positive.");

        double u = _random.NextDouble();
        if (u <= 0)
            u = double.Epsilon; // guard against -ln(0) = +infinity

        return -Math.Log(u) / rate;
    }
}