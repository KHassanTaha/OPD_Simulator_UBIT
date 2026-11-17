namespace OpdSimulator.App.Models;

using OpdSimulator.App.Services;
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
/// <param name="GeneratorDays">Number of operating SESSIONS, used by <see cref="RunMode.MultiDay"/>; closed weekdays between them are skipped and not counted (FR-SIM-12).</param>
/// <param name="StartDay">Weekday of day block 0 in a calendar run.</param>
/// <param name="DailyCap">Maximum admissions per operating session; null = unlimited.</param>
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
    /// <summary>
    /// p_bypass = the share of patients routed straight past Screening to a doctor
    /// (D-179). Null means "not configured", which the coordinator treats as off.
    /// </summary>
    /// <remarks>
    /// An init property for the same reason as <see cref="StageServiceFamilies"/>:
    /// a positional parameter would force defaults onto every parameter after it.
    /// The coordinator resolves the effective value in the same order as p_exit —
    /// an explicit override wins, then the value fitted from the file, then off.
    /// </remarks>
    public double? PBypassOverride { get; init; }

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
    /// The configured inter-arrival distribution, or null for the classic
    /// exponential (Poisson) process (Phase 8V, D-211).
    /// </summary>
    /// <remarks>
    /// The mean in the spec is a placeholder: the coordinator relocates the spec onto
    /// the λ it finally resolves with
    /// <see cref="DistributionSpecOperations.WithMean"/>, which preserves Ca². The
    /// shape, not the location, is what this record carries from the form — a bare
    /// family name could not tell the analytical widget what Ca² the run sampled.
    /// Null is load-bearing: it is what keeps the engine on its legacy sampler for
    /// the M/M/c path (D-054).
    /// </remarks>
    public DistributionSpec? ArrivalDistribution { get; init; }

    /// <summary>
    /// Which arrival-rate estimate drives the run when no manual λ overrides it
    /// (D-173).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared as an init property defaulting to <see cref="LambdaSource.Mle"/> so
    /// every construction site that predates Phase 8O keeps the behaviour it had.
    /// </para>
    /// <para>
    /// This carries the CHOICE, not the number. Putting the chosen λ into
    /// <paramref name="ManualArrivalRate"/> would have been the smaller edit, but it
    /// would have dressed a value the app FITTED from the user's data as one the
    /// user TYPED — and that record is read back to label the run's provenance. So
    /// the choice travels and the coordinator still decides the number, keeping
    /// fitted and manual distinguishable everywhere downstream.
    /// </para>
    /// </remarks>
    public LambdaSource LambdaSource { get; init; } = LambdaSource.Mle;

    /// <summary>
    /// The window λ for the window the user SELECTED on the Input tab, when one
    /// has been resolved there (D-172, D-173).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null whenever the Input tab was not involved: the CLI builds parameters
    /// directly, and a GUI run whose window selection has not been evaluated yet
    /// has nothing to offer. The coordinator then uses the binding's own
    /// <c>WindowLambda</c>, so both routes keep working — the same defence in
    /// depth D-128 established for the config panel's Start gate.
    /// </para>
    /// <para>
    /// This exists because <c>DataBindingResult.WindowLambda</c> is fixed at
    /// analysis time from the AUTO-detected window, and a user who picks "1
    /// week" on a six-day file has changed the divisor. Carrying the override
    /// rather than mutating the binding keeps the auto-detected figure available
    /// for the calculations dialog, which reports the observed window as read
    /// from the file.
    /// </para>
    /// </remarks>
    public double? WindowLambdaOverride { get; init; }

    /// <summary>
    /// The window <see cref="WindowLambdaOverride"/> was computed from, so a
    /// receipt can print the division that produces it (D-173).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Carries the SELECTED window, not the file's own: the override is
    /// <c>arrivals ÷ SelectedWindow.OperatingMinutes</c>, so without this the
    /// calculations dialog would have to print the file's observed window as the
    /// divisor of a number that was not computed from it. A reader dividing the
    /// printed arrivals by the printed minutes would get a different λ from the
    /// one printed above it.
    /// </para>
    /// <para>
    /// Null whenever <see cref="WindowLambdaOverride"/> is null, and null when
    /// the Input tab was not involved (CLI, unit tests). Equal to
    /// <c>DataBindingResult.ObservedWindow</c> whenever the user left the
    /// selection on Auto, which is the common case.
    /// </para>
    /// </remarks>
    public ObservationWindow? SelectedWindow { get; init; }

    /// <summary>
    /// The stage after which a patient may exit, for the given stage count and
    /// resolved exit probability, or −1 when exit routing is off (Phase 9P, D-243).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The resolved probability is an <b>argument</b> rather than a read of
    /// <see cref="PExitOverride"/> because the value the run uses is frequently
    /// fitted from the file (<c>DataBindingResult.FittedExitProbability</c>) and
    /// therefore unknown when the record is built. A property reading the override
    /// would say "no exit" for every fit-mode run that did not type one — the exact
    /// disagreement D-243 exists to remove. The stage count and the routing rule
    /// are the parts that live here; the number comes from the one resolver.
    /// </para>
    /// <para>
    /// Static as well as instance so the ρ preview can ask for the index from a
    /// stage count it holds, without building a whole record it would then discard.
    /// The instance form delegates here, so there is exactly one definition of the
    /// rule for the run and the preview.
    /// </para>
    /// <para>
    /// The rule is the pre-9P rule verbatim: with routing on, the exit stage is the
    /// one before the last, and a single-stage network yields −1 because it has no
    /// downstream stage.
    /// </para>
    /// </remarks>
    /// <param name="stageCount">Number of stages in the serial network.</param>
    /// <param name="exitProbability">The resolved p_exit the run will use.</param>
    /// <returns>Zero-based exit-stage index, or −1 when exit routing is off.</returns>
    public static int ExitStageIndexFor(int stageCount, double exitProbability) =>
        exitProbability > 0 ? stageCount - 2 : -1;

    /// <summary>
    /// The stage a bypass draw fires on — the stage being skipped — for the given
    /// stage count and resolved bypass probability (Phase 9P, D-243).
    /// </summary>
    /// <remarks>
    /// Three stages put S at 1 (skipping Screening, landing on Doctor); two stages
    /// put S at 0 (skipping the front door, so the draw fires at arrival). A
    /// single-stage network can neither skip nor land, so it is always −1. See
    /// <see cref="ExitStageIndexFor(int,double)"/> for why the probability is an
    /// argument and why the rule is static.
    /// </remarks>
    /// <param name="stageCount">Number of stages in the serial network.</param>
    /// <param name="bypassProbability">The resolved p_bypass the run will use.</param>
    /// <returns>Zero-based index of the skipped stage, or −1 when bypass is off.</returns>
    public static int BypassStageIndexFor(int stageCount, double bypassProbability) =>
        stageCount >= 2 && bypassProbability > 0 ? stageCount - 2 : -1;

    /// <summary>
    /// The stage a bypassed patient lands on for the given stage count and resolved
    /// bypass probability (Phase 9P, D-243).
    /// </summary>
    /// <remarks>The terminal stage, exactly as D-179 defined it for any chain length.</remarks>
    /// <param name="stageCount">Number of stages in the serial network.</param>
    /// <param name="bypassProbability">The resolved p_bypass the run will use.</param>
    /// <returns>Zero-based index of the destination stage, or −1 when bypass is off.</returns>
    public static int BypassDestinationIndexFor(int stageCount, double bypassProbability) =>
        stageCount >= 2 && bypassProbability > 0 ? stageCount - 1 : -1;

    /// <inheritdoc cref="ExitStageIndexFor(int,double)"/>
    /// <param name="exitProbability">The resolved p_exit the run will use.</param>
    public int ExitStageIndexFor(double exitProbability) =>
        ExitStageIndexFor(StageNames.Count, exitProbability);

    /// <inheritdoc cref="BypassStageIndexFor(int,double)"/>
    /// <param name="bypassProbability">The resolved p_bypass the run will use.</param>
    public int BypassStageIndexFor(double bypassProbability) =>
        BypassStageIndexFor(StageNames.Count, bypassProbability);

    /// <inheritdoc cref="BypassDestinationIndexFor(int,double)"/>
    /// <param name="bypassProbability">The resolved p_bypass the run will use.</param>
    public int BypassDestinationIndexFor(double bypassProbability) =>
        BypassDestinationIndexFor(StageNames.Count, bypassProbability);

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

/// <summary>
/// Which of the two arrival-rate estimates a run should use (D-173).
/// </summary>
/// <remarks>
/// The two estimates are not interchangeable and neither is a correction of the
/// other: MLE λ describes the rate while patients were arriving, window λ
/// describes the rate across the whole operating session. Both are always shown;
/// this picks one, and MLE is the default because it is what the course teaches.
/// </remarks>
public enum LambdaSource
{
    /// <summary>λ = 1 / mean(inter-arrival gaps). The default.</summary>
    Mle,

    /// <summary>λ = total arrivals ÷ the file's operating minutes.</summary>
    Window,
}
