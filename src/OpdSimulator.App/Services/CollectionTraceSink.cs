namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Trace;

/// <summary>
/// In-memory <see cref="ITraceSink"/> that renders each event through
/// <see cref="TraceFormatter"/> and keeps at most <see cref="Capacity"/> lines
/// (dropping the oldest) so the results panel can show the run's story without
/// unbounded memory growth on long diagnostic runs (D-104/D-105).
/// </summary>
public sealed class CollectionTraceSink : ITraceSink
{
    private readonly int _capacity;
    private readonly TraceLevel _level;
    private readonly List<string> _lines = new(1024);

    /// <summary>Default maximum number of rendered lines.</summary>
    public const int Capacity = 50_000;

    /// <summary>Creates the sink.</summary>
    /// <param name="level">Trace detail to render; <see cref="TraceLevel.None"/> keeps no lines.</param>
    /// <param name="capacity">Maximum retained line count.</param>
    /// <exception cref="ArgumentOutOfRangeException">If the capacity is less than 1.</exception>
    public CollectionTraceSink(TraceLevel level = TraceLevel.Events, int capacity = Capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _level = level;
        _capacity = capacity;
    }

    /// <summary>Gets the rendered lines in simulation order, oldest first.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <inheritdoc/>
    public void Write(TraceEvent evt)
    {
        if (_level == TraceLevel.None)
        {
            return;
        }

        string? line = TraceFormatter.Format(evt, _level);
        if (line is null)
        {
            return;
        }

        if (_lines.Count >= _capacity)
        {
            _lines.RemoveAt(0);
        }

        _lines.Add(line);
    }

    /// <inheritdoc/>
    public void Flush()
    {
    }
}