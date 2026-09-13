namespace OpdSimulator.Data.Preprocess;

/// <summary>
/// Computes inter-arrival times from arrival times (FR-DATA-3).
/// </summary>
/// <remarks>
/// Given monotonically non-decreasing arrival times (the validator enforces this),
/// the inter-arrival time of patient i is <c>arrival[i] - arrival[i-1]</c>. These
/// differences are the sample the inter-arrival distribution is fitted to — the
/// quantity M/M/c theory treats as exponential.
/// </remarks>
public static class InterArrivalCalculator
{
    /// <summary>
    /// Computes the gap between each consecutive pair of arrival times.
    /// </summary>
    /// <param name="arrivalMinutes">Parsed arrival times, minutes since midnight, non-decreasing.</param>
    /// <returns>Inter-arrival times (length = arrival count − 1).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="arrivalMinutes"/> is null.</exception>
    public static double[] Compute(IReadOnlyList<double> arrivalMinutes)
    {
        ArgumentNullException.ThrowIfNull(arrivalMinutes);

        if (arrivalMinutes.Count < 2)
            return Array.Empty<double>();

        var gaps = new double[arrivalMinutes.Count - 1];
        for (int i = 1; i < arrivalMinutes.Count; i++)
            gaps[i - 1] = arrivalMinutes[i] - arrivalMinutes[i - 1];
        return gaps;
    }
}