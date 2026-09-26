namespace OpdSimulator.Data.Fitting;

/// <summary>
/// The outcome of a general fit: the winning candidate (or null
/// when all candidates were rejected) plus every candidate that
/// was tried, in the order they were evaluated.
/// </summary>
/// <param name="Best">The winning candidate with the lowest
/// AIC among the non-rejected candidates, or null when none
/// survived.</param>
/// <param name="AllCandidates">Every candidate evaluated,
/// including rejected ones.</param>
public sealed record GeneralFitResult(
    DistributionFitResult? Best,
    IReadOnlyList<DistributionFitResult> AllCandidates);
