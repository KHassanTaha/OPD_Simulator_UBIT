namespace OpdSimulator.App.Models;

using OpdSimulator.App.Services;

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
/// <param name="InterArrivalMinutes">Observed inter-arrival gaps, for fitting and charts. Cross-session gaps are excluded (D-173).</param>
/// <param name="ServiceMinutesByStage">Observed service times per clinic stage, for fitting and charts.</param>
/// <remarks>
/// <see cref="FittedArrivalRate"/> is MLE λ, kept under its original name and its
/// original meaning. D-173 adds <see cref="WindowLambda"/> BESIDE it rather than
/// renaming anything: two live estimates, one chosen by the user, and renaming the
/// one every existing call site already reads would have bought nothing.
/// </remarks>
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

    /// <summary>
    /// λ₂ = total arrivals ÷ the file's operating minutes (D-173).
    /// </summary>
    /// <remarks>
    /// The second arrival-rate estimate, and the one that matches the window a
    /// run will actually simulate over. Null whenever <see cref="ObservedWindow"/>
    /// is null (no operating sessions) or fewer than two arrivals exist, which
    /// keeps it defined on exactly the same data as <see cref="FittedArrivalRate"/>.
    /// </remarks>
    public double? WindowLambda { get; init; }

    /// <summary>
    /// Operating time the file covers, or null when it declares no usable
    /// session dates (D-172).
    /// </summary>
    public ObservationWindow? ObservedWindow { get; init; }

    /// <summary>
    /// Distinct session dates parsed from the optional <c>session_date</c> column
    /// (D-175); null when the file has no such column.
    /// </summary>
    public IReadOnlyList<DateOnly>? SessionDates { get; init; }

    /// <summary>
    /// p_bypass = rows reaching a doctor with no screening record ÷ all arrivals
    /// (FR-DATA-7, D-179); null when it could not be computed.
    /// </summary>
    /// <remarks>
    /// An init property beside the other fitted quantities rather than a
    /// positional parameter, because it is an 8Q.2 addition: every existing
    /// construction site keeps its meaning, and the three values below default to
    /// the "not measured" state.
    /// </remarks>
    public double? FittedBypassProbability { get; init; }

    /// <summary>Rows that reached a doctor without a screening record (D-179).</summary>
    public int BypassExits { get; init; }

    /// <summary>Rows that were actually screened — the p_exit denominator (D-179).</summary>
    public int ScreenedPatients { get; init; }
}