namespace OpdSimulator.Core.Calendar;

/// <summary>
/// The OPD weekly schedule: which weekdays the clinic is open and the daily
/// arrival window.
/// </summary>
/// <remarks>
/// <para>
/// Time model (CONTEXT §5.1): the simulation clock <c>t = 0</c> is anchored at
/// the start of the arrival window on the first day. A <b>day block</b> is the
/// 24 hours from that anchor to the next day's anchor, so a block for day index
/// <c>d</c> occupies <c>[d·1440, (d+1)·1440)</c> and its weekday is
/// <c>(<see cref="StartDayOfWeek"/> + d) mod 7</c>. The arrival window occupies
/// the first <see cref="OpenDurationMinutes"/> minutes of every open day's
/// block — that is, <c>t mod 1440 &lt; openDuration</c> on an open weekday —
/// which makes the whole timeline non-negative and lets the engine gate
/// arrivals purely by checking the clock at each arrival event.
/// </para>
/// <para>
/// Defaults come from CONTEXT §1.1: open Monday–Thursday and Saturday, closed
/// Friday and Sunday, window 08:15–11:00 (495→660 minutes past midnight). The
/// clinic model treats arrivals after the window (or on a closed day) as
/// gated demand: the Poisson arrival stream continues, but those patients are
/// not admitted. Services already in progress keep running to completion
/// (FR-SIM-6, CONTEXT §5.2), which is why each day's trailing <see cref="OpenDurationMinutes"/>
/// plus the drain can extend the operating time past the window.
/// </para>
/// </remarks>
public sealed class ClinicCalendar
{
    /// <summary>Number of minutes in a 24-hour day.</summary>
    public const double MinutesPerDay = 1440;

    /// <summary>Default clinic opening hour relative to midnight: 08:15.</summary>
    public const int DefaultWindowStartMinutes = 8 * 60 + 15;

    /// <summary>Default clinic closing hour relative to midnight: 11:00.</summary>
    public const int DefaultWindowEndMinutes = 11 * 60;

    /// <summary>Default open weekdays (CONTEXT §1.1).</summary>
    public static readonly IReadOnlySet<DayOfWeek> DefaultOpenDays = new HashSet<DayOfWeek>
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Saturday,
    };

    /// <summary>
    /// Creates a clinic schedule.
    /// </summary>
    /// <param name="openDays">Weekdays the clinic is open. Defaults to
    /// <see cref="DefaultOpenDays"/>.</param>
    /// <param name="startDayOfWeek">Weekday of simulation day 0 (the day whose
    /// block starts at t = 0). Defaults to Monday.</param>
    /// <param name="windowStartMinutes">Arrival-window start, minutes past
    /// midnight on each open day. Defaults to 08:15.</param>
    /// <param name="windowEndMinutes">Arrival-window end, minutes past midnight
    /// on each open day. Defaults to 11:00.</param>
    /// <exception cref="ArgumentException">If <paramref name="openDays"/> is
    /// null/empty, or if the window is not strictly increasing.</exception>
    public ClinicCalendar(
        IEnumerable<DayOfWeek>? openDays = null,
        DayOfWeek startDayOfWeek = DayOfWeek.Monday,
        double windowStartMinutes = DefaultWindowStartMinutes,
        double windowEndMinutes = DefaultWindowEndMinutes)
    {
        openDays ??= DefaultOpenDays;
        if (!openDays.Any())
            throw new ArgumentException("The clinic must be open on at least one weekday.", nameof(openDays));
        if (windowStartMinutes >= windowEndMinutes)
            throw new ArgumentException("The arrival window must be non-empty (start < end).");
        if (windowStartMinutes < 0 || windowEndMinutes > MinutesPerDay)
            throw new ArgumentException("The arrival window must lie within a 24-hour day.");

        OpenDays = new HashSet<DayOfWeek>(openDays);
        StartDayOfWeek = startDayOfWeek;
        WindowStartMinutes = windowStartMinutes;
        WindowEndMinutes = windowEndMinutes;
    }

    /// <summary>Weekdays the clinic is open.</summary>
    public IReadOnlySet<DayOfWeek> OpenDays { get; }

    /// <summary>Weekday of simulation day 0 (the block containing t = 0).</summary>
    public DayOfWeek StartDayOfWeek { get; }

    /// <summary>Arrival-window start, minutes past midnight on an open day.</summary>
    public double WindowStartMinutes { get; }

    /// <summary>Arrival-window end, minutes past midnight on an open day.</summary>
    public double WindowEndMinutes { get; }

    /// <summary>Length of the daily arrival window in minutes.</summary>
    public double OpenDurationMinutes => WindowEndMinutes - WindowStartMinutes;

    /// <summary>
    /// Returns the weekday of a given day block index, relative to
    /// <see cref="StartDayOfWeek"/>.
    /// </summary>
    /// <param name="dayIndex">Zero-based day block index.</param>
    /// <returns>The weekday that block falls on.</returns>
    public DayOfWeek DayOfWeekAt(int dayIndex)
    {
        if (dayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(dayIndex));
        return (DayOfWeek)(((int)StartDayOfWeek + dayIndex) % 7);
    }

    /// <summary>
    /// Reports whether the given weekday is a clinic day.
    /// </summary>
    /// <param name="day">The weekday to test.</param>
    /// <returns>True if the clinic is open that weekday.</returns>
    public bool IsOpenDay(DayOfWeek day) => OpenDays.Contains(day);

    /// <summary>
    /// Reports whether an arrival at absolute clock time <paramref name="t"/>
    /// falls inside a clinic arrival window.
    /// </summary>
    /// <param name="t">Simulation clock minutes from t = 0 (day-0 window start).</param>
    /// <returns>True only on an open weekday AND inside that day's arrival window
    /// (window end exclusive: 11:00:00 is closed).</returns>
    public bool IsInArrivalWindow(double t)
    {
        if (t < 0)
            return false;

        int dayIndex = (int)(t / MinutesPerDay);
        if (!IsOpenDay(DayOfWeekAt(dayIndex)))
            return false;

        double blockMinute = t - dayIndex * MinutesPerDay;
        return blockMinute >= 0 && blockMinute < OpenDurationMinutes;
    }

    /// <summary>
    /// Formats a simulation clock time as the wall clock for display, using
    /// <see cref="WindowStartMinutes"/> as the anchor of day blocks.
    /// </summary>
    /// <param name="t">Simulation clock minutes from t = 0.</param>
    /// <returns>A clock string "HH:mm", e.g. "10:42".</returns>
    public string FormatClock(double t)
    {
        if (t < 0)
            t = 0;
        double wall = WindowStartMinutes + (t % MinutesPerDay);
        if (wall >= MinutesPerDay)
            wall -= MinutesPerDay;
        int hours = (int)(wall / 60);
        int minutes = (int)(wall % 60);
        return $"{hours:00}:{minutes:00}";
    }
}