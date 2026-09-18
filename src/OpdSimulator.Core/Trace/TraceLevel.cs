namespace OpdSimulator.Core.Trace;

/// <summary>
/// How much detail a human-readable event trace contains.
/// </summary>
/// <remarks>
/// The levels form a strict ladder — <see cref="Standard"/> &lt; <see cref="Detailed"/>
/// &lt; <see cref="Debug"/>. They only control the <em>rendered text</em> (via
/// <see cref="TraceFormatter"/>), never the engine's event stream: the engine
/// always emits the same <see cref="TraceEvent"/> sequence through an
/// <see cref="ITraceSink"/>, so a rerun at a different level reproduces the
/// same events, merely displaying more or fewer columns/rows. Keeping the
/// engine blind to the level guarantees the simulation arithmetic is
/// level-independent and therefore verifiable.
/// </remarks>
public enum TraceLevel
{
    /// <summary>Lowest level: no trace rows are collected (the level for a non-trace run).</summary>
    Minimal = 0,

    /// <summary>One row per patient event (arrival, service start/end, exit), with only the core columns.</summary>
    Standard = 1,

    /// <summary>
    /// <see cref="Standard"/> plus the state-bearing columns — the serving server
    /// id and the routing/exit destination. This is the default for the
    /// <c>trace</c> CLI command.
    /// </summary>
    Detailed = 2,

    /// <summary>
    /// <see cref="Detailed"/> plus a row for every random-number draw (seed,
    /// uniform deviate, and the sampled time or routing decision it produced).
    /// </summary>
    Debug = 3,
}