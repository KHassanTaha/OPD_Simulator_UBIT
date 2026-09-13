namespace OpdSimulator.Data.Preprocess;

/// <summary>
/// The OPD clinic's canonical stage flow order (CONTEXT §1.2).
/// </summary>
/// <remarks>
/// Stage detection (<see cref="StagePairDetector"/>) is deliberately generic
/// (N stages, FR-DATA-9), but the simulator models one specific clinic whose
/// flow is fixed: Reception → Screening → Doctor, with probabilistic exit
/// after Screening. Validation and network construction need to know this
/// order to decide, for example, that a patient who exited at Screening
/// legitimately has no <c>doctor_*</c> times. Keeping it in the Data layer
/// means the engine never needs clinic knowledge — it only consumes an
/// ordered <c>NetworkTopology</c>.
/// </remarks>
public static class ClinicStageOrder
{
    /// <summary>
    /// Clinic flow order: <c>Reception</c>, <c>Screening</c>, <c>Doctor</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Flow = new[] { "Reception", "Screening", "Doctor" };

    /// <summary>
    /// Returns the flow index of a stage name (case-insensitive), or
    /// <see cref="int.MaxValue"/> when the stage is not part of the clinic flow.
    /// </summary>
    /// <param name="stage">The stage name.</param>
    /// <returns>0 for Reception, 1 for Screening, 2 for Doctor;
    /// <see cref="int.MaxValue"/> for anything else.</returns>
    public static int FlowIndex(string stage)
    {
        for (int i = 0; i < Flow.Count; i++)
            if (string.Equals(Flow[i], stage, StringComparison.OrdinalIgnoreCase))
                return i;
        return int.MaxValue;
    }
}