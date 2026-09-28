namespace OpdSimulator.Core.Distributions;

/// <summary>Builds an <see cref="IDistributionSampler"/> from a declarative <see cref="DistributionSpec"/>.
/// <remarks>
/// Validation lives here rather than in each sampler so that a spec is rejected before any
/// sampler is constructed, and so every failure names the field the caller actually set in
/// the spec. Each error message mentions the offending field, for example
/// <c>"Normal distribution requires StdDev &gt; 0."</c>.
/// </remarks>
public static class DistributionSamplerFactory
{
    /// <summary>
    /// Creates the sampler described by <paramref name="spec"/>.
    /// </summary>
    /// <param name="spec">The family and its parameters.</param>
    /// <param name="rng">Source of uniform deviates, shared by MathNet-backed samplers.</param>
    /// <returns>A sampler ready to draw from.</returns>
    /// <exception cref="ArgumentNullException">
    /// If <paramref name="spec"/> or <paramref name="rng"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// If a parameter the family requires is missing or out of range. The message names the field.
    /// </exception>
    public static IDistributionSampler Create(DistributionSpec spec, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(rng);

        switch (spec.Family)
        {
            case DistributionFamily.Exponential:
                return new ExponentialDistributionSampler(rng, RequirePositiveMean(spec, "Exponential"));

            case DistributionFamily.Deterministic:
                return new DeterministicSampler(rng, RequirePositiveMean(spec, "Deterministic"));

            case DistributionFamily.Normal:
                return new NormalSampler(rng, spec.Mean, RequirePositiveStdDev(spec, "Normal"));

            // Lognormal is specified by the mean and sd of X, but the constructor needs ln(Mean),
            // so Mean must be positive too — checked here rather than deeper down.
            case DistributionFamily.Lognormal:
                return new LognormalSampler(
                    rng,
                    RequirePositiveMean(spec, "Lognormal"),
                    RequirePositiveStdDev(spec, "Lognormal"));

            case DistributionFamily.Gamma:
                return new GammaSampler(
                    rng,
                    RequirePositiveShape(spec, "Gamma"),
                    RequirePositiveScale(spec, "Gamma"));

            case DistributionFamily.Uniform:
                return new UniformSampler(rng, RequireMin(spec), RequireMax(spec));

            default:
                throw new ArgumentException($"Unsupported distribution family '{spec.Family}'.", nameof(spec));
        }
    }

    private static double RequirePositiveMean(DistributionSpec spec, string family)
    {
        if (spec.Mean <= 0)
            throw new ArgumentException($"{family} distribution requires Mean > 0.", "spec.Mean");

        return spec.Mean;
    }

    private static double RequirePositiveStdDev(DistributionSpec spec, string family)
    {
        if (spec.StdDev is null || spec.StdDev <= 0)
            throw new ArgumentException($"{family} distribution requires StdDev > 0.", "spec.StdDev");

        return spec.StdDev.Value;
    }

    private static double RequirePositiveShape(DistributionSpec spec, string family)
    {
        if (spec.Shape is null || spec.Shape <= 0)
            throw new ArgumentException($"{family} distribution requires Shape > 0.", "spec.Shape");

        return spec.Shape.Value;
    }

    private static double RequirePositiveScale(DistributionSpec spec, string family)
    {
        if (spec.Scale is null || spec.Scale <= 0)
            throw new ArgumentException($"{family} distribution requires Scale > 0.", "spec.Scale");

        return spec.Scale.Value;
    }

    private static double RequireMin(DistributionSpec spec)
    {
        if (spec.Min is null)
            throw new ArgumentException("Uniform distribution requires Min < Max.", "spec.Min");

        return spec.Min.Value;
    }

    private static double RequireMax(DistributionSpec spec)
    {
        if (spec.Max is null)
            throw new ArgumentException("Uniform distribution requires Max > Min.", "spec.Max");

        if (spec.Min >= spec.Max)
        {
            throw new ArgumentException(
                $"Uniform distribution requires Min < Max (got Min={spec.Min}, Max={spec.Max}).", "spec.Min");
        }

        return spec.Max.Value;
    }
}
