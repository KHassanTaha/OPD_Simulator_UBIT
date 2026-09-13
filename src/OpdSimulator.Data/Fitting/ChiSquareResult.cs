namespace OpdSimulator.Data.Fitting;

/// <summary>
/// Outcome of a chi-square goodness-of-fit test (FR-STAT-4).
/// </summary>
/// <param name="Statistic">Σ(Oᵢ − Eᵢ)²/Eᵢ.</param>
/// <param name="DegreesOfFreedom">k − 1 − p, where p is the fitted parameter count.</param>
/// <param name="PValue">P(χ² ≥ statistic) under the null hypothesis (data follows the fitted family).</param>
/// <param name="Alpha">The significance level used for the decision.</param>
/// <param name="RejectFit"><see langword="true"/> when PValue &lt; Alpha ⇒ the fit is rejected.</param>
/// <param name="Observed">Observed frequency per bin.</param>
/// <param name="Expected">Expected frequency per bin (n/k with equal-probability bins).</param>
/// <param name="BinEdges">The bin edges that were used (k+1 entries).</param>
public sealed record ChiSquareResult(
    double Statistic,
    int DegreesOfFreedom,
    double PValue,
    double Alpha,
    bool RejectFit,
    IReadOnlyList<int> Observed,
    IReadOnlyList<double> Expected,
    IReadOnlyList<double> BinEdges)
{
    /// <summary>Human-readable verdict: "Reject" or "Fail to reject".</summary>
    public string Decision => RejectFit ? "Reject" : "Fail to reject";
}