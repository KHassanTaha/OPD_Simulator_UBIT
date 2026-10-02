namespace OpdSimulator.Data.Preprocess;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;

/// <summary>
/// Estimates the post-Screening exit probability from <c>departure_stage</c>
/// (FR-DATA-6), and reports how many patients bypassed Screening entirely
/// (FR-DATA-7, D-179).
/// </summary>
/// <remarks>
/// <para>
/// <b>p_exit = Screening exits / screened patients</b>. The denominator is the
/// number of rows carrying a screening record, i.e. a non-empty
/// <c>screening_start</c>.
///
/// </para>
/// <para>
/// The previous denominator was "Screening exits + Doctor exits", which quietly
/// counted every patient that had a <c>departure_stage</c> at all. That included
/// the patients who skipped screening entirely, so p_exit was being computed
/// against a population the question never asked about: of the patients we
/// screened, how many stopped there? A patient with no screening record cannot
/// have stopped at Screening, and letting them inflate the denominator biased
/// p_exit upward — the more direct-to-doctor traffic the clinic had, the more
/// p_exit was diluted by patients who never reached the coin toss.
/// </para>
/// </remarks>
public static class PExitCalculator
{
    /// <summary>
    /// Computes p_exit and the bypass rate from the departure_stage and
    /// screening columns.
    /// </summary>
    /// <param name="dataSet">Validated data.</param>
    /// <returns>The p_exit estimate, the bypass estimate, and the component counts.</returns>
    /// <exception cref="DataValidationException">
    /// If no row has a valid departure stage (p_exit is undefined), or if no row
    /// has a screening record (the p_exit denominator is zero).
    /// </exception>
    /// <exception cref="ArgumentNullException">If <paramref name="dataSet"/> is null.</exception>
    public static PExitResult Compute(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        int screening = 0;
        int doctor = 0;
        int reception = 0;
        int bypass = 0;

        bool canDetectBypass = dataSet.Columns.Any(c => c.Equals("screening_start", StringComparison.OrdinalIgnoreCase));

        foreach (var row in dataSet.Rows)
        {
            if (!row.TryGetValue("departure_stage", out string? stageText))
                continue;

            string stage = stageText.Trim();
            bool isDoctor = stage.Equals("Doctor", StringComparison.OrdinalIgnoreCase);
            if (stage.Equals("Screening", StringComparison.OrdinalIgnoreCase)) screening++;
            else if (isDoctor) doctor++;
            else if (stage.Equals("Reception", StringComparison.OrdinalIgnoreCase)) reception++;

            // Bypass is defined by the absence of a screening record on a row that
            // reached a doctor — not by a literal column. Inferring it from the data
            // means a file that never mentions bypass still measures it.
            //
            // Detection needs the column to EXIST. A missing cell in a file that has
            // the column means "not screened"; a missing column means the file
            // predates the schema and cannot answer the question at all, so nothing
            // is inferred and the old denominator stands.
            if (canDetectBypass && isDoctor)
            {
                bool wasScreened = row.TryGetValue("screening_start", out string? start) && !string.IsNullOrWhiteSpace(start);
                if (!wasScreened)
                    bypass++;
            }
            // Any other value fails validation before this point; ignore defensively.
        }

        // A bypassed patient reached a doctor without ever being screened, so it
        // must leave the numerator's population entirely: it is neither a screening
        // exit nor a screened patient who continued.
        int screened = screening + doctor - bypass;
        if (screened == 0)
            throw new DataValidationException(
                "No rows have a valid departure_stage (Screening or Doctor), so p_exit is undefined.");

        return new PExitResult(
            ExitProbability: (double)screening / screened,
            ScreenedPatients: screened,
            ScreeningExits: screening,
            DoctorExits: doctor,
            BypassExits: bypass,
            ReceptionExited: reception);
    }

    /// <summary>
    /// Computes <c>p_bypass</c>: the fraction of all arrivals that reach a doctor
    /// without a screening record (FR-DATA-7, D-179).
    /// </summary>
    /// <param name="dataSet">Validated data.</param>
    /// <param name="result">The p_exit counts from <see cref="Compute(DataSet)"/>.</param>
    /// <returns>
    /// p_bypass = bypass count / total arrivals, or 0 when the file has no rows.
    /// </returns>
    /// <remarks>
    /// The denominator is every arrival, not just the screened ones: p_bypass
    /// answers "what share of the people walking in skip Screening?", so a patient
    /// who reneges at Reception belongs in the denominator even though they are
    /// excluded from p_exit.
    /// </remarks>
    /// <exception cref="ArgumentNullException">If either argument is null.</exception>
    public static double ComputeBypassProbability(DataSet dataSet, PExitResult result)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(result);

        if (dataSet.Rows.Count == 0)
            return 0.0;

        return (double)result.BypassExits / dataSet.Rows.Count;
    }
}