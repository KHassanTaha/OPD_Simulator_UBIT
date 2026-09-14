namespace OpdSimulator.App.Models;

/// <summary>
/// One goodness-of-fit result pair: the <see cref="Fitted"/> distribution a
/// series was fit to plus its <see cref="ChiSquare"/> verdict. The results
/// panel and the Input Analysis tab both render these as rows (FR-STAT-8).
/// </summary>
/// <param name="Label">Human label, e.g. "Inter-arrival" or "Reception service".</param>
/// <param name="Samples">The observed series the distribution was fit to.</param>
/// <param name="Fitted">The fitted distribution (null when fitting failed).</param>
    /// <param name="ChiSquare">The chi-square verdict, or null when no fit ran.</param>
public sealed record FitReport(
    string Label,
    IReadOnlyList<double> Samples,
    OpdSimulator.Data.Fitting.FittedDistribution? Fitted,
    OpdSimulator.Data.Fitting.ChiSquareResult? ChiSquare);