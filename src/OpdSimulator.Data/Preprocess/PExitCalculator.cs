namespace OpdSimulator.Data.Preprocess;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;

/// <summary>
/// Estimates the post-Screening exit probability from <c>departure_stage</c> (FR-DATA-6).
/// </summary>
/// <remarks>
/// <c>p_exit = Screening / (Screening + Doctor)</c>. Rows whose departure stage is
/// <c>Reception</c> are counted but excluded from both numerator and denominator
/// (leaving at Reception is reneging — out of scope; D-008).</remarks>
public static class PExitCalculator
{
    /// <summary>
    /// Computes p_exit from the departure_stage column.
    /// </summary>
    /// <param name="dataSet">Validated data.</param>
    /// <returns>The p_exit estimate and the component counts.</returns>
    /// <exception cref="DataValidationException">If no row has a valid departure stage (p_exit is undefined).</exception>
    /// <exception cref="ArgumentNullException">If <paramref name="dataSet"/> is null.</exception>
    public static PExitResult Compute(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        int screening = 0;
        int doctor = 0;
        int reception = 0;

        foreach (var row in dataSet.Rows)
        {
            if (!row.TryGetValue("departure_stage", out string? stageText))
                continue;

            string stage = stageText.Trim();
            if (stage.Equals("Screening", StringComparison.OrdinalIgnoreCase)) screening++;
            else if (stage.Equals("Doctor", StringComparison.OrdinalIgnoreCase)) doctor++;
            else if (stage.Equals("Reception", StringComparison.OrdinalIgnoreCase)) reception++;
            // Any other value fails validation before this point; ignore defensively.
        }

        int candidates = screening + doctor;
        if (candidates == 0)
            throw new DataValidationException(
                "No rows have a valid departure_stage (Screening or Doctor), so p_exit is undefined.");

        return new PExitResult(
            ExitProbability: (double)screening / candidates,
            TotalCandidates: candidates,
            ScreeningExits: screening,
            DoctorExits: doctor,
            ReceptionExcluded: reception);
    }
}