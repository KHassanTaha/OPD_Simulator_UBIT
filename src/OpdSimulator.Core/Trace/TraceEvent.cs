namespace OpdSimulator.Core.Trace;

/// <summary>
/// One item of a simulation trace: a point-in-time event plus the state values
/// that make it human-readable.
/// </summary>
/// <remarks>
/// <para>
/// The trace is deliberately a different channel from the Serilog event log
/// (FR-VAL-4). Serilog logs the <em>whole</em> run with free text and structure
/// for debugging; <see cref="TraceEvent"/> is a compact, ordered, deterministic
/// sequence that turns the simulation into a line-by-line story — the paper trail
/// used to verify a run by hand in the viva.
/// </para>
/// <para>
/// Semantics of <see cref="QueueLength"/> per row type (<see cref="TraceFormatter"/>
/// documents the columns; the engine owns these values):
/// <list type="bullet">
///   <item><see cref="TraceEventType.Arrival"/> — first-stage queue length plus one:
///   the arriving patient is present in the stage whether queued or already served.</item>
///   <item><see cref="TraceEventType.StartService"/> — the stage's queue length after any
///   dequeue has happened.</item>
///   <item><see cref="TraceEventType.EndService"/> — the stage's queue length before the
///   freed server pulls the next patient.</item>
///   <item><see cref="TraceEventType.Route"/> — the next stage's queue length right after
///   the patient was enqueued (or found an idle server).</item>
///   <item><see cref="TraceEventType.Exit"/> — null (the patient has left the system).</item>
/// </list>
/// </para>
/// </remarks>
public sealed record TraceEvent
{
    /// <summary>
    /// Creates a trace item.
    /// </summary>
    /// <param name="time">Simulation clock time (minutes from t = 0) at which the event happened.</param>
    /// <param name="type">The kind of row.</param>
    /// <param name="patientId">The patient the row concerns; null for draws that concern no single patient (e.g. the seed row).</param>
    /// <param name="stageName">The stage the row's location column names; null when no stage applies.</param>
    /// <param name="serverId">The zero-based server index for service rows; null otherwise.</param>
    /// <param name="queueLength">The queue-length column value; null renders as <c>q=-</c>.</param>
    /// <param name="details">Free-form suffix shown at state/rng levels, e.g. <c>→ exit</c> or a draw description.</param>
    public TraceEvent(
        double time,
        TraceEventType type,
        int? patientId,
        string? stageName,
        int? serverId,
        int? queueLength,
        string? details)
    {
        Time = time;
        Type = type;
        PatientId = patientId;
        StageName = stageName;
        ServerId = serverId;
        QueueLength = queueLength;
        Details = details;
    }

    /// <summary>Simulation clock time (minutes from t = 0) at which the event happened.</summary>
    public double Time { get; }

    /// <summary>The kind of row.</summary>
    public TraceEventType Type { get; }

    /// <summary>The patient the row concerns, or null for RNG-only rows.</summary>
    public int? PatientId { get; }

    /// <summary>The stage named in the location column, or null when none applies.</summary>
    public string? StageName { get; }

    /// <summary>The zero-based serving-server index for service rows, or null otherwise.</summary>
    public int? ServerId { get; }

    /// <summary>The queue-length column value, or null for rows without one.</summary>
    public int? QueueLength { get; }

    /// <summary>Free-form suffix shown at state/rng levels.</summary>
    public string? Details { get; }
}