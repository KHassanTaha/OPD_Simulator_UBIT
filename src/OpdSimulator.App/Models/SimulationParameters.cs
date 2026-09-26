namespace OpdSimulator.App.Models;

using OpdSimulator.Core.Distributions;
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
/// <param name="ManualArrivalRate">Manual λ override (patients per minute, mode-converted); null = fitted from data.</param>
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
/// <param name="TraceLevelName">Human-readable trace level: "Minimal", "Standard", "Detailed" or "Debug".</param>
public sealed record SimulationParameters(
    ParameterMode Mode,
    string InterArrivalDistribution,
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
    string TraceLevelName)
{
    /// <summary>
    /// One service family per stage, in <see cref="StageNames"/> order (Phase 8J).
    /// Replaces the single stored service-family string: a stage's family was always
    /// already per-stage in the UI (<c>StageRow.ServiceFamily</c>), but the record
    /// collapsed them to the first stage's, deferring per-stage override (D-126). The
    /// coordinator builds each <c>StageSpec</c> from the matching entry.
    /// </summary>
    /// <remarks>
    /// Declared as an init property rather than a positional parameter so it can carry a
    /// real default. A positional record parameter cannot default unless every parameter
    /// after it does too, which would mean giving all twelve existing parameters a
    /// default — a much wider change to this record than the phase calls for. The empty
    /// default also makes the unset case explicit: a record built without it is
    /// incomplete, and the coordinator refuses it loudly rather than guessing a family
    /// for a network it was never told about.
    /// </remarks>
    public IReadOnlyList<DistributionSpec> ServiceFamilies { get; init; } =
        Array.Empty<DistributionSpec>();

    /// <summary>
    /// μ per server per stage exactly as the user entered it, mode-converted, parallel to
    /// <see cref="ServiceFamilies"/> and built from the same source (Phase 8J). Null where
    /// the user entered nothing and the rate must be fitted from data.
    /// </summary>
    /// <remarks>
    /// This is a separate list from <paramref name="ManualServiceRates"/> on purpose: the two
    /// answer different questions. <paramref name="ManualServiceRates"/> is the
    /// mode-precedence input (D-128) that decides *whether* a manual value applies; this one
    /// is the rate the stage is actually configured with, and it is the value handed to
    /// <c>StageSpec.ServiceRate</c> unchanged. Keeping them apart means the coordinator
    /// never has to recover a rate by inverting a spec's mean, which is not bit-reversible
    /// (D-146 caveat 1).
    /// </remarks>
    public IReadOnlyList<double?> ServiceRates { get; init; } =
        Array.Empty<double?>();

    /// <summary>
    /// The first stage's service family as a display name, for callers that still need one
    /// family for the whole run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived, not stored. Before Phase 8J this was a constructor field holding
    /// <c>StageRows[0].ServiceFamily</c>, and <c>MainViewModel</c> reads it to label every
    /// stage with the same family. Returning the first entry preserves that behaviour
    /// exactly — the enum member names are the same five strings the UI dropdowns use, so
    /// the label text is unchanged. It is no longer an input to anything.
    /// </para>
    /// <para>
    /// It is a transitional shim, not the model's shape. It lies the moment stages
    /// disagree, which is precisely what per-stage families exist to express; Phase 8K
    /// replaces these label sites with <see cref="ServiceFamilies"/> read per stage. The UI
    /// does not yet let a user set stages independently, so no reachable configuration is
    /// mislabelled today.
    /// </para>
    /// </remarks>
    public string ServiceDistribution =>
        ServiceFamilies.Count > 0 ? ServiceFamilies[0].Family.ToString() : "Exponential";
}
