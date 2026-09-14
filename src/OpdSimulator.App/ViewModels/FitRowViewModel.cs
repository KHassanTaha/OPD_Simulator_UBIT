namespace OpdSimulator.App.ViewModels;

using System.Globalization;
using OpdSimulator.App.Models;

/// <summary>
/// One chi-square result row for the results panel (FR-STAT-8): the series
/// label plus a human line stating the fitted parameters, the test statistic
/// and its verdict.
/// </summary>
public sealed class FitRowViewModel : ViewModelBase
{
    /// <summary>Gets the series label (e.g. "Inter-arrival", "Reception service").</summary>
    public string Label { get; }

    /// <summary>Gets the rendered result line.</summary>
    public string Detail { get; }

    /// <summary>Gets the semantic verdict text ("Pass"/"Fail"/"N/A").</summary>
    public string Verdict { get; }

    private FitRowViewModel(string label, string detail, string verdict)
    {
        Label = label;
        Detail = detail;
        Verdict = verdict;
    }

    /// <summary>Builds the display row from a fit report.</summary>
    public static FitRowViewModel From(FitReport report)
    {
        if (report.Fitted is null || report.ChiSquare is null)
        {
            return new FitRowViewModel(report.Label,
                "Fit unavailable (no samples or the chosen family could not fit this series).", "N/A");
        }

        var parameters = string.Join(", ",
            report.Fitted.Parameters.Select(p => p.Key + " = " + p.Value.ToString("0.###", CultureInfo.InvariantCulture)));

        var chi = report.ChiSquare;
        bool passed = !chi.RejectFit;
        string verdict = passed ? "Pass" : "Fail";
        return new FitRowViewModel(
            report.Label,
            $"{report.Fitted.Name} fit ({parameters}); χ² = {chi.Statistic:0.###} (df = {chi.DegreesOfFreedom}, p = {chi.PValue:0.###}), α = {chi.Alpha:0.##} → {verdict}",
            verdict);
    }
}