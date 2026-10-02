namespace OpdSimulator.Data.Preprocess;

/// <summary>
/// Estimate of the post-Screening exit probability <c>p_exit</c> and the counts behind it.
/// </summary>
/// <param name="ExitProbability">p_exit = Screening exits / screened patients (D-179).</param>
/// <param name="ScreenedPatients">
/// Rows that were actually screened — the denominator. This was
/// <c>TotalCandidates</c>, which counted every row with a usable
/// <c>departure_stage</c> and therefore included patients who never had a
/// screening record at all. Renamed because it now counts something narrower and
/// more honest: patients who were screened.
/// </param>
/// <param name="ScreeningExits">Rows whose departure stage is Screening.</param>
/// <param name="DoctorExits">Rows whose departure stage is Doctor.</param>
/// <param name="BypassExits">
/// Rows that went from Reception straight to a doctor with no screening record
/// (D-179). They are counted but excluded from p_exit, because p_exit answers
/// "of the patients we screened, how many stopped there?" and a patient who was
/// never screened cannot have stopped at Screening.
/// </param>
/// <param name="ReceptionExited">
/// Rows with <c>departure_stage = Reception</c>, excluded from p_exit (reneging,
/// out of scope; D-008).
/// </param>
public sealed record PExitResult(
    double ExitProbability,
    int ScreenedPatients,
    int ScreeningExits,
    int DoctorExits,
    int BypassExits,
    int ReceptionExited);