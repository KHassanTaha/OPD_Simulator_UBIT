namespace OpdSimulator.Data.Fitting;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Fits each supported continuous family to a sample set and
/// selects the best by AIC, with chi-square as a sanity check.
/// </summary>
/// <remarks>
/// <para>
/// This is the auto-fit path (G/G/1): given observations, it decides which family
/// describes them instead of trusting a user-picked family. It orchestrates the
/// existing per-family fitters — it never re-derives an MLE — and adds the two
/// things the individual fitters do not provide: a cross-family ranking (AIC/BIC)
/// and a chi-square sanity check.
/// </para>
/// <para>
/// <b>Orchestration lives here, not in the Engine.</b> Core does not reference Data,
/// so <c>OpdSimulator.Core</c> cannot call this class. App and Cli reference both
/// projects and are the layer that turns a fit into engine configuration.
/// </para>
/// <para>
/// <b>Deterministic is not a candidate.</b> A degenerate distribution has no
/// dispersion to discover: a constant series "fits" Deterministic perfectly, so
/// offering it would make the best-fitting family a statement about the user's
/// choice rather than about the data. It stays a user choice.
/// </para>
/// <para>
/// <b>Nothing here throws for bad data.</b> The individual fitters and
/// <see cref="ChiSquareTest"/> both throw on unusable input; every such throw is
/// caught and reported as a rejected candidate with the original message, so a
/// hopeless sample set yields an all-rejected result rather than an exception.
/// </para>
/// </remarks>
public static class GeneralDistributionFitter
{
    /// <summary>
    /// Below this many samples no family is fitted. Chi-square needs a usable number
    /// of bins and the parameters are not meaningfully estimated from a handful of
    /// points, so a short sample is reported as insufficient rather than mis-fitted.
    /// <para>
    /// <c>internal</c> rather than <c>private</c> so <see cref="GammaFitter"/> can
    /// re-test the same threshold after it excludes non-positive samples, instead of
    /// hard-coding a second copy of 20 that could drift away from this one.
    /// </para>
    /// </summary>
    internal const int MinimumSampleCount = 20;

    /// <summary>Classical lower bound on an expected bin frequency for a trustworthy χ².</summary>
    private const double MinimumExpectedBinCount = 5.0;

    private static readonly DistributionFamily[] CandidateArray =
    {
        DistributionFamily.Exponential,
        DistributionFamily.Normal,
        DistributionFamily.Lognormal,
        DistributionFamily.Gamma,
        DistributionFamily.Uniform,
    };

    /// <summary>
    /// Candidates considered by <see cref="FitBest"/>. Deterministic
    /// is NOT a fit candidate — it is a user choice, not a
    /// data-driven outcome.
    /// </summary>
    public static IReadOnlyList<DistributionFamily> Candidates { get; } =
        Array.AsReadOnly(CandidateArray);

    /// <summary>
    /// The existing fitters, keyed by the family each one fits.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="DistributionFitterFactory.SupportedNames"/> rather than a
    /// second hand-written list, so a family added there is picked up here. The
    /// string-to-enum bridge (<see cref="ToFamily"/>) is the single place the two
    /// vocabularies meet: Data identifies a family by its CLI/UI name, Core by
    /// <see cref="DistributionFamily"/>.
    /// </remarks>
    private static readonly IReadOnlyDictionary<DistributionFamily, IDistributionFitter> FittersByFamily =
        DistributionFitterFactory.SupportedNames.ToDictionary(
            name => ToFamily(name),
            name => ResolveFitter(name));

    /// <summary>
    /// Fit every candidate family to the samples, reject the ones
    /// that break their assumptions, and return the winner with
    /// the lowest AIC.
    /// </summary>
    /// <param name="samples">Observed samples, all &gt; 0.</param>
    /// <param name="significanceLevel">Alpha for the chi-square
    /// sanity check (default 0.05).</param>
    /// <returns>The winner (or null) plus every candidate tried.</returns>
    public static GeneralFitResult FitBest(
        IReadOnlyList<double> samples,
        double significanceLevel = 0.05)
    {
        // A null or empty series is data the caller got wrong, not a bug in this
        // method, so it flows through the same "not enough evidence" path as a short
        // sample rather than throwing (see remarks).
        var observed = samples ?? Array.Empty<double>();

        if (observed.Count < MinimumSampleCount)
        {
            var tooFew = CandidateArray
                .Select(family => RejectedWithoutFit(family, $"insufficient samples (< {MinimumSampleCount})"))
                .ToList();

            return new GeneralFitResult(null, tooFew);
        }

        var candidates = CandidateArray
            .Select(family => FitCandidate(family, observed, significanceLevel))
            .ToList();

        var best = ChooseBest(candidates);
        return new GeneralFitResult(best, candidates);
    }

    /// <summary>Free parameter count k, the AIC/BIC penalty term.</summary>
    private static int ParameterCount(DistributionFamily family) => family switch
    {
        // Exponential is the only one-parameter family: λ is fully determined by x̄.
        DistributionFamily.Exponential => 1,
        DistributionFamily.Normal => 2,
        DistributionFamily.Lognormal => 2,
        DistributionFamily.Gamma => 2,
        DistributionFamily.Uniform => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Not a fit candidate."),
    };

    private static DistributionFitResult FitCandidate(
        DistributionFamily family,
        IReadOnlyList<double> samples,
        double alpha)
    {
        var fitter = FittersByFamily[family];

        FittedDistribution fit;
        try
        {
            fit = fitter.Fit(samples);
        }
        catch (Exception ex)
        {
            // The per-family fitters guard their own assumptions by throwing (a
            // non-positive sample for Lognormal, zero variance for Normal/Gamma, a
            // single distinct value for Uniform, ...). That is a rejection, not a
            // failure of this method.
            return RejectedWithoutFit(family, ex.Message);
        }

        DistributionSpec spec;
        try
        {
            spec = ToSpec(family, fit);
        }
        catch (Exception ex)
        {
            return RejectedWithoutFit(family, $"could not translate fitted parameters: {ex.Message}");
        }

        if (!IsUsable(spec))
            return RejectedWithoutFit(family, "fitted parameters are not finite");

        int k = ParameterCount(family);
        double logLikelihood = fit.LogLikelihood;
        double aic = (2 * k) - (2 * logLikelihood);
        double bic = (k * Math.Log(samples.Count)) - (2 * logLikelihood);

        ChiSquareResult chiSquare;
        try
        {
            chiSquare = ChiSquareTest.Run(samples, fit, alpha);
        }
        catch (Exception ex)
        {
            // ChiSquareTest throws on an unusable alpha, too few expected counts, or
            // degenerate degrees of freedom. The fit itself is still valid, so it is kept
            // and only the candidate is disqualified.
            return RejectedAfterFit(family, $"chi-square test unavailable: {ex.Message}", spec, logLikelihood, aic, bic, null);
        }

        if (chiSquare.Expected.Min() < MinimumExpectedBinCount)
            return RejectedAfterFit(family, "expected bin count below 5", spec, logLikelihood, aic, bic, chiSquare);

        return new DistributionFitResult(
            Family: family,
            Spec: spec,
            LogLikelihood: logLikelihood,
            Aic: aic,
            Bic: bic,
            ChiSquare: chiSquare,
            Rejected: false,
            RejectionReason: null);
    }

    /// <summary>
    /// Lowest AIC wins; ties fall to BIC, then to the better chi-square p-value.
    /// </summary>
    private static DistributionFitResult? ChooseBest(List<DistributionFitResult> candidates)
    {
        var ranked = candidates
            .Where(c => !c.Rejected)
            .OrderBy(c => c.Aic)
            .ThenBy(c => c.Bic)
            .ThenByDescending(c => c.ChiSquare?.PValue ?? double.NegativeInfinity)
            .ToList();

        return ranked.Count == 0 ? null : ranked[0];
    }

    /// <summary>
    /// A candidate that never produced a fit, so there are no diagnostics to report.
    /// </summary>
    private static DistributionFitResult RejectedWithoutFit(DistributionFamily family, string reason)
    {
        return new DistributionFitResult(
            Family: family,
            // The record's Spec is non-nullable, but a candidate that never fitted has no
            // parameters. Mean is NaN rather than 0 so the placeholder can never be
            // mistaken for an estimate; callers must check Rejected before using the spec.
            Spec: new DistributionSpec(family, double.NaN),
            LogLikelihood: double.NaN,
            Aic: double.NaN,
            Bic: double.NaN,
            ChiSquare: null,
            Rejected: true,
            RejectionReason: reason);
    }

    /// <summary>
    /// A candidate that fitted successfully but was disqualified afterwards — its
    /// parameters and likelihood are real, so they are kept.
    /// </summary>
    /// <remarks>
    /// Discarding these would hide exactly the comparison the caller needs: knowing that
    /// Uniform out-scored Normal on likelihood but had to be rejected for a 4-per-bin
    /// chi-square is the useful fact, and NaN-ing it away would hide it.
    /// </remarks>
    private static DistributionFitResult RejectedAfterFit(
        DistributionFamily family,
        string reason,
        DistributionSpec spec,
        double logLikelihood,
        double aic,
        double bic,
        ChiSquareResult? chiSquare)
    {
        return new DistributionFitResult(
            Family: family,
            Spec: spec,
            LogLikelihood: logLikelihood,
            Aic: aic,
            Bic: bic,
            ChiSquare: chiSquare,
            Rejected: true,
            RejectionReason: reason);
    }

    /// <summary>
    /// Translates a Data fitter's MathNet-flavoured parameters into the
    /// <see cref="DistributionSpec"/> that Core's sampler factory expects.
    /// </summary>
    /// <remarks>
    /// The two layers do not share a parameterisation, and the differences are exactly
    /// the kind that silently produce a wrong distribution (D-144):
    /// <list type="bullet">
    /// <item><b>Gamma</b> — the fitter reports MathNet's shape–<b>rate</b>; Core's
    /// <c>GammaSampler</c> takes shape–<b>scale</b>. The spec therefore carries
    /// <c>scale = 1/rate</c>.</item>
    /// <item><b>Lognormal</b> — the fitter reports MathNet's (μ, σ), which are the mean
    /// and sd of <c>ln X</c>. Core's <c>LognormalSampler</c> takes the mean and sd
    /// <b>of X</b>, so they are converted back: E[X] = exp(μ + σ²/2) and
    /// Var[X] = (e^σ² − 1)·e^(2μ+σ²).</item>
    /// </list>
    /// </remarks>
    private static DistributionSpec ToSpec(DistributionFamily family, FittedDistribution fit)
    {
        var p = fit.Parameters;

        return family switch
        {
            // λ is the rate, so the mean of the distribution is its reciprocal.
            DistributionFamily.Exponential =>
                new DistributionSpec(family, 1.0 / p["rate"]),

            DistributionFamily.Normal =>
                new DistributionSpec(family, p["mean"], StdDev: p["stddev"]),

            DistributionFamily.Lognormal => ToLognormalSpec(p["mu"], p["sigma"]),

            // shape stays; rate becomes scale. Mean is shape·scale = shape/rate.
            DistributionFamily.Gamma =>
                new DistributionSpec(
                    family,
                    p["shape"] / p["rate"],
                    Shape: p["shape"],
                    Scale: 1.0 / p["rate"]),

            DistributionFamily.Uniform =>
                new DistributionSpec(family, (p["min"] + p["max"]) / 2.0, Min: p["min"], Max: p["max"]),

            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Not a fit candidate."),
        };
    }

    private static DistributionSpec ToLognormalSpec(double mu, double sigma)
    {
        double variance = (Math.Exp(sigma * sigma) - 1.0) * Math.Exp((2 * mu) + (sigma * sigma));
        return new DistributionSpec(
            DistributionFamily.Lognormal,
            Math.Exp(mu + (sigma * sigma / 2.0)),
            StdDev: Math.Sqrt(variance));
    }

    /// <summary>
    /// Rejects a spec whose numbers went non-finite, which would otherwise propagate
    /// silently into a sampler built from it.
    /// </summary>
    private static bool IsUsable(DistributionSpec spec)
    {
        var values = new List<double?> { spec.Mean, spec.StdDev, spec.Shape, spec.Scale, spec.Min, spec.Max };
        return values.Where(v => v.HasValue).All(v => double.IsFinite(v!.Value));
    }

    /// <summary>
    /// The single bridge between Data's family <em>names</em> and Core's family <em>enum</em>.
    /// </summary>
    /// <remarks>
    /// Data predates <see cref="DistributionFamily"/> and identifies a family by its
    /// CLI/UI name string. <see cref="IDistributionFitter.Name"/> is public and consumed
    /// by App and Cli, so it is left alone; the two vocabularies meet here instead.
    /// </remarks>
    private static DistributionFamily ToFamily(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "exponential" => DistributionFamily.Exponential,
            "normal" => DistributionFamily.Normal,
            "lognormal" => DistributionFamily.Lognormal,
            "gamma" => DistributionFamily.Gamma,
            "uniform" => DistributionFamily.Uniform,
            _ => throw new ArgumentException(
                $"Unknown distribution family name '{name}'. Expected one of: " +
                $"{DistributionFitterFactory.SupportedNamesText()}.",
                nameof(name)),
        };
    }

    private static IDistributionFitter ResolveFitter(string name)
    {
        if (!DistributionFitterFactory.TryCreate(name, out var fitter) || fitter is null)
        {
            throw new InvalidOperationException(
                $"DistributionFitterFactory lists '{name}' as supported but could not build it.");
        }

        return fitter;
    }
}
