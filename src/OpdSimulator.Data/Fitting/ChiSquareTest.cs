namespace OpdSimulator.Data.Fitting;

using MathNet.Numerics.Distributions;

/// <summary>
/// Runs the Pearson chi-square goodness-of-fit test with equal-probability bins (FR-STAT-3/4).
/// </summary>
/// <remarks>
/// <para>
/// Procedure: choose k bins (square-root rule, clamped), bin the data against the
/// fitted CDF, compute the statistic, then get the p-value from the χ² distribution
/// with k − 1 − p degrees of freedom (p = number of estimated parameters). This is
/// the textbook analysis the viva expects; MathNet's <see cref="ChiSquared"/> only
/// supplies the CDF — the test itself is computed here (D-…).
/// </para>
/// <para>
/// Guard: the classical rule <c>Eᵢ ≥ 1 for all i</c> (and typically ≥ 5) is
/// required, otherwise the χ² approximation is unreliable and the test must not
/// print a number pretending otherwise (fail loud).
/// </para>
/// </remarks>
public static class ChiSquareTest
{
    /// <summary>
    /// Performs the test for one sample and fitted distribution.
    /// </summary>
    /// <param name="samples">The observed continuous sample.</param>
    /// <param name="fitted">The fitted distribution to test against.</param>
    /// <param name="alpha">Significance level, e.g. 0.05.</param>
    /// <returns>The full test result with bins, statistic, p-value and decision.</returns>
    /// <exception cref="ArgumentException">If the sample is empty or alpha is outside (0,1).</exception>
    /// <exception cref="InvalidOperationException">If an expected bin count is below 1 (degenerate test).</exception>
    public static ChiSquareResult Run(IReadOnlyList<double> samples, FittedDistribution fitted, double alpha)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(fitted);

        if (samples.Count == 0)
            throw new ArgumentException("Cannot run a chi-square test on an empty sample.");
        if (alpha <= 0 || alpha >= 1)
            throw new ArgumentException("Alpha must be strictly between 0 and 1.");

        int bins = BinSelector.BinCount(samples.Count);
        double[] edges = BinSelector.EqualProbabilityEdges(bins, samples, fitted.InverseCdf);
        int[] observed = BinSelector.ObservedFrequencies(samples, edges);

        double expected = (double)samples.Count / bins;
        if (expected < 1.0)
            throw new InvalidOperationException(
                $"Chi-square test cannot be trusted here: expected bin count {expected:0.##} < 1. Provide more data.");

        double statistic = 0;
        for (int i = 0; i < observed.Length; i++)
        {
            double diff = observed[i] - expected;
            statistic += (diff * diff) / expected;
        }

        int parameterCount = fitted.Parameters.Count;
        int df = bins - 1 - parameterCount;
        if (df < 1)
            throw new InvalidOperationException(
                $"Degenerate chi-square test: {bins} bins − 1 − {parameterCount} fitted parameters gives {df} degrees of freedom.");

        double pValue = 1.0 - new ChiSquared(df).CumulativeDistribution(statistic);

        return new ChiSquareResult(
            Statistic: statistic,
            DegreesOfFreedom: df,
            PValue: pValue,
            Alpha: alpha,
            RejectFit: pValue < alpha,
            Observed: observed,
            Expected: Enumerable.Repeat(expected, observed.Length).ToList(),
            BinEdges: edges);
    }
}