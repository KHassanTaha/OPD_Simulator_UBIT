namespace OpdSimulator.Core.Trace;

using System.Globalization;
using OpdSimulator.Core.Calendar;

/// <summary>
/// Renders a <see cref="TraceEvent"/> as one fixed-column line of text.
/// </summary>
/// <remarks>
/// <para>
/// Row layout (detailed level, the default):
/// <c>T=&lt;sim time&gt;  &lt;wall clock&gt;  &lt;TYPE&gt;  &lt;P#&gt;  &lt;location&gt;  q=&lt;queue&gt;</c>.
/// The type column is left-justified to 12 characters and the patient id to 6,
/// so the story reads as a table. The <c>q=</c> column follows the semantics
/// documented on <see cref="TraceEvent"/>; a null queue renders as <c>q=-</c>.
/// </para>
/// <para>
/// Level filtering is here, not in the engine, so the event stream and the
/// simulation arithmetic never depend on the display level:
/// <list type="bullet">
///   <item>below <see cref="TraceLevel.Detailed"/> the location suffix (server id,
///   routing/exit destination) is dropped;</item>
///   <item>below <see cref="TraceLevel.Debug"/> the RNG draw rows are dropped entirely.</item>
/// </list>
/// Numbers are formatted with the invariant culture so a trace file is
/// byte-identical on any machine/locale (required for the golden-file test).
/// </para>
/// </remarks>
public static class TraceFormatter
{
    private static readonly IReadOnlyDictionary<TraceEventType, string> TypeNames =
        new Dictionary<TraceEventType, string>
        {
            { TraceEventType.Arrival, "ARRIVAL" },
            { TraceEventType.StartService, "START_SVC" },
            { TraceEventType.EndService, "END_SVC" },
            { TraceEventType.Route, "ROUTE" },
            { TraceEventType.Exit, "EXIT" },
            { TraceEventType.Rng, "RNG" },
        };

    /// <summary>
    /// Formats one event as a line, or returns <see langword="null"/> when the
    /// row is suppressed at the given level.
    /// </summary>
    /// <param name="evt">The event to render.</param>
    /// <param name="level">How much detail to include (see remarks).</param>
    /// <param name="realStartMinutes">Wall-clock anchor of t = 0 (default 08:15).</param>
    /// <returns>A single text line, or null if the row should not be shown.</returns>
    public static string? Format(TraceEvent evt, TraceLevel level, double realStartMinutes = ClinicCalendar.DefaultWindowStartMinutes)
    {
        if (evt is null)
            throw new ArgumentNullException(nameof(evt));

        if (evt.Type == TraceEventType.Rng && level < TraceLevel.Debug)
            return null;

        string t = evt.Time.ToString("0.000", CultureInfo.InvariantCulture);
        string wall = TraceClock.Format(evt.Time, realStartMinutes);
        string type = TypeNames[evt.Type];

        if (evt.Type == TraceEventType.Rng)
            return $"T={t}  {wall}  {type,-12}  {evt.Details ?? string.Empty}";

        string patient = evt.PatientId is { } id ? "P" + id.ToString(CultureInfo.InvariantCulture) : "-";

        string location = evt.StageName ?? "-";
        if (level >= TraceLevel.Detailed)
        {
            if (evt.ServerId is { } serverId)
                location += " s" + serverId.ToString(CultureInfo.InvariantCulture);
            if (evt.Details is { Length: > 0 } details)
                location += " " + details;
        }

        string q = evt.QueueLength is { } queueLength
            ? queueLength.ToString(CultureInfo.InvariantCulture)
            : "-";

        return $"T={t}  {wall}  {type,-12}  {patient,-6}  {location}  q={q}";
    }
}