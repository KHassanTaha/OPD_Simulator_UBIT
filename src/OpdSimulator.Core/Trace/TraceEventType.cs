namespace OpdSimulator.Core.Trace;

/// <summary>
/// The kinds of rows a human-readable event trace contains.
/// </summary>
/// <remarks>
/// Five of the values mirror the events a patient can experience (the PRD
/// FR-SIM-2 set plus the routing hand-off), and the sixth (<see cref="Rng"/>)
/// records the random-number draws behind those events. The engine emits one
/// <see cref="TraceEvent"/> per occurrence; <see cref="TraceFormatter"/>
/// decides how each row is rendered for a given <see cref="TraceLevel"/>.
/// </remarks>
public enum TraceEventType
{
    /// <summary>An arrival is admitted to the first stage.</summary>
    Arrival = 0,

    /// <summary>A server starts serving a patient.</summary>
    StartService = 1,

    /// <summary>A server finishes serving a patient (the row carries the next destination).</summary>
    EndService = 2,

    /// <summary>A patient is handed off from one stage to the next.</summary>
    Route = 3,

    /// <summary>A patient leaves the system (after the exit stage or the last stage).</summary>
    Exit = 4,

    /// <summary>A random-number draw (seed, uniform deviate, sampled value).</summary>
    Rng = 5,
}