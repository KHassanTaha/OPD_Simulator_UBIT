namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Data.Fitting;

/// <summary>
/// Fits a distribution family to a sample series and runs the chi-square
/// goodness-of-fit, producing the <see cref="FitReport"/> rows shown in the
/// results panel (FR-STAT-8). Any failure (unknown family, degenerate samples,
/// a broken fit) yields a report with null results so callers render a clean
/// "fit unavailable" line instead of crashing.
/// </summary>
public static class FitsService
{
    /// <summary>Significance level used for every chi-square verdict (5%).</summary>
    public const double DefaultAlpha = 0.05;

    /// <summary>
    /// Fits <paramref name="familyName"/> to <paramref name="samples"/> and
    /// tests the result.
    /// </summary>
    public static FitReport Fit(string label, IReadOnlyList<double> samples, string familyName)
    {
        if (samples.Count == 0)
        {
            return new FitReport(label, samples, null, null);
        }

        if (!DistributionFitterFactory.TryCreate(familyName, out var fitter))
        {
            return new FitReport(label, samples, null, null);
        }

        try
        {
            var fitted = fitter!.Fit(samples);
            var chiSquare = ChiSquareTest.Run(samples, fitted, DefaultAlpha);
            return new FitReport(label, samples, fitted, chiSquare);
        }
        catch (Exception)
        {
            // Degenerate samples (e.g. all-zero service times) are legitimate
            // GUI input; surface a null report rather than an exception.
            return new FitReport(label, samples, null, null);
        }
    }
}