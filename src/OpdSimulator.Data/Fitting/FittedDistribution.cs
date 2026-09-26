namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;
using OpdSimulator.Core.Distributions;

/// <summary>
/// A fitted continuous distribution and the diagnostics that describe how well it fits.
/// </summary>
/// <remarks>
/// MathNet's <see cref="IContinuousDistribution"/> interface exposes densities and
/// sampling but <em>not</em> the CDF (that lives on an internal base class). We
/// therefore capture the CDF and its inverse as delegates at construction time,
/// where the concrete distribution type is known — the equality-probability
/// chi-square binning needs exactly those two functions (FR-STAT-3).
/// </remarks>
public sealed class FittedDistribution
{
    /// <summary>
    /// Creates a fitted distribution.
    /// </summary>
    /// <param name="name">Family name (also the CLI value).</param>
    /// <param name="parameters">Estimated parameters, name → value.</param>
    /// <param name="distribution">The MathNet instance (used for densities/sampling).</param>
    /// <param name="cdf">The distribution's cumulative density function P(X ≤ x).</param>
    /// <param name="inverseCdf">The quantile function, inverse of <paramref name="cdf"/>.</param>
    /// <param name="sampleSize">Number of samples the fit used.</param>
    /// <param name="logLikelihood">In-sample log-likelihood.</param>
    public FittedDistribution(
        string name,
        IReadOnlyDictionary<string, double> parameters,
        IContinuousDistribution distribution,
        Func<double, double> cdf,
        Func<double, double> inverseCdf,
        int sampleSize,
        double logLikelihood)
    {
        Name = name;
        Parameters = parameters;
        Distribution = distribution;
        Cdf = cdf;
        InverseCdf = inverseCdf;
        SampleSize = sampleSize;
        LogLikelihood = logLikelihood;
        AIC = 2 * parameters.Count - 2 * logLikelihood; // AIC = 2k − 2·LL
    }

    /// <summary>Distribution name as shown in the UI and export (e.g. "Exponential").</summary>
    public string Name { get; }

    /// <summary>Estimated parameters in stable order, name → value (e.g. "rate" → 0.5).</summary>
    public IReadOnlyDictionary<string, double> Parameters { get; }

    /// <summary>The MathNet distribution instance (densities and sampling).</summary>
    public IContinuousDistribution Distribution { get; }

    /// <summary>P(X ≤ x) for the fitted parameters.</summary>
    public Func<double, double> Cdf { get; }

    /// <summary>Quantile function x such that P(X ≤ x) = p.</summary>
    public Func<double, double> InverseCdf { get; }

    /// <summary>Number of samples the fit used.</summary>
    public int SampleSize { get; }

    /// <summary>In-sample log-likelihood (always ≤ 0 for continuous data).</summary>
    public double LogLikelihood { get; }

    /// <summary>Akaike information criterion = 2k − 2·LL; penalises extra parameters.</summary>
    public double AIC { get; }

    /// <summary>
    /// Renders parameters as a compact, human- and export-friendly string.
    /// </summary>
    /// <returns>e.g. <c>rate = 0.5000</c>.</returns>
    public string ParametersText()
        => string.Join(", ", Parameters.Select(kvp => $"{kvp.Key} = {kvp.Value:0.###}"));

    /// <summary>
    /// Wraps an already-configured <see cref="DistributionSpec"/> as a
    /// <see cref="FittedDistribution"/> WITHOUT fitting anything, so the existing
    /// chi-square and histogram pipeline can be pointed at a specification instead of
    /// at a refit (Phase 8K, D-154).
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS EXISTS. Output-side verification asks "did the engine generate the
    /// distribution the user configured?". Refitting the engine's own output and testing
    /// the refit is a different question — it passes whenever the engine produces
    /// <em>some</em> plausible distribution, so a stage configured as Normal but sampled
    /// with the sampler's default would still be reported as a good fit. Passing the
    /// configured spec through the same <see cref="ChiSquareTest"/> makes the verdict
    /// answer the question that was actually asked.
    /// </para>
    /// <para>
    /// WHY <see cref="LogLikelihood"/> IS NaN. A likelihood is the probability of a
    /// sample under a distribution, and this distribution was not estimated from any
    /// sample, so there is no honest value to report. NaN is used because it is already
    /// this codebase's "no value" sentinel for a fit that did not happen — a rejected
    /// candidate in <see cref="GeneralDistributionFitter"/> carries a NaN AIC for the
    /// same reason. Consequently <see cref="AIC"/> is NaN too, and neither must be read
    /// on a spec-derived instance: AIC compares competing <em>fits</em> of the same data
    /// and there is nothing here to compare. The verification path reads only
    /// <see cref="Cdf"/> and <see cref="InverseCdf"/>.
    /// </para>
    /// </remarks>
    /// <param name="spec">The configured specification. Its <c>Mean</c> is the stage's mean service time; the spread field depends on the family.</param>
    /// <param name="sampleSize">Sample count to record, for display only.</param>
    /// <returns>A distribution carrying the spec's parameters, ready for chi-square and histogram use.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="spec"/> is null.</exception>
    /// <exception cref="ArgumentException">When the spec's family has no configured distribution, or its mean or spread is not usable.</exception>
    public static FittedDistribution FromSpec(DistributionSpec spec, int sampleSize)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!(spec.Mean > 0) || double.IsNaN(spec.Mean))
        {
            throw new ArgumentException(
                $"Cannot build a verification distribution for {spec.Family}: mean must be > 0 but was {spec.Mean}.",
                nameof(spec));
        }

        IContinuousDistribution distribution;
        Func<double, double> inverseCdf;
        Dictionary<string, double> parameters;

        switch (spec.Family)
        {
            case DistributionFamily.Exponential:
                // The spec stores a mean; the MathNet constructor takes a rate, so the
                // one inversion lives here and nowhere else.
                var exponential = new Exponential(1.0 / spec.Mean);
                distribution = exponential;
                inverseCdf = exponential.InverseCumulativeDistribution;
                parameters = new Dictionary<string, double> { ["rate"] = 1.0 / spec.Mean };
                break;

            case DistributionFamily.Deterministic:
                throw new ArgumentException(
                    "Deterministic has no distribution to chi-square; the caller skips it.",
                    nameof(spec));

            case DistributionFamily.Normal:
                Require(spec.StdDev, "standard deviation", spec.Family);
                var normal = new Normal(spec.Mean, spec.StdDev!.Value);
                distribution = normal;
                inverseCdf = normal.InverseCumulativeDistribution;
                parameters = new Dictionary<string, double>
                {
                    ["mean"] = spec.Mean,
                    ["stdDev"] = spec.StdDev!.Value,
                };
                break;

            case DistributionFamily.Lognormal:
                // The spec carries μ (the service rate) and σ in ARITHMETIC terms, which
                // is how the user enters and reads them. MathNet's LogNormal is
                // parameterised by the parameters of ln X, so the arithmetic σ has to be
                // converted. This is the same derivation LognormalFitter performs on a
                // sample, repeated here because this path has no sample.
                Require(spec.StdDev, "standard deviation", spec.Family);
                double sigmaOfLog = LogNormalArithmeticStdDevToLogStdDev(spec.Mean, spec.StdDev!.Value);
                var logNormal = new LogNormal(Math.Log(spec.Mean) - (sigmaOfLog * sigmaOfLog / 2.0), sigmaOfLog);
                distribution = logNormal;
                inverseCdf = logNormal.InverseCumulativeDistribution;
                parameters = new Dictionary<string, double>
                {
                    ["mean"] = spec.Mean,
                    ["stdDev"] = spec.StdDev!.Value,
                };
                break;

            case DistributionFamily.Gamma:
                // Shape k is the user's spread; Scale is derived as Mean/k by BuildSpec,
                // and is recomputed here from the same two numbers so this factory cannot
                // disagree with the run's own sampler.
                Require(spec.Shape, "shape", spec.Family);
                var gamma = new Gamma(spec.Shape!.Value, spec.Mean / spec.Shape!.Value);
                distribution = gamma;
                inverseCdf = gamma.InverseCumulativeDistribution;
                parameters = new Dictionary<string, double>
                {
                    ["shape"] = spec.Shape!.Value,
                    ["scale"] = spec.Mean / spec.Shape!.Value,
                };
                break;

            case DistributionFamily.Uniform:
                if (spec.Min is not { } lo || spec.Max is not { } hi || !(hi > lo))
                {
                    throw new ArgumentException(
                        $"Cannot build a verification distribution for Uniform: bounds must satisfy Max > Min but were [{spec.Min}, {spec.Max}].",
                        nameof(spec));
                }

                var uniform = new ContinuousUniform(lo, hi);
                distribution = uniform;
                inverseCdf = uniform.InverseCumulativeDistribution;
                parameters = new Dictionary<string, double> { ["min"] = lo, ["max"] = hi };
                break;

            default:
                throw new ArgumentException(
                    $"No verification distribution is defined for family {spec.Family}.",
                    nameof(spec));
        }

        // The inverse CDF is captured per case from the concrete MathNet type, for the
        // same reason the class-level constructor does it: IContinuousDistribution
        // exposes CumulativeDistribution but not the inverse, which lives on the
        // concrete base class and is gone once the value is held as the interface.
        return new FittedDistribution(
            spec.Family.ToString(),
            parameters,
            distribution,
            distribution.CumulativeDistribution,
            inverseCdf,
            sampleSize,
            double.NaN);
    }

    private static void Require(double? value, string what, DistributionFamily family)
    {
        if (value is not { } v || !(v > 0) || double.IsNaN(v) || double.IsInfinity(v))
        {
            throw new ArgumentException(
                $"Cannot build a verification distribution for {family}: {what} must be a finite number > 0 but was {value}.",
                "spec");
        }
    }

    /// <summary>
    /// Converts an arithmetic mean/standard-deviation pair into the standard deviation
    /// of ln X, which is how MathNet parameterises <see cref="LogNormal"/>.
    /// </summary>
    /// <remarks>
    /// For X ~ LogNormal, Var(ln X) = ln(1 + σ²/μ²), so σ_ln = sqrt(ln(1 + (σ/μ)²)).
    /// The guard keeps the argument of ln positive: a σ/μ ratio large enough to make
    /// 1 + (σ/μ)² round to exactly 1 would otherwise return 0 and produce a
    /// zero-variance log-normal with no spread.
    /// </remarks>
    private static double LogNormalArithmeticStdDevToLogStdDev(double mean, double stdDev)
    {
        double ratioSquared = (stdDev / mean) * (stdDev / mean);
        double inside = 1.0 + ratioSquared;
        return inside > 1.0 ? Math.Sqrt(Math.Log(inside)) : 0.0;
    }
}