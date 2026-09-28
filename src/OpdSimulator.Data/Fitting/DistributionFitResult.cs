namespace OpdSimulator.Data.Fitting;

using OpdSimulator.Core.Distributions;

/// <summary>
/// The result of fitting one candidate family to a sample set.
/// Both accepted and rejected candidates are represented; the
/// caller inspects <see cref="Rejected"/> and
/// <see cref="RejectionReason"/>.
/// </summary>
/// <param name="Family">The candidate family.</param>
/// <param name="Spec">The fitted parameters, ready to build
/// a sampler from.</param>
/// <param name="LogLikelihood">Sum of log-pdf at each sample.</param>
/// <param name="Aic">Akaike Information Criterion
/// (2k − 2·ln L).</param>
/// <param name="Bic">Bayesian Information Criterion
/// (k·ln n − 2·ln L).</param>
/// <param name="ChiSquare">Chi-square goodness of fit. Null
/// when the family is Deterministic or when the fit was
/// rejected before chi-square.</param>
/// <param name="Rejected">True when a rule disqualified this
/// candidate.</param>
/// <param name="RejectionReason">Human-readable reason when
/// Rejected is true; null otherwise.</param>
public sealed record DistributionFitResult(
    DistributionFamily Family,
    DistributionSpec Spec,
    double LogLikelihood,
    double Aic,
    double Bic,
    ChiSquareResult? ChiSquare,
    bool Rejected,
    string? RejectionReason);
