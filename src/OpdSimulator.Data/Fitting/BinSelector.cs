namespace OpdSimulator.Data.Fitting;

/// <summary>
/// Chooses bin counts and equal-probability bin edges for the chi-square test (FR-STAT-3).
/// </summary>
/// <remarks>
/// Two DFT-standard design rules, both easy to defend in the viva:
/// <list type="bullet">
/// <item><b>Bin count</b> follows the square-root rule <c>k = ceil(√n)</c>, clamped to
/// [5,20] so very small samples still give a usable test and very large samples do
/// not explode the report.</item>
/// <item><b>Edges</b> are equal-probability: interior cut-points are the fitted
/// distribution's quantiles at i/k, so every interior bin is expected to hold n/k
/// observations. Endpoints are the sample min/max, which makes each bin non-empty
/// for the observed data.</item>
/// </list>
/// </remarks>
public static class BinSelector
{
    /// <summary>
    /// Computes the number of bins for a sample of the given size.
    /// </summary>
    /// <param name="sampleSize">Number of observations.</param>
    /// <returns>ceil(√n) clamped to [5, 20].</returns>
    public static int BinCount(int sampleSize)
        => Math.Clamp((int)Math.Ceiling(Math.Sqrt(sampleSize)), 5, 20);

    /// <summary>
    /// Computes k+1 equal-probability bin edges from the fitted distribution.
    /// </summary>
    /// <param name="binCount">Number of bins (see <see cref="BinCount"/>).</param>
    /// <param name="samples">The observed sample (defines the outer endpoints).</param>
    /// <param name="fitted">The fitted distribution (defines the interior quantiles).</param>
    /// <returns>Edges array of length <paramref name="binCount"/> + 1, strictly increasing.</returns>
    /// <exception cref="ArgumentException">If <paramref name="samples"/> is empty.</exception>
    public static double[] EqualProbabilityEdges(int binCount, IReadOnlyList<double> samples, Func<double, double> inverseCdf)
    {
        if (samples.Count == 0)
            throw new ArgumentException("Cannot compute bin edges for an empty sample.");

        var edges = new double[binCount + 1];
        edges[0] = samples.Min();
        edges[binCount] = samples.Max();
        for (int i = 1; i < binCount; i++)
            edges[i] = inverseCdf((double)i / binCount);

        // Guard against quantile non-monotonicity (numerical noise on flat densities).
        for (int i = 1; i < edges.Length; i++)
        {
            if (edges[i] <= edges[i - 1])
                edges[i] = edges[i - 1] + 1e-9;
        }

        return edges;
    }

    /// <summary>
    /// Counts how many <paramref name="samples"/> fall into each bin defined by <paramref name="edges"/>.
    /// </summary>
    /// <param name="samples">Observations.</param>
    /// <param name="edges">Length-k+1 increasing bin edges.</param>
    /// <returns>k observed frequencies.</returns>
    public static int[] ObservedFrequencies(IReadOnlyList<double> samples, IReadOnlyList<double> edges)
    {
        var counts = new int[edges.Count - 1];
        foreach (double x in samples)
        {
            // Find the first bin whose upper edge is > x (sample max equals the last edge).
            int bin = -1;
            for (int i = 1; i < edges.Count; i++)
            {
                if (x <= edges[i])
                {
                    bin = i - 1;
                    break;
                }
            }
            if (bin < 0)
                bin = counts.Length - 1; // exact maximum value
            counts[bin]++;
        }
        return counts;
    }
}