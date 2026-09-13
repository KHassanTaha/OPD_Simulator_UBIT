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
}