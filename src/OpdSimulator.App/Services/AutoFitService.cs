namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Data.Fitting;

/// <summary>
/// One fitted stage series, paired with the name of the stage it came from
/// (Phase 9C, D-231). Stage names stay beside their fits so a report cannot be
/// applied to the wrong row when the panel's stage order differs from the file's.
/// </summary>
/// <param name="StageName">Stage name as reported by the loaded file.</param>
/// <param name="Fit">Winner and rejected candidates for that stage's service times.</param>
public sealed record StageAutoFit(string StageName, GeneralFitResult Fit);

/// <summary>
/// Every data-driven family decision made on one load (Phase 9C, D-231): the
/// engine-wide inter-arrival series plus one series per stage in the file's own
/// order. No UI types and no cached state — the same samples and α always
/// produce the same report.
/// </summary>
/// <param name="InterArrival">Fit of <see cref="DataBindingResult.InterArrivalMinutes"/>.</param>
/// <param name="Stages">Service fits, in <see cref="DataBindingResult.StageNames"/> order.</param>
public sealed record AutoFitReport(
    GeneralFitResult InterArrival,
    IReadOnlyList<StageAutoFit> Stages);

/// <summary>
/// Chooses the family and spread that the uploaded data supports (Phase 9C,
/// D-231). This service is deliberately thin: <see cref="GeneralDistributionFitter"/>
/// already owns the candidate rules, the AIC ranking and the chi-square sanity
/// check, so a second selection algorithm here would be a second thing to
/// defend in the viva. Deterministic is not a candidate — it is a user choice.
/// </summary>
public static class AutoFitService
{
    /// <summary>
    /// Fits the inter-arrival series and every stage's service series from
    /// <paramref name="binding"/>. A short or fully rejected series keeps its
    /// <see cref="GeneralFitResult.Best"/> null; the caller then keeps the current
    /// family and discloses why (D-237). Nothing is written and nothing throws.
    /// </summary>
    /// <param name="binding">Analysed data file; its sample lists are the only inputs.</param>
    /// <param name="alpha">Significance level passed to every candidate's χ² check.</param>
    /// <returns>The winner per series, with the per-stage order matching the file.</returns>
    public static AutoFitReport Fit(DataBindingResult binding, double alpha)
    {
        var interArrival = GeneralDistributionFitter.FitBest(binding.InterArrivalMinutes, alpha);

        var stages = new List<StageAutoFit>(binding.StageNames.Count);
        foreach (string stage in binding.StageNames)
        {
            // A stage the file names but has no timings for is a refusal, not a
            // reason to fit another stage's samples under this name.
            var samples = binding.ServiceMinutesByStage.TryGetValue(stage, out var times)
                ? times
                : Array.Empty<double>();
            stages.Add(new StageAutoFit(stage, GeneralDistributionFitter.FitBest(samples, alpha)));
        }

        return new AutoFitReport(interArrival, stages);
    }
}
