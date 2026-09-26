namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Degenerate distribution that returns the mean on every draw.
/// </summary>
/// <remarks>
/// <para>
/// Not a probability density in the continuous sense: the law is a unit mass at
/// <see cref="Mean"/>. <see cref="Pdf"/> therefore returns
/// <see cref="double.PositiveInfinity"/> at the mean (the limit of a narrowing
/// density) and 0 elsewhere, and the variance is 0.
/// </para>
/// <para>
/// <see cref="IRandomSource"/> is accepted for uniformity with the other samplers —
/// the factory builds all six through the same signature — but is deliberately
/// never read, which is what makes draws exactly repeatable.
/// </para>
/// </remarks>
public sealed class DeterministicSampler : IDistributionSampler
{
    /// <summary>
    /// Creates a sampler that always returns <paramref name="mean"/>.
    /// </summary>
    /// <param name="random">Unused; accepted so the factory has one signature.</param>
    /// <param name="mean">The constant value to emit.</param>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="mean"/> is not positive.</exception>
    public DeterministicSampler(IRandomSource random, double mean)
    {
        if (mean <= 0)
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be positive.");

        _ = random ?? throw new ArgumentNullException(nameof(random));
        Mean = mean;
    }

    /// <inheritdoc />
    public double Mean { get; }

    /// <summary>Zero: a constant has no spread.</summary>
    public double Variance => 0.0;

    /// <summary>Always returns <see cref="Mean"/>.</summary>
    public double NextSample() => Mean;

    /// <summary>Unit mass at the mean; see remarks.</summary>
    public double Pdf(double x) => x == Mean ? double.PositiveInfinity : 0.0;

    /// <summary>Step function: 0 below the mean, 1 at and above it.</summary>
    public double Cdf(double x) => x < Mean ? 0.0 : 1.0;
}
