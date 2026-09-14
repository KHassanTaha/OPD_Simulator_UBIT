namespace OpdSimulator.Core.Trace;

/// <summary>
/// A <see cref="ITraceSink"/> that discards every event.
/// </summary>
/// <remarks>
/// Lets callers build traces unconditionally and switch them off by injecting
/// this single instance instead of writing special cases around a possibly-null
/// sink.
/// </remarks>
public sealed class NullTraceSink : ITraceSink
{
    /// <summary>Shared no-op instance.</summary>
    public static readonly NullTraceSink Instance = new();

    private NullTraceSink()
    {
    }

    /// <inheritdoc />
    public void Write(TraceEvent evt)
    {
    }

    /// <inheritdoc />
    public void Flush()
    {
    }
}