namespace OpdSimulator.Core.Trace;

/// <summary>
/// Receives the ordered <see cref="TraceEvent"/> stream produced by the engine.
/// </summary>
/// <remarks>
/// This interface is the seam that keeps the simulation core independent of any
/// presentation: the engine calls <see cref="Write"/> for every event and never
/// formats, filters, or persists anything itself. A sink may collect the events
/// in memory (UI, tests), render them to text immediately (<see cref="TextWriterTraceSink"/>),
/// or discard them (<see cref="NullTraceSink"/>).
/// </remarks>
public interface ITraceSink
{
    /// <summary>
    /// Records one trace event in simulation order.
    /// </summary>
    /// <param name="evt">The event to record.</param>
    void Write(TraceEvent evt);

    /// <summary>
    /// Flushes any buffered output so the consumer sees all events written so far.
    /// </summary>
    void Flush();
}