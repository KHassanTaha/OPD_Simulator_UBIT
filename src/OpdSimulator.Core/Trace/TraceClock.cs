namespace OpdSimulator.Core.Trace;

using OpdSimulator.Core.Calendar;

/// <summary>
/// Renders a simulation clock time as a wall clock with seconds (<c>HH:mm:ss</c>).
/// </summary>
/// <remarks>
/// Follows the day model of <see cref="ClinicCalendar.FormatClock"/>: t = 0 is
/// anchored at the arrival-window start (08:15 by default), and wall time wraps
/// at 24 hours. Fractional minutes are truncated to whole seconds so the seconds
/// column is always a deterministic floor — this keeps trace files byte-stable
/// across machines (a value of 1.345 minutes reads 08:16:20, not 08:16:21).
/// </remarks>
public static class TraceClock
{
    /// <summary>
    /// Formats a simulation time as <c>HH:mm:ss</c> past midnight.
    /// </summary>
    /// <param name="t">Simulation clock minutes from t = 0 (the day-0 window start).</param>
    /// <param name="realStartMinutes">Wall-clock anchor of t = 0, minutes past midnight (default 08:15).</param>
    /// <returns>A clock string such as <c>"10:42:07"</c>.</returns>
    public static string Format(double t, double realStartMinutes = ClinicCalendar.DefaultWindowStartMinutes)
    {
        if (t < 0)
            t = 0;

        double wall = realStartMinutes + (t % ClinicCalendar.MinutesPerDay);
        if (wall >= ClinicCalendar.MinutesPerDay)
            wall -= ClinicCalendar.MinutesPerDay;

        double wholeMinutes = Math.Floor(wall);
        int hours = (int)(wholeMinutes / 60);
        int minutes = (int)(wholeMinutes % 60);
        int seconds = (int)((wall - wholeMinutes) * 60); // truncate, never round (see remarks)

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}