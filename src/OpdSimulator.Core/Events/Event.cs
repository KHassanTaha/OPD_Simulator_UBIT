namespace OpdSimulator.Core.Events;

/// <summary>
/// A single future event scheduled on the <see cref="FEL"/>: something that
/// must happen at a specific simulation clock time.
/// </summary>
/// <remarks>
/// <see cref="Event"/> implements <see cref="IComparable{T}"/> so the
/// <see cref="FEL"/> (a <see cref="System.Collections.Generic.PriorityQueue{TElement,TPriority}"/>)
/// can order events by time. Ordering is deliberately total and deterministic:
/// time first, then event type (enum value), then patient id. This guarantees a
/// fixed, reproducible event sequence for a given random seed (NFR-4) — the
/// tie-break cannot silently change between runs.
/// </remarks>
public sealed class Event : IComparable<Event>
{
    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="time">Clock time at which the event is due.</param>
    /// <param name="type">The kind of event.</param>
    /// <param name="patientId">Identifier of the patient the event concerns.</param>
    public Event(double time, EventType type, int patientId)
    {
        Time = time;
        Type = type;
        PatientId = patientId;
    }

    /// <summary>Clock time (minutes from t=0) at which the event is due.</summary>
    public double Time { get; }

    /// <summary>The kind of event.</summary>
    public EventType Type { get; }

    /// <summary>Identifier of the patient this event concerns.</summary>
    public int PatientId { get; }

    /// <summary>
    /// Orders events: ascending time, then ascending event type, then ascending patient id.
    /// </summary>
    /// <param name="other">The other event to compare against.</param>
    /// <returns>Negative if this event sorts first, zero if equal, positive otherwise.</returns>
    public int CompareTo(Event? other)
    {
        if (other is null)
            return 1;

        int byTime = Time.CompareTo(other.Time);
        if (byTime != 0)
            return byTime;

        int byType = Type.CompareTo(other.Type);
        if (byType != 0)
            return byType;

        return PatientId.CompareTo(other.PatientId);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Type} @ {Time:0.###} (patient {PatientId})";
}