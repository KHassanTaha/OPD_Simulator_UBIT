namespace OpdSimulator.Data.Preprocess;

using System.Globalization;

/// <summary>
/// Parses wall-clock time values into minutes since midnight.
/// </summary>
/// <remarks>
/// Accepted formats (documented for the viva):
/// <list type="bullet">
/// <item><c>HH:mm</c> — 24-hour (e.g. "8:15", "08:15", "08:15:30").</item>
/// <item><c>h:mm AM/PM</c> — 12-hour with meridiem, case-insensitive (e.g. "8:15 AM").</item>
/// <item>Bare number &lt; 1 — Excel time stored as a fraction of a day (0.34375 = 08:15).</item>
/// <item>Bare number ≥ 1 — already minutes since midnight (495 = 08:15).</item>
/// </list>
/// Implemented via <see cref="DateTime.ParseExact"/> (not TimeSpan) because the
/// .NET TimeSpan custom format has no meridiem specifier and no uppercase hour —
/// DateTime formats cover both. All parsing uses invariant culture so any machine
/// reads "8:15" the same way.
/// </remarks>
public static class TimeParser
{
    private static readonly string[] Formats =
    {
        "H:mm",
        "HH:mm",
        "H:mm:ss",
        "HH:mm:ss",
        "h:mm tt",
        "h:mm:ss tt",
    };

    /// <summary>
    /// Attempts to parse a time value into minutes since midnight.
    /// </summary>
    /// <param name="text">Raw cell text.</param>
    /// <param name="minutes">On success, minutes since midnight (double).</param>
    /// <returns><see langword="true"/> when the value was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string text, out double minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string t = text.Trim();

        // 12-hour "h:mm AM/PM" — normalise so "am"/"pm" (lowercase or squashed) also parse.
        if (t.Length >= 6
            && (t.EndsWith("AM", StringComparison.OrdinalIgnoreCase)
                || t.EndsWith("PM", StringComparison.OrdinalIgnoreCase)))
        {
            string prefix = t[..^2].TrimEnd();
            t = prefix + " " + t[^2..].ToUpperInvariant();
        }

        if (DateTime.TryParseExact(t, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            minutes = dt.TimeOfDay.TotalMinutes;
            return true;
        }

        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            // < 1 ⇒ Excel day-fraction (0.34375 → 08:15); otherwise minutes since midnight.
            minutes = number < 1.0 ? number * 1440.0 : number;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to parse a calendar date from a <c>session_date</c> cell (D-175).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Accepted inputs, in the order they are tried:
    /// <list type="bullet">
    /// <item><c>YYYY-MM-DD</c> — the ISO form the sample files and the UI emit.</item>
    /// <item><c>YYYY-MM-DD HH:mm:ss</c> — the string form <c>ExcelLoader</c> produces
    /// for a genuinely date-typed cell (it formats with <c>ToString("yyyy-MM-dd HH:mm:ss")</c>).
    /// Without this branch the same multi-day data would validate from a .csv and
    /// fail from an .xlsx, which is the kind of asymmetry that reads as a bug later.</item>
    /// <item><c>YYYY-MM-DDTHH:mm:ss</c> — ISO-8601 date-time, same truncation.</item>
    /// <item><c>DD/MM/YYYY</c> — a fallback for hand-entered files.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The time portion of a date-time input is <b>discarded</b>, not validated: a
    /// <c>session_date</c> names the operating session a row belongs to, and the
    /// row's own <c>arrival_time</c> carries the wall clock. Truncation happens at
    /// the first space or <c>'T'</c> so a single rule covers both date-time forms.
    /// </para>
    /// <para>
    /// This method is additive: <see cref="TryParse"/> is untouched, so every file
    /// that loaded before D-175 still loads exactly the same way.
    /// </para>
    /// </remarks>
    /// <param name="text">Raw cell text.</param>
    /// <param name="date">On success, the parsed calendar date.</param>
    /// <returns><see langword="true"/> when the value was parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryParseDate(string text, out DateOnly date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string t = text.Trim();

        // Excel/ISO-8601 date-times carry a time we do not need. Cut at the first
        // space or 'T' so "2026-09-15 08:15:00" and "2026-09-15T08:15:00" both
        // reduce to the plain ISO date.
        int cut = t.IndexOfAny(new[] { ' ', 'T' });
        if (cut > 0)
            t = t[..cut];

        if (DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        return DateOnly.TryParseExact(t, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}