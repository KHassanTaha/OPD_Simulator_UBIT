namespace OpdSimulator.Core.Distributions;

/// <summary>Lognormal sampler backed by MathNet.Numerics, specified in the units of X.</summary>
/// <remarks>
/// <para>
/// Callers describe the service/arrival times they observed, so this sampler takes the
/// arithmetic mean and standard deviation <em>of X</em>, not of ln X. MathNet's
/// <c>LogNormal(mu, sigma)</c> is parameterised the other way round: <c>mu</c> and
/// <c>sigma</c> are the mean and standard deviation of <c>ln X</c>. The conversion used
/// here is the standard one-way mapping:
/// </para>
/// <list type="bullet">
///   <item><description>σ² = ln(1 + (StdDev / Mean)²)</description></item>
///   <item><description>μ = ln(Mean) − σ² / 2</description></item>
/// </list>
/// <para>
/// The subtraction of σ²/2 is the part that is easy to omit: without it the distribution
/// is shifted and the sample mean no longer matches <see cref="Mean"/>.
/// </para>
/// </remarks>
public sealed class LognormalSampler : IDistributionSampler
{
    private readonly MathNet.Numerics.Distributions.LogNormal _lognormal;

    /// <summary>
    /// Creates a lognormal sampler from the mean and standard deviation of X.
    /// </summary>
    /// <param name="random">Source of uniform deviates, adapted for MathNet.</param>
    /// <param name="mean">E[X]; must be strictly positive because ln(Mean) is required.</param>
    /// <param name="stdDev">SD(X); must be strictly positive.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="mean"/> or <paramref name="stdDev"/> is not positive.
    /// </exception>
    public LognormalSampler(IRandomSource random, double mean, double stdDev)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (mean <= 0)
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be positive.");
        if (stdDev <= 0)
            throw new ArgumentOutOfRangeException(nameof(stdDev), stdDev, "StdDev must be positive.");

        double sigmaSquared = Math.Log(1.0 + (stdDev / mean) * (stdDev / mean));
        double mu = Math.Log(mean) - (sigmaSquared / 2.0);

        _lognormal = new MathNet.Numerics.Distributions.LogNormal(mu, Math.Sqrt(sigmaSquared), new MathNetRandomAdapter(random));
        Mean = mean;
        Variance = stdDev * stdDev;
    }

    /// <inheritdoc />
    public double Mean { get; }

    /// <summary>Variance of X, which is StdDev².</summary>
    public double Variance { get; }

    /// <summary>Draws one lognormal variate.</summary>
    public double NextSample() => _lognormal.Sample();

    /// <summary>Lognormal pdf of X.</summary>
    public double Pdf(double x) => _lognormal.Density(x);

    /// <summary>Lognormal cdf of X.</summary>
    public double Cdf(double x) => _lognormal.CumulativeDistribution(x);
}
