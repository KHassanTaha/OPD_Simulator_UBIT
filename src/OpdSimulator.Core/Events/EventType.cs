namespace OpdSimulator.Core.Events;

/// <summary>
/// The four event types defined by PRD FR-SIM-2.
/// </summary>
/// <remarks>
/// The enum's underlying values are significant: they define the deterministic
/// tie-break order in <see cref="Event.CompareTo"/> when two events share the
/// same clock time (arrival before service completions, earlier stages before
/// later ones). The value also maps a stage index to its completion event
/// (stage 0 → <see cref="ReceptionEnd"/>, 1 → <see cref="ScreeningEnd"/>,
/// 2 → <see cref="DoctorEnd"/>), which keeps the engine N-stage generic.
/// </remarks>
public enum EventType
{
    /// <summary>A new patient arrives at the first stage queue.</summary>
    Arrival = 0,

    /// <summary>Reception (stage 0) finished serving a patient.</summary>
    ReceptionEnd = 1,

    /// <summary>Screening (stage 1) finished serving a patient.</summary>
    ScreeningEnd = 2,

    /// <summary>Doctor consultation (stage 2) finished serving a patient.</summary>
    DoctorEnd = 3,
}