namespace OpdSimulator.App.Models;

using OpdSimulator.Data.Parameters;

/// <summary>How the time-horizon field is interpreted.</summary>
public enum HorizonMode
{
    /// <summary>The value is the arrival-generation window in minutes.</summary>
    Minutes,

    /// <summary>The value is the number of clinic calendar-day blocks.</summary>
    Days,
}

/// <summary>
/// The flat, validated inputs a run needs. Produced by the config view model
/// from the raw field values and consumed by the simulation coordinator, so
/// both stay free of UI types and the same value round-trips through presets.
/// </summary>
/// <param name="Mode">Rate-wise or mean-wise interpretation of the parameter fields.</param>
/// <param name="InterArrivalDistribution">Fitting family for inter-arrival times (e.g. "Exponential").</param>
/// <param name="ServiceDistribution">Fitting family for service times.</param>
/// <param name="ArrivalRate">External arrival rate λ₀ (patients per minute), after mode conversion.</param>
/// <param name="StageNames">Stage names in clinic-flow order (Reception → Screening → Doctor).</param>
/// <param name="ServerCounts">Parallel servers per stage, one per <paramref name="StageNames"/> entry.</param>
/// <param name="ServiceRates">Service rate μ per server per stage, one per <paramref name="StageNames"/> entry.</param>
/// <param name="HorizonMode">Whether <paramref name="HorizonMinutes"/> is a window or calendar days.</param>
/// <param name="HorizonMinutes">Arrival window minutes (used when <paramref name="HorizonMode"/> is <see cref="HorizonMode.Minutes"/>).</param>
/// <param name="GeneratorDays">Number of calendar-day blocks (used when <paramref name="HorizonMode"/> is <see cref="HorizonMode.Days"/>).</param>
/// <param name="StartDay">Weekday of day block 0 in a calendar run.</param>
/// <param name="DailyCap">Maximum admissions per day block; null = unlimited.</param>
/// <param name="Seed">Random seed for reproducibility (FR-VAL-3).</param>
/// <param name="PExitOverride">Manual exit probability after Screening; null = fitted from data (or 0 with no data).</param>
/// <param name="TraceLevelName">Human-readable trace level: "None", "Events", "State" or "Rng".</param>
public sealed record SimulationParameters(
    ParameterMode Mode,
    string InterArrivalDistribution,
    string ServiceDistribution,
    double ArrivalRate,
    IReadOnlyList<string> StageNames,
    IReadOnlyList<int> ServerCounts,
    IReadOnlyList<double> ServiceRates,
    HorizonMode HorizonMode,
    double HorizonMinutes,
    int GeneratorDays,
    DayOfWeek StartDay,
    int? DailyCap,
    int Seed,
    double? PExitOverride,
    string TraceLevelName);