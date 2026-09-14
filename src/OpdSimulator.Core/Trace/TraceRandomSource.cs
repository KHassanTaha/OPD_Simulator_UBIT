namespace OpdSimulator.Core.Trace;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Passively observing wrapper around an <see cref="IRandomSource"/> that
/// counts draws and remembers the latest uniform deviate, so the trace can show
/// exactly what the RNG supplied without disturbing the draw sequence.
/// </summary>
/// <remarks>
/// <para>
/// This wrapper is <em>transparent</em>: <see cref="NextDouble"/> and
/// <see cref="SetSeed"/> forward straight to the inner source, and
/// <see cref="SetSeed"/> additionally resets the observation counters. Wrapping
/// the source does not change the random stream in any way, so the engine can
/// always run through this wrapper and the Milestone-1 metrics (seed 42:
/// served = 29892, wait = 0.724) remain byte-identical.
/// </para>
/// <para>
/// The wrapper only observes — it knows nothing about what a draw means. The
/// engine gives each draw its meaning (inter-arrival time, service time,
/// routing coin, server pick) by reading <see cref="LastDraw"/> and
/// <see cref="DrawCount"/> immediately after the draw and emitting a
/// <see cref="TraceEventType.Rng"/> row with the description.
/// </para>
/// </remarks>
public sealed class TraceRandomSource : IRandomSource
{
    private readonly IRandomSource _inner;
    private int _drawCount;
    private double _lastDraw;

    /// <summary>
    /// Creates the wrapper around an existing source.
    /// </summary>
    /// <param name="inner">The random source whose draws are observed.</param>
    public TraceRandomSource(IRandomSource inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>Number of <see cref="NextDouble"/> draws since the last <see cref="SetSeed"/>.</summary>
    public int DrawCount => _drawCount;

    /// <summary>The uniform deviate produced by the most recent draw (0 before any draw).</summary>
    public double LastDraw => _lastDraw;

    /// <inheritdoc />
    public void SetSeed(int seed)
    {
        _inner.SetSeed(seed);
        _drawCount = 0;
        _lastDraw = 0;
    }

    /// <inheritdoc />
    public double NextDouble()
    {
        double u = _inner.NextDouble();
        _drawCount++;
        _lastDraw = u;
        return u;
    }
}