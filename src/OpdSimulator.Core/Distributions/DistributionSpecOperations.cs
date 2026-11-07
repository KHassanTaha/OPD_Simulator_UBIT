namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Operations on <see cref="DistributionSpec"/> that need more than a record
/// copy: rescaling the location without changing the shape, and deriving the
/// squared coefficient of variation the analytical models consume.
/// </summary>
/// <remarks>
/// <para>
/// The simulator resolves a rate (λ or μ) and a distribution family separately,
/// because the two are entered in different places and may come from different
/// sources. <see cref="WithMean"/> is the single place that reconciles them: it
/// replaces the mean while preserving the distribution's <b>shape</b>, so a
/// fitted Gamma stays the same Gamma at the rate the run actually uses. Doing
/// this in the view models instead would give each caller its own chance to
/// disagree, which is the class of bug D-147 and D-157 exist to prevent.
/// </para>
/// <para>
/// Shape is preserved in the only sense that matters to a queue model: the
/// squared coefficient of variation, <c>Ca² = Var(X)/E[X]²</c>. A Normal or
/// Lognormal keeps its CV, not its absolute standard deviation; a Uniform keeps
/// its CV, not its absolute half-width; a Gamma keeps its shape <c>k</c>.
/// </para>
/// </remarks>
public static class DistributionSpecOperations
{
    /// <summary>
    /// Computes <c>Var(X) / E[X]²</c> for a configured distribution.
    /// </summary>
    /// <param name="spec">The distribution to describe.</param>
    /// <returns>
    /// The squared coefficient of variation: 1 for Exponential, 0 for
    /// Deterministic, <c>1/k</c> for Gamma, and <c>(σ/μ)²</c> for Normal and
    /// Lognormal.
    /// </returns>
    /// <exception cref="ArgumentNullException">If <paramref name="spec"/> is null.</exception>
    /// <exception cref="ArgumentException">If the spec's mean is not positive.</exception>
    public static double SquaredCoefficientOfVariation(DistributionSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!(spec.Mean > 0))
        {
            throw new ArgumentException(
                $"Distribution mean must be positive (was {spec.Mean:0.####}).",
                nameof(spec));
        }

        double variance = spec.Family switch
        {
            DistributionFamily.Exponential => spec.Mean * spec.Mean,
            DistributionFamily.Deterministic => 0.0,
            DistributionFamily.Normal => Square(spec.StdDev ?? 0.0),
            DistributionFamily.Lognormal => Square(spec.StdDev ?? 0.0),
            DistributionFamily.Gamma => spec.Shape is > 0
                ? spec.Mean * spec.Mean / spec.Shape.Value
                : spec.Mean * spec.Mean,
            DistributionFamily.Uniform => Square((spec.Max ?? spec.Mean) - (spec.Min ?? spec.Mean)) / 12.0,
            _ => spec.Mean * spec.Mean
        };

        return variance / (spec.Mean * spec.Mean);
    }

    /// <summary>
    /// Returns the same distribution relocated to a new mean, preserving its
    /// squared coefficient of variation.
    /// </summary>
    /// <param name="spec">The configured distribution; its mean must be positive.</param>
    /// <param name="mean">The new mean (for an arrival process, <c>1/λ</c>).</param>
    /// <returns>A spec of the same family and shape at <paramref name="mean"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="spec"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="mean"/> is not positive.</exception>
    /// <exception cref="ArgumentException">If <paramref name="spec"/>'s own mean is not positive.</exception>
    public static DistributionSpec WithMean(DistributionSpec spec, double mean)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!(mean > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be strictly positive.");
        }

        if (!(spec.Mean > 0))
        {
            throw new ArgumentException(
                $"The spec's own mean must be positive to rescale it (was {spec.Mean:0.####}).",
                nameof(spec));
        }

        // The factor that keeps the spread proportional to the location. For
        // Exponential and Deterministic the factor is irrelevant: neither has a
        // free spread parameter.
        double scale = mean / spec.Mean;

        return spec.Family switch
        {
            DistributionFamily.Exponential or DistributionFamily.Deterministic
                => spec with { Mean = mean },

            // StdDev is absolute in the spec, so a relocated Normal/Lognormal must
            // scale it too; copying it unchanged would silently change the CV, and
            // the analytical model would then describe a different distribution
            // from the one the engine samples.
            DistributionFamily.Normal or DistributionFamily.Lognormal
                => spec with { Mean = mean, StdDev = (spec.StdDev ?? 0.0) * scale },

            // Gamma's shape is dimensionless; its scale carries the location.
            DistributionFamily.Gamma
                => spec with { Mean = mean, Scale = mean / RequireShape(spec) },

            // Uniform bounds are symmetric about the mean, so moving the mean and
            // scaling the half-width keeps the CV and keeps Min < Max.
            DistributionFamily.Uniform
                => RescaleUniform(spec, mean, scale),

            _ => throw new ArgumentOutOfRangeException(
                nameof(spec), spec.Family, "Unsupported distribution family.")
        };
    }

    /// <summary>
    /// Builds a distribution spec from a family, a mean, and that family's spread
    /// parameter.
    /// </summary>
    /// <param name="family">The distribution family.</param>
    /// <param name="mean">The distribution mean in minutes; must be positive.</param>
    /// <param name="spread">
    /// The family's spread parameter, in the same units the family uses elsewhere
    /// in the app: shape <c>k</c> for Gamma, standard deviation in minutes for
    /// Normal and Lognormal, and half-width in minutes for Uniform. Ignored for
    /// Exponential and Deterministic, which have no free spread parameter.
    /// </param>
    /// <returns>The distribution spec.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If <paramref name="mean"/> is not positive, or a family that needs a spread
    /// parameter was given a missing or non-positive one.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// If a Uniform half-width is at least the mean, which would put the lower
    /// bound at or below zero — a negative duration is not a duration.
    /// </exception>
    public static DistributionSpec Create(
        DistributionFamily family,
        double mean,
        double? spread)
    {
        if (!(mean > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be strictly positive.");
        }

        switch (family)
        {
            case DistributionFamily.Exponential:
            case DistributionFamily.Deterministic:
                return new DistributionSpec(family, mean);

            case DistributionFamily.Gamma:
            {
                double shape = RequireSpread(spread, family, "shape k");
                return new DistributionSpec(family, mean, Shape: shape, Scale: mean / shape);
            }

            case DistributionFamily.Normal:
            case DistributionFamily.Lognormal:
            {
                double stdDev = RequireSpread(spread, family, "standard deviation (minutes)");
                return new DistributionSpec(family, mean, StdDev: stdDev);
            }

            case DistributionFamily.Uniform:
            {
                double halfWidth = RequireSpread(spread, family, "half-width (minutes)");
                if (halfWidth >= mean)
                {
                    throw new ArgumentException(
                        $"Uniform half-width ({halfWidth:0.####}) must be less than the mean " +
                        $"({mean:0.####}), or the lower bound would be zero or negative.",
                        nameof(spread));
                }

                return new DistributionSpec(family, mean, Min: mean - halfWidth, Max: mean + halfWidth);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported distribution family.");
        }
    }

    private static DistributionSpec RescaleUniform(DistributionSpec spec, double mean, double scale)
    {
        if (spec.Min is not { } min || spec.Max is not { } max || !(max > min))
        {
            throw new ArgumentException("Uniform rescaling needs Min < Max on the spec.", nameof(spec));
        }

        double halfWidth = (max - min) / 2.0 * scale;
        if (halfWidth >= mean)
        {
            throw new ArgumentException(
                $"Uniform half-width ({halfWidth:0.####}) must be less than the mean ({mean:0.####}).",
                nameof(spec));
        }

        return spec with { Mean = mean, Min = mean - halfWidth, Max = mean + halfWidth };
    }

    private static double RequireShape(DistributionSpec spec)
        => spec.Shape is > 0
            ? spec.Shape.Value
            : throw new ArgumentException("Gamma requires Shape > 0 on the spec.", nameof(spec));

    private static double RequireSpread(double? spread, DistributionFamily family, string description)
    {
        if (spread is not > 0 || !double.IsFinite(spread.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(spread),
                spread,
                $"{family} requires a positive {description}.");
        }

        return spread.Value;
    }

    private static double Square(double value) => value * value;
}
