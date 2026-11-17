using OpdSimulator.Data.Fitting;

namespace OpdSimulator.App.Models;

/// <summary>
/// The unit in which the user enters rates (λ, μ) or means
/// (1/λ, 1/μ) and in which the fitted distributions are reported. The
/// engine internally works in minutes; values are converted at
/// parameter-build time.
/// </summary>
/// <remarks>
/// One axis, not two (Phase 9P, D-240): this enum is the only unit state in
/// the app. Phase 8W's separate <c>FittingTimeUnit</c> selector was deleted
/// because it could disagree with this one, and because it changed nothing
/// about the run. <see cref="FittingTimeUnit"/> survives as the Data-layer
/// type the fit reports are written in.
/// </remarks>
public enum TimeUnit
{
    Minutes,
    Seconds,
    Hours,
    Days,
    Sessions,
}

/// <summary>
/// The minute mappings and display labels for <see cref="TimeUnit"/>.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is deliberately NOT re-derived here. It is read from
/// <see cref="FittingTimeUnits.MinutesPerUnit"/>, which already owns
/// "1 day = 1440 minutes" and "1 session = 165 operating minutes", so the two
/// enums cannot drift apart (Phase 9P, D-240).
/// </para>
/// <para>
/// Everything that converts a value lives in this type and in
/// <c>ConfigPanelViewModel.ToPerMinute</c>/<c>ToMinutes</c>. No other code may
/// divide or multiply by a unit constant.
/// </para>
/// </remarks>
public static class TimeUnits
{
    /// <summary>Minutes represented by one unit (1, 1/60, 60, 1440 or 165).</summary>
    /// <param name="unit">The unit to convert from.</param>
    /// <returns>The minutes in one <paramref name="unit"/>.</returns>
    public static double MinutesPerUnit(TimeUnit unit) =>
        FittingTimeUnits.MinutesPerUnit(ToFitting(unit));

    /// <summary>The Data-layer unit with the same meaning, for fit reporting.</summary>
    /// <param name="unit">The app's unit.</param>
    /// <returns>The matching <see cref="FittingTimeUnit"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">For an undefined enum value.</exception>
    public static FittingTimeUnit ToFitting(TimeUnit unit) => unit switch
    {
        TimeUnit.Minutes => FittingTimeUnit.Minutes,
        TimeUnit.Seconds => FittingTimeUnit.Seconds,
        TimeUnit.Hours => FittingTimeUnit.Hours,
        TimeUnit.Days => FittingTimeUnit.Days,
        TimeUnit.Sessions => FittingTimeUnit.Sessions,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown time unit."),
    };

    /// <summary>The plural unit name used in captions ("minutes", "hours", …).</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>A lower-case plural label.</returns>
    public static string Label(TimeUnit unit) => FittingTimeUnits.Label(ToFitting(unit));

    /// <summary>
    /// The compact denominator for a rate ("min", "s", "hour", "day", "session").
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <returns>The denominator suffix.</returns>
    public static string RateUnit(TimeUnit unit) => FittingTimeUnits.RateUnit(ToFitting(unit));
}
