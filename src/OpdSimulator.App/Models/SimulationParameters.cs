namespace OpdSimulator.App.Models;

using OpdSimulator.Data.Parameters;

/// <summary>
/// The validated inputs a run needs, resolved from the raw config fields by the
/// config view model and consumed by the simulation coordinator — a flat data
/// seam that keeps the GUI and the coordinator free of UI types (the M5
/// pattern, re-introduced by D-104). Manual values are already mode-converted
/// (rate-wise vs mean-wise); blanks are null and fall back to the fitted values
/// inside the coordinator.
/// </summary>
/// <param name="Mode">Rate-wise or mean-wise interpretation of the parameter fields.</param>
/// <param name="InterArrivalDistribution">Fitting family for inter-arrival times (e.g. "Exponential").</param>
/// <param name="ServiceDistribution">Fitting family for service times.</param>
/// <param name="ManualArrivalRate">Manual λ₀ override (patients per minute, mode-converted); null = fitted from data.</param>
/// <param name="StageNames">Stage names in flow order (Reception → Screening → Doctor by default).</param>
/// <param name="ServerCounts">Parallel servers per stage, one per <paramref name="StageNames"/> entry.</param>
/// <param name="ManualServiceRates">Manual μ per server per stage (mode-converted); null entry = fitted from data.</param>
/// <param name="RunMode">ClinicDay, MultiDay or DiagnosticTrace (D-105).</param>
/// <param name="HorizonMinutes">Arrival-window minutes, used by <see cref="RunMode.DiagnosticTrace"/>.</param>
/// <param name="GeneratorDays">Number of calendar-day blocks, used by <see cref="RunMode.MultiDay"/>.</param>
/// <param name="StartDay">Weekday of day block 0 in a calendar run.</param>
/// <param name="DailyCap">Maximum admissions per day block; null = unlimited.</param>
/// <param name="Seed">Random seed for reproducibility (FR-VAL-3).</param>
/// <param name="PExitOverride">Manual exit probability after Screening; null = fitted (or default 0.4 with no data).</param>
/// <param name="TraceLevelName">Human-readable trace level: "None", "Events", "State" or "Rng".</param>
public sealed record SimulationParameters(
    ParameterMode Mode,
    string InterArrivalDistribution,
    string ServiceDistribution,
    double? ManualArrivalRate,
    IReadOnlyList<string> StageNames,
    IReadOnlyList<int> ServerCounts,
    IReadOnlyList<double?> ManualServiceRates,
    RunMode RunMode,
    double HorizonMinutes,
    int GeneratorDays,
    DayOfWeek StartDay,
    int? DailyCap,
    int Seed,
    double? PExitOverride,
    string TraceLevelName);