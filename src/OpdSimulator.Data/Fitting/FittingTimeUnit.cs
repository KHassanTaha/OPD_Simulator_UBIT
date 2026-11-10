namespace OpdSimulator.Data.Fitting;

/// <summary>
/// The unit a distribution fit is expressed in (Phase 8W).
/// </summary>
/// <remarks>
/// This is a SCALE choice, not a different sample. Every calculator in this
/// project produces minutes, and a fit is scale-equivariant: dividing the
/// samples by 60 changes the fitted parameters and the chart axes, but it does
/// not change the maximum-likelihood shape, the χ² statistic or the verdict.
/// The unit exists so a user can read λ per hour, or a service time per second,
/// without converting by hand.
/// </remarks>
public enum FittingTimeUnit
{
    /// <summary>Minutes — the project's native unit and the default.</summary>
    Minutes,

    /// <summary>Seconds (1 second = 1/60 minute).</summary>
    Seconds,

    /// <summary>Hours (1 hour = 60 minutes).</summary>
    Hours,

    /// <summary>Calendar days (1 day = 1440 minutes), for arrival-rate reporting.</summary>
    Days,

    /// <summary>Operating sessions (1 session = 165 operating minutes).</summary>
    Sessions,
}

/// <summary>
/// Converts fitted quantities between minutes and a <see cref="FittingTimeUnit"/>,
/// and owns every display label for the unit so no view hard-codes "minutes"
/// (Phase 8W).
/// </summary>
public static class FittingTimeUnits
{
    /// <summary>Operating minutes in one clinic session (08:15–11:00).</summary>
    public const double SessionMinutes = 165.0;

    /// <summary>Minutes in one calendar day.</summary>
    public const double DayMinutes = 1440.0;

    /// <summary>Dropdown entries, in display order. Minutes is first and default.</summary>
    public static IReadOnlyList<string> Options { get; } =
        new[] { "minutes", "seconds", "hours", "days", "sessions" };

    /// <summary>Minutes represented by one unit.</summary>
    /// <param name="unit">The unit to convert from.</param>
    /// <returns>1, 1/60, 60, 1440 or 165.</returns>
    /// <exception cref="ArgumentOutOfRangeException">For an undefined enum value.</exception>
    public static double MinutesPerUnit(FittingTimeUnit unit) => unit switch
    {
        FittingTimeUnit.Minutes => 1.0,
        FittingTimeUnit.Seconds => 1.0 / 60.0,
        FittingTimeUnit.Hours => 60.0,
        FittingTimeUnit.Days => DayMinutes,
        FittingTimeUnit.Sessions => SessionMinutes,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown fitting time unit."),
    };

    /// <summary>Converts a duration in minutes to the chosen unit.</summary>
    /// <param name="minutes">Duration in minutes.</param>
    /// <param name="unit">Target unit.</param>
    /// <returns>The duration expressed in <paramref name="unit"/>.</returns>
    public static double FromMinutes(double minutes, FittingTimeUnit unit)
        => minutes / MinutesPerUnit(unit);

    /// <summary>Converts a duration in the chosen unit back to minutes.</summary>
    /// <param name="value">Duration in <paramref name="unit"/>.</param>
    /// <param name="unit">Source unit.</param>
    /// <returns>The duration in minutes.</returns>
    public static double ToMinutes(double value, FittingTimeUnit unit)
        => value * MinutesPerUnit(unit);

    /// <summary>Converts a per-minute rate to a rate per <paramref name="unit"/>.</summary>
    /// <param name="ratePerMinute">Rate in events per minute.</param>
    /// <param name="unit">Target unit.</param>
    /// <returns>The rate per <paramref name="unit"/> (e.g. 0.2/min → 12/hour).</returns>
    public static double RateFromPerMinute(double ratePerMinute, FittingTimeUnit unit)
        => ratePerMinute * MinutesPerUnit(unit);

    /// <summary>Scales a minute sample onto the chosen unit for fitting and charting.</summary>
    /// <param name="minutes">Samples in minutes.</param>
    /// <param name="unit">Target unit.</param>
    /// <returns>The same samples in <paramref name="unit"/>; the input instance when it is already minutes.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="minutes"/> is null.</exception>
    public static IReadOnlyList<double> Scale(IReadOnlyList<double> minutes, FittingTimeUnit unit)
    {
        ArgumentNullException.ThrowIfNull(minutes);
        if (unit == FittingTimeUnit.Minutes)
        {
            return minutes;
        }

        double divisor = MinutesPerUnit(unit);
        var scaled = new double[minutes.Count];
        for (int i = 0; i < minutes.Count; i++)
        {
            scaled[i] = minutes[i] / divisor;
        }

        return scaled;
    }

    /// <summary>The plural unit name used in chart titles ("minutes", "hours", …).</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>A lower-case plural label.</returns>
    public static string Label(FittingTimeUnit unit) => unit switch
    {
        FittingTimeUnit.Minutes => "minutes",
        FittingTimeUnit.Seconds => "seconds",
        FittingTimeUnit.Hours => "hours",
        FittingTimeUnit.Days => "days",
        FittingTimeUnit.Sessions => "sessions",
        _ => "minutes",
    };

    /// <summary>The rate label shown beside a fitted λ ("patients/min", "patients/hour", …).</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>A compact rate label.</returns>
    public static string RateLabel(FittingTimeUnit unit) => unit switch
    {
        FittingTimeUnit.Minutes => "patients/min",
        FittingTimeUnit.Seconds => "patients/s",
        FittingTimeUnit.Hours => "patients/hour",
        FittingTimeUnit.Days => "patients/day (24 h)",
        FittingTimeUnit.Sessions => "patients/session (165 min)",
        _ => "patients/min",
    };

    /// <summary>The denominator suffix for "X per unit" ("min", "s", "hour", "day", "session").</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>A compact suffix for rate strings.</returns>
    public static string RateUnit(FittingTimeUnit unit) => unit switch
    {
        FittingTimeUnit.Minutes => "min",
        FittingTimeUnit.Seconds => "s",
        FittingTimeUnit.Hours => "hour",
        FittingTimeUnit.Days => "day",
        FittingTimeUnit.Sessions => "session",
        _ => "min",
    };

    /// <summary>
    /// The definition a days/sessions label needs, or an empty string for the
    /// units whose meaning is unambiguous.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>" (1 day = 24 h)" / " (1 session = 165 operating minutes)" / "".</returns>
    public static string DefinitionNote(FittingTimeUnit unit) => unit switch
    {
        FittingTimeUnit.Days => " (1 day = 24 h)",
        FittingTimeUnit.Sessions => " (1 session = 165 operating minutes)",
        _ => "",
    };

    /// <summary>Parses a CLI/UI token or label (case-insensitive).</summary>
    /// <param name="text">"minutes", "seconds", "hours", "days" or "sessions".</param>
    /// <param name="unit">The parsed unit; minutes when parsing fails.</param>
    /// <returns>True when the token was recognised.</returns>
    public static bool TryParse(string? text, out FittingTimeUnit unit)
    {
        switch (text?.Trim().ToLowerInvariant())
        {
            case "minutes":
            case "minute":
            case "min":
                unit = FittingTimeUnit.Minutes;
                return true;
            case "seconds":
            case "second":
            case "s":
                unit = FittingTimeUnit.Seconds;
                return true;
            case "hours":
            case "hour":
            case "h":
                unit = FittingTimeUnit.Hours;
                return true;
            case "days":
            case "day":
                unit = FittingTimeUnit.Days;
                return true;
            case "sessions":
            case "session":
                unit = FittingTimeUnit.Sessions;
                return true;
            default:
                unit = FittingTimeUnit.Minutes;
                return false;
        }
    }

    /// <summary>Parses a dropdown label, defaulting to minutes for an unknown value.</summary>
    /// <param name="label">A label from <see cref="Options"/>.</param>
    /// <returns>The matching unit, or <see cref="FittingTimeUnit.Minutes"/>.</returns>
    public static FittingTimeUnit ParseLabel(string? label)
        => TryParse(label, out var unit) ? unit : FittingTimeUnit.Minutes;
}
