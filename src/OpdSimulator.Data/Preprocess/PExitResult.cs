namespace OpdSimulator.Data.Preprocess;

/// <summary>
/// Estimate of the post-Screening exit probability <c>p_exit</c> and the counts behind it.
/// </summary>
/// <param name="ExitProbability">p_exit = Screening exits / (Screening + Doctor exits).</param>
/// <param name="TotalCandidates">Rows with a valid departure stage (Screening or Doctor).</param>
/// <param name="ScreeningExits">Rows whose departure stage is Screening.</param>
/// <param name="DoctorExits">Rows whose departure stage is Doctor.</param>
/// <param name="ReceptionExcluded">Rows with <c>departure_stage = Reception</c>, excluded from p_exit (FR-DATA-6, D-008).</param>
public sealed record PExitResult(
    double ExitProbability,
    int TotalCandidates,
    int ScreeningExits,
    int DoctorExits,
    int ReceptionExcluded);