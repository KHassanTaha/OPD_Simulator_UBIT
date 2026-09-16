namespace OpdSimulator.App.Models;

/// <summary>
/// What a successfully analysed data file contributes to the run: the original
/// dataset, its validation verdict, and the fitted parameters the upload
/// surfaces (D-104). Mirrors the M5 binding record and the CLI's
/// <c>simulate-data</c> loading stage.
/// </summary>
/// <param name="SourcePath">Full path of the source file.</param>
/// <param name="DataSet">The loaded dataset, or null when the file could not be parsed.</param>
/// <param name="Issues">Validator issues; an empty list means the file is fully valid.</param>
/// <param name="ErrorMessage">Clean human error when the file could not be read at all.</param>
/// <param name="FittedArrivalRate">λ₀ = 1 / mean(inter-arrival), when at least two arrivals exist.</param>
/// <param name="StageNames">Detected clinic stages in flow order, one per fitted rate.</param>
/// <param name="FittedServiceRates">μ per detected stage = 1 / mean(service time); NaN where a stage has no timings.</param>
/// <param name="FittedExitProbability">p_exit from departure_stage counts; null when undefined (D-008).</param>
/// <param name="ScreeningExits">Rows whose departure_stage is Screening.</param>
/// <param name="DoctorExits">Rows whose departure_stage is Doctor.</param>
/// <param name="ReceptionExcluded">Rows excluded from the p_exit denominator (left at Reception — reneging).</param>
/// <param name="InterArrivalMinutes">Observed inter-arrival gaps, for fitting and charts.</param>
/// <param name="ServiceMinutesByStage">Observed service times per clinic stage, for fitting and charts.</param>
public sealed record DataBindingResult(
    string? SourcePath,
    OpdSimulator.Data.Loaders.DataSet? DataSet,
    IReadOnlyList<OpdSimulator.Data.Validation.ValidationIssue> Issues,
    string? ErrorMessage,
    double? FittedArrivalRate,
    IReadOnlyList<string> StageNames,
    IReadOnlyList<double> FittedServiceRates,
    double? FittedExitProbability,
    int ScreeningExits,
    int DoctorExits,
    int ReceptionExcluded,
    IReadOnlyList<double> InterArrivalMinutes,
    IReadOnlyDictionary<string, IReadOnlyList<double>> ServiceMinutesByStage)
{
    /// <summary>Whether the file loaded and every row passed validation.</summary>
    public bool IsUsable => DataSet is not null && ErrorMessage is null && Issues.Count == 0;
}