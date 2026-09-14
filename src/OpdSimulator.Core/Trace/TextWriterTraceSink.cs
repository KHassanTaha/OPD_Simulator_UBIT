namespace OpdSimulator.Core.Trace;

/// <summary>
/// A <see cref="ITraceSink"/> that writes each event to a text writer as one
/// formatted line, applying the desired <see cref="TraceLevel"/> immediately.
/// </summary>
/// <remarks>
/// Streaming renderer used by the <c>trace</c> CLI command: it wraps
/// <see cref="System.IO.TextWriter"/> (usually <see cref="System.Console.Out"/>
/// or a <see cref="System.IO.StreamWriter"/> over a file) and delegates the
/// line layout to <see cref="TraceFormatter"/>, so one code path produces both
/// the console and the file copy of a trace.
/// </remarks>
public sealed class TextWriterTraceSink : ITraceSink
{
    private readonly TextWriter _writer;
    private readonly TraceLevel _level;
    private readonly double _realStartMinutes;

    /// <summary>
    /// Creates the sink bound to a writer and level.
    /// </summary>
    /// <param name="writer">Where formatted lines are written.</param>
    /// <param name="level">The detail level applied to every row.</param>
    /// <param name="realStartMinutes">Wall-clock anchor of t = 0 (default 08:15).</param>
    public TextWriterTraceSink(TextWriter writer, TraceLevel level = TraceLevel.State, double realStartMinutes = OpdSimulator.Core.Calendar.ClinicCalendar.DefaultWindowStartMinutes)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _level = level;
        _realStartMinutes = realStartMinutes;
    }

    /// <inheritdoc />
    public void Write(TraceEvent evt)
    {
        string? line = TraceFormatter.Format(evt, _level, _realStartMinutes);
        if (line is not null)
            _writer.WriteLine(line);
    }

    /// <inheritdoc />
    public void Flush() => _writer.Flush();
}