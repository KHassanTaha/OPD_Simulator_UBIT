using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;
using Serilog;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Drives the configuration panel (Phase 4): data upload state, distribution
/// and parameter-mode choices, per-stage parameters, horizon and advanced
/// options, plus the pinned Start / Clear-All actions. Validation is
/// blur-based and deterministic (AGENTS §16.9); the p_exit boundary [0,1)
/// mirrors the Core contract exactly. No Core engine call yet — Phase 5 wires
/// the run.
/// </summary>
public partial class ConfigPanelViewModel : ObservableObject
{
    /// <summary>Default stage names used to seed fresh rows (spec §4).</summary>
    private static readonly string[] DefaultStageNames = { "Reception", "Screening", "Doctor" };

    /// <summary>Distribution options offered by both model dropdowns.</summary>
    public IReadOnlyList<string> Distributions { get; } =
        new[] { "Exponential", "Poisson", "Normal", "Uniform" };

    /// <summary>
    /// The six families offered by the per-stage and default-family dropdowns
    /// (Phase 8K, D-150). Enum-typed so the row and the defaults cannot drift apart
    /// the way two string lists can.
    /// </summary>
    public IReadOnlyList<DistributionFamily> StageFamilyOptions { get; } =
        Enum.GetValues<DistributionFamily>();

    /// <summary>
    /// Service family stamped onto every NEW stage row (Phase 8K, D-150, task 8K.4).
    /// </summary>
    /// <remarks>
    /// A default for creation, not a run-wide override: changing it leaves existing rows
    /// alone, by design. A user who tuned one stage to Normal did not ask for the other
    /// two to change when they later edited a default, so propagation is the explicit
    /// "Apply to all stages" button and nothing else.
    /// </remarks>
    [ObservableProperty]
    private DistributionFamily _defaultStageServiceFamily = DistributionFamily.Exponential;

    /// <summary>
    /// Arrival family stamped onto every NEW stage row, and shown on the row as the
    /// family's own record of intent (Phase 8K, D-150, task 8K.4).
    /// </summary>
    /// <remarks>
    /// Separate from the Model section's inter-arrival dropdown, which stays the value
    /// the run actually uses: arrivals are generated engine-wide and Core has no
    /// per-stage arrival family. This is the row's label for that one process.
    /// </remarks>
    [ObservableProperty]
    private DistributionFamily _defaultStageArrivalFamily = DistributionFamily.Exponential;

    /// <summary>Day-of-week options for a multi-day run (clinic week, CONTEXT §1.1).</summary>
    public IReadOnlyList<string> StartDays { get; } =
        new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday" };

    /// <summary>Trace detail levels offered by the advanced dropdown.</summary>
    public IReadOnlyList<string> TraceLevels { get; } =
        new[] { "Minimal", "Standard", "Detailed", "Debug" };

    public ConfigPanelViewModel()
    {
        StageCount.Value = DefaultStages.ToString();
        ManualLambda.ValueChanged += (_, _) => RecomputeRho();
        ManualMuPerStage.ValueChanged += (_, _) =>
        {
            // A comma-list edit changes each stage's effective μ source (5d.1).
            RecomputeRho();
            RefreshStageSourceLabels();
        };
        StageCount.ValueChanged += (_, _) => OnStageCountEdited();
        EnsureStageCount(DefaultStages);
    }

    private const int DefaultStages = 3;

    /// <summary>Upload button: raises <see cref="UploadRequested"/> (the view resolves the file path).</summary>
    public event EventHandler? UploadRequested;

    /// <summary>Clear-All button: raises <see cref="ClearAllRequested"/> (the view confirms via ThemedDialog first).</summary>
    public event EventHandler? ClearAllRequested;

    /// <summary>
    /// Raised when the user clicks Start Calculation. The parent view model
    /// subscribes, snaps the parameters into a <see cref="SimulationParameters"/>
    /// and runs them on a background thread (Phase 5).
    /// </summary>
    public event EventHandler? RunRequested;

    /// <summary>Hosts the run: raises <see cref="RunRequested"/> (validation is blur-based on the config fields).</summary>
    [RelayCommand(CanExecute = nameof(CanStartCalculation))]
    private void StartCalculation() => RunRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Uploads the patient data file (the view resolves the picker path).</summary>
    [RelayCommand]
    private void UploadData() => UploadRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the view to confirm a full reset before clearing any field.</summary>
    [RelayCommand]
    private void ClearAll() => ClearAllRequested?.Invoke(this, EventArgs.Empty);

    // ── Data-source mode (Phase 7C) ─────────────────────────────────────

    /// <summary>Label for the "fit the parameters from an uploaded data file" path.</summary>
    public const string FitFromDataLabel = "Fit from an uploaded data file";

    /// <summary>Label for the "enter the parameters manually" path.</summary>
    public const string EnterManuallyLabel = "Enter parameters manually";

    /// <summary>The two data-source labels offered by the mode selector, in dropdown order.</summary>
    public IReadOnlyList<string> SourceModeOptions { get; } =
        new[] { FitFromDataLabel, EnterManuallyLabel };

    /// <summary>
    /// Which parameter source drives a run (default <see cref="DataSourceMode.FitFromData"/>).
    /// The enum is the single source of truth; the string-backed dropdown is kept
    /// in sync through <see cref="SourceModeSelection"/> (Phase 7C).
    /// </summary>
    [ObservableProperty]
    private DataSourceMode sourceMode = DataSourceMode.FitFromData;

    /// <summary>The dropdown's committed label, kept in sync with <see cref="SourceMode"/> (string-based SearchableDropdown seam).</summary>
    [ObservableProperty]
    private string sourceModeSelection = FitFromDataLabel;

    /// <summary>Whether the Data section is shown — only while fitting from data (7C.3).</summary>
    public bool IsDataSectionVisible => SourceMode == DataSourceMode.FitFromData;

    /// <summary>Whether the per-stage μ inputs are editable — only in manual mode (7C.5).</summary>
    public bool IsManualMuEditable => SourceMode == DataSourceMode.EnterManually;

    /// <summary>Whether the comma-list μ field is shown — only while fitting from data (7C.5).</summary>
    public bool IsCommaListMuVisible => SourceMode == DataSourceMode.FitFromData;

    partial void OnSourceModeSelectionChanged(string value)
    {
        var mode = string.Equals(value, EnterManuallyLabel, StringComparison.Ordinal)
            ? DataSourceMode.EnterManually
            : DataSourceMode.FitFromData;
        if (mode != SourceMode)
        {
            SourceMode = mode;
        }
    }

    partial void OnSourceModeChanged(DataSourceMode value)
    {
        // Keep the string-backed dropdown seam in sync when the mode is set
        // programmatically (ResetToDefaults, tests) as well as from the UI.
        string label = value == DataSourceMode.EnterManually ? EnterManuallyLabel : FitFromDataLabel;
        if (!string.Equals(SourceModeSelection, label, StringComparison.Ordinal))
        {
            SourceModeSelection = label;
        }

        if (value == DataSourceMode.EnterManually && string.IsNullOrWhiteSpace(PExit.Value))
        {
            // A manual run has no fitted p_exit to fall back on, so seed the
            // documented default (7C.5). Switching back never clears it.
            PExit.Value = "0.4";
        }

        if (value == DataSourceMode.EnterManually && !ParametersIsOptionalEnabled)
        {
            // The λ / μ / p_exit fields live in the optional Parameters section;
            // manual mode requires them, so turn the section on (7C.5).
            ParametersIsOptionalEnabled = true;
        }

        OnPropertyChanged(nameof(IsDataSectionVisible));
        OnPropertyChanged(nameof(IsManualMuEditable));
        OnPropertyChanged(nameof(IsCommaListMuVisible));
        RefreshStageSourceLabels();
        RecomputeRho();
        RecomputeBlockingState();
    }

    // ── Section 1 · Data ────────────────────────────────────────────────

    /// <summary>Status line under the Upload button ("No file loaded" or "Loaded N rows from …").</summary>
    [ObservableProperty]
    private string _dataStatus = "No file loaded";

    /// <summary>Name of the last successfully loaded file, or null.</summary>
    public string? LoadedFileName { get; private set; }

    // ── Section 2 · Model ───────────────────────────────────────────────

    /// <summary>Selected inter-arrival distribution (label; value maps in the VM).</summary>
    [ObservableProperty]
    private string? _interArrivalDistribution = "Exponential";

    /// <summary>Significance level α for every chi-square verdict (strictly between 0 and 1; default 0.05, D-113).</summary>
    public ConfigFieldViewModel SignificanceLevel { get; } = new() { Value = "0.05" };

    /// <summary>The α the run will use: the parsed field when valid, else the default (defence in depth).</summary>
    public double SignificanceLevelForRun =>
        double.TryParse(SignificanceLevel.Value, out var alpha) && alpha is > 0 and < 1
            ? alpha
            : FitsService.DefaultAlpha;

    /// <summary>True when parameters are entered as rates (default), false when mean-wise (CONTEXT §5.6).</summary>
    [ObservableProperty]
    private bool _isRateWise = true;

    /// <summary>True when parameters are entered mean-wise (1/λ, 1/μ in minutes).</summary>
    [ObservableProperty]
    private bool _isMeanWise;

    /// <summary>Rate-wise or Mean-wise interpretation of the manual λ and per-stage μ values.</summary>
    [ObservableProperty]
    private ParameterMode parameterMode = ParameterMode.RateWise;

    /// <summary>The unit used for rate/mean input.</summary>
    [ObservableProperty]
    private TimeUnit timeUnit = TimeUnit.Minutes;

    /// <summary>
    /// Canonical rate-vs-mean flag (Phase 7A): the radio-backed booleans stay in
    /// sync with it so the single source of truth is the enum, not a pair of
    /// flags that could disagree.
    /// </summary>
    partial void OnParameterModeChanged(ParameterMode value)
    {
        IsRateWise = value == ParameterMode.RateWise;
        IsMeanWise = value == ParameterMode.MeanWise;
    }

    partial void OnIsRateWiseChanged(bool value)
    {
        if (value)
        {
            IsMeanWise = false;
            ParameterMode = ParameterMode.RateWise;
        }

        RecomputeRho();
        RefreshStageSourceLabels();
    }

    partial void OnIsMeanWiseChanged(bool value)
    {
        if (value)
        {
            IsRateWise = false;
            ParameterMode = ParameterMode.MeanWise;
        }

        RecomputeRho();
        RefreshStageSourceLabels();
    }

    // ── Section 3 · Parameters ──────────────────────────────────────────

    /// <summary>Manual arrival-rate override; blank = use the fitted value.</summary>
    public ConfigFieldViewModel ManualLambda { get; } = new();

    /// <summary>Manual per-stage service-rate override (comma separated); blank = use fitted values.</summary>
    public ConfigFieldViewModel ManualMuPerStage { get; } = new();

    /// <summary>Exit probability override; only meaningful for 2+ stages (Core contract: 0 ≤ p &lt; 1).</summary>
    public ConfigFieldViewModel PExit { get; } = new();

    /// <summary>
    /// Direct-to-Doctor probability override (D-179); only meaningful for 3+
    /// stages, because bypass skips a stage and needs one to skip.
    /// </summary>
    public ConfigFieldViewModel PBypass { get; } = new();

    /// <summary>
    /// Read-only utilisation per stage, computed live from the current field
    /// values (ρ = λ / (c·μ)); "—" while the data needed is not available.
    /// </summary>
    [ObservableProperty]
    private string _rhoSummary = "—";

    // ── Section 4 · Stages (1–5) ────────────────────────────────────────

    /// <summary>Number-of-stages input field.</summary>
    public ConfigFieldViewModel StageCount { get; } = new();

    /// <summary>Ordinal index of the next default stage name (the 4th row is "Stage 4").</summary>
    private int _nextStageNameIndex = DefaultStageNames.Length;

    /// <summary>The per-stage rows, resized live when <see cref="StageCount"/> changes.</summary>
    public ObservableCollection<StageRow> StageRows { get; } = new();

    /// <summary>
    /// Every stage's own service family, as the Input tab consumes it (Phase 8L).
    /// The one place the rows are projected into that shape, so the tab cannot
    /// diverge from the coordinator's per-stage chi-square, which reads the same
    /// families off the same rows.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain computed property with no change notification: callers
    /// read it when something has already told them to re-derive (a binding change,
    /// an α change, or <see cref="StageServiceFamiliesChanged"/>), so raising a
    /// property-changed event here would only duplicate that trigger.
    /// </remarks>
    public IReadOnlyList<StageServiceFamily> StageServiceFamilies
        => StageRows.Select(row => new StageServiceFamily(row.StageName, row.ServiceFamily)).ToList();

    /// <summary>
    /// Whether the p_exit override is shown. Per spec it appears only when 2+
    /// stages are configured (an early-exit route needs at least one downstream stage).
    /// </summary>
    public bool PExitVisible => TryStageCount(out var n) && n >= 2;

    /// <summary>
    /// Whether the p_bypass override is shown. Two stages is the floor since 8R:
    /// with two stages the skipped stage is the front door, so the draw happens at
    /// the arrival event and a real fraction of patients reach the doctor without a
    /// screening record (D-189). That is the shape of the clinic capture, and the
    /// 8Q engine refused to run it at all.
    /// </summary>
    public bool PBypassVisible => TryStageCount(out var n) && n >= 2;

    /// <summary>
    /// Whether the daily admitted-load cap is shown (D-190).
    /// </summary>
    /// <remarks>
    /// Shown for both calendar run modes, because a single clinic session has a
    /// backlog at its close just as a week of sessions does. Hidden for a diagnostic
    /// trace, which has no session, no opening hours and therefore no cap — a field
    /// that is visible but silently ignored would be a lie.
    /// </remarks>
    public bool DailyCapVisible => IsSingleDay || IsMultiDay;

    // ── Section 5 · Horizon ─────────────────────────────────────────────

    /// <summary>Run-mode radio: true for a single clinic day (default).</summary>
    [ObservableProperty]
    private bool _isSingleDay = true;

    /// <summary>Run-mode radio: true for a multi-day horizon.</summary>
    [ObservableProperty]
    private bool _isMultiDay;

    /// <summary>Run-mode radio: true for the diagnostic trace run (D-105).</summary>
    [ObservableProperty]
    private bool _isDiagnosticTrace;

    /// <summary>Accounts the three radios: exactly one is checked, from <see cref="RunMode"/>.</summary>
    public RunMode ActiveRunMode => IsDiagnosticTrace ? RunMode.DiagnosticTrace
        : IsMultiDay ? RunMode.MultiDay
        : RunMode.ClinicDay;

    // ── Diagnostic-trace duration (D-174, ruling 6) ───────────────────────

    /// <summary>
    /// Duration labels for the diagnostic-trace run, in dropdown order, with
    /// the default first (ruling 6).
    /// </summary>
    /// <remarks>
    /// The default is 1 hour, not 15 minutes and not the old 10000-minute field.
    /// A diagnostic trace prints every event, so its cost is paid per simulated
    /// minute: the previous default produced a trace too long to read, and 15
    /// minutes was too short to show a patient completing the network. One
    /// operating session's worth of arrivals is the length that actually
    /// demonstrates the engine.
    /// </remarks>
    public IReadOnlyList<string> DurationOptions { get; } =
        new[] { "1 hour", "15 minutes", "Custom minutes…" };

    /// <summary>Currently selected diagnostic duration (ruling 6).</summary>
    [ObservableProperty]
    private DiagnosticDurationPreset duration = DiagnosticDurationPreset.OneHour;

    /// <summary>The dropdown's committed label, kept in sync with <see cref="Duration"/>.</summary>
    [ObservableProperty]
    private string durationSelection = "1 hour";

    /// <summary>Whether the custom-minutes field is shown (ruling 6).</summary>
    [ObservableProperty]
    private bool isCustomMinutesVisible;

    /// <summary>Custom arrival-window minutes, visible only for <see cref="DiagnosticDurationPreset.CustomMinutes"/>.</summary>
    [ObservableProperty]
    private ConfigFieldViewModel customMinutes = new() { Value = "10000" };

    partial void OnDurationSelectionChanged(string value)
    {
        var preset = ParseDurationPreset(value);
        if (preset != Duration)
        {
            Duration = preset;
        }
    }

    partial void OnDurationChanged(DiagnosticDurationPreset value)
    {
        string label = DurationLabel(value);
        if (!string.Equals(DurationSelection, label, StringComparison.Ordinal))
        {
            DurationSelection = label;
        }

        IsCustomMinutesVisible = value == DiagnosticDurationPreset.CustomMinutes;
        RecomputeBlockingState();
    }

    /// <summary>
    /// The diagnostic run's arrival window in minutes (ruling 6).
    /// </summary>
    /// <returns>
    /// 60 or 15 for the fixed presets; the validated field for Custom, or 0 when
    /// that field does not parse — the run then refuses at build time rather
    /// than silently simulating a default.
    /// </returns>
    public int ResolveDiagnosticMinutes() =>
        Duration switch
        {
            DiagnosticDurationPreset.OneHour => DefaultDiagnosticMinutes,
            DiagnosticDurationPreset.FifteenMinutes => 15,
            _ => int.TryParse(CustomMinutes.Value, out var minutes) && minutes >= 1 ? minutes : 0,
        };

    /// <summary>
    /// The diagnostic run's default arrival window, in minutes (D-174 ruling 6).
    /// </summary>
    /// <remarks>
    /// Also the value handed to a CALENDAR run's <c>HorizonMinutes</c>, which that
    /// mode ignores. A constant rather than a second switch: the calendar modes
    /// need *some* legal number in the record, and reusing the diagnostic default
    /// says plainly that the field is inert here. Naming it keeps a reader from
    /// wondering whether 60 is a clinic figure or a magic number.
    /// </remarks>
    public const int DefaultDiagnosticMinutes = 60;

    private static string DurationLabel(DiagnosticDurationPreset preset) =>
        preset switch
        {
            DiagnosticDurationPreset.FifteenMinutes => "15 minutes",
            DiagnosticDurationPreset.CustomMinutes => "Custom minutes…",
            _ => "1 hour",
        };

    private static DiagnosticDurationPreset ParseDurationPreset(string label) =>
        label switch
        {
            "15 minutes" => DiagnosticDurationPreset.FifteenMinutes,
            "Custom minutes…" => DiagnosticDurationPreset.CustomMinutes,
            _ => DiagnosticDurationPreset.OneHour,
        };

    /// <summary>Number of clinic days (visible only in multi-day mode).</summary>
    public ConfigFieldViewModel Days { get; } = new();

    /// <summary>
    /// Default admitted-load cap, in patients per session (D-190).
    /// </summary>
    /// <remarks>
    /// Not a bare literal scattered through the code: the field default, the
    /// watermark and the tests all read it from here so they cannot disagree about
    /// what a fresh launch shows.
    /// </remarks>
    public const string DefaultDailyCap = "85";

    /// <summary>
    /// Daily admitted-load cap: the most patients admitted to Screening in one
    /// session. Shown for both calendar run modes; blank means unlimited (D-190).
    /// </summary>
    public ConfigFieldViewModel DailyCap { get; } = new() { Value = DefaultDailyCap };

    /// <summary>First day of a multi-day run (clinic week, CONTEXT §1.1).</summary>
    [ObservableProperty]
    private string? _startDay = "Monday";

    /// <summary>Whether the Days / Start-day / Daily-cap fields are shown (multi-day mode only).</summary>
    public bool IsMultiDayViewVisible => IsMultiDay;

    /// <summary>Whether the Trace-level dropdown is shown — only in diagnostic mode (D-105).</summary>
    public bool TraceLevelVisible => IsDiagnosticTrace;

    partial void OnIsMultiDayChanged(bool value)
    {
        if (value)
        {
            IsSingleDay = false;
            IsDiagnosticTrace = false;
            ValidateDays();
        }
        else
        {
            Days.ClearError();
        }

        // Daily-cap participates only in multi-day mode: leaving the mode
        // drops any stale inline error with the hidden field (D-105).
        DailyCap.ClearError();
        RecomputeBlockingState();
    }

    partial void OnIsSingleDayChanged(bool value)
    {
        if (value)
        {
            IsMultiDay = false;
            IsDiagnosticTrace = false;
            RecomputeBlockingState();
        }
    }

    partial void OnIsDiagnosticTraceChanged(bool value)
    {
        if (value)
        {
            IsSingleDay = false;
            IsMultiDay = false;
            if (IsCustomMinutesVisible)
            {
                ValidateCustomMinutes();
            }
        }
        else
        {
            CustomMinutes.ClearError();
        }
        RecomputeBlockingState();
    }

    // ── Section 6 · Advanced ────────────────────────────────────────────

    /// <summary>Random seed (default 42) — deterministic runs.</summary>
    public ConfigFieldViewModel Seed { get; } = new();

    /// <summary>Selected trace level (default Detailed).</summary>
    [ObservableProperty]
    private string? _traceLevel = "Detailed";

    // ── Optional-section toggles (Phase 4b, D-101) ──────────────────────

    /// <summary>
    /// Enable switch for the optional Parameters section. When off (default),
    /// the manual λ / μ / p_exit overrides are treated as not supplied (the
    /// same as empty strings) and ρ shows "—"; the run then always uses the
    /// fitted values (Phase 5).
    /// </summary>
    [ObservableProperty]
    private bool _parametersIsOptionalEnabled;

    /// <summary>
    /// Enable switch for the optional Advanced section. When off (default),
    /// the random seed defaults to 42 and the trace level to Detailed.
    /// </summary>
    [ObservableProperty]
    private bool _advancedIsOptionalEnabled;

    partial void OnParametersIsOptionalEnabledChanged(bool value)
    {
        if (!value && SourceMode == DataSourceMode.EnterManually)
        {
            // In manual mode the Parameters fields are the required input, so
            // the optional toggle may not disable them (7C.5). Force it back on.
            ParametersIsOptionalEnabled = true;
            return;
        }

        if (!value)
        {
            // An OFF optional section means its values are "not supplied":
            // any stale inline errors must go away too (D-103).
            ManualLambda.ClearError();
            ManualMuPerStage.ClearError();
            PExit.ClearError();
            PBypass.ClearError();
        }

        OnPropertyChanged(nameof(ParametersSupplied));
        RecomputeRho();
        RefreshStageSourceLabels();
        RecomputeBlockingState();
    }

    partial void OnAdvancedIsOptionalEnabledChanged(bool value)
    {
        if (!value)
        {
            Seed.ClearError();
        }

        OnPropertyChanged(nameof(EffectiveSeed));
        OnPropertyChanged(nameof(EffectiveTraceLevel));
        RecomputeBlockingState();
    }

    /// <summary>
    /// When Parameters is OFF (and we are not in manual mode), its entries are
    /// "not supplied" — the µ comes from data only. Manual mode (7C) always
    /// supplies its λ / μ / p_exit, so the toggle is bypassed there.
    /// </summary>
    public bool ParametersSupplied =>
        ParametersIsOptionalEnabled || SourceMode == DataSourceMode.EnterManually;

    // ── Collapsible-section expansion (Phase 8D.3) ──────────────────────

    /// <summary>Whether the "2 · Model" section is expanded.</summary>
    [ObservableProperty]
    private bool _isModelSectionExpanded = true;

    /// <summary>Whether the "3 · Parameters" section is expanded.</summary>
    [ObservableProperty]
    private bool _isParametersSectionExpanded = true;

    /// <summary>Whether the "4 · Stages" section is expanded.</summary>
    [ObservableProperty]
    private bool _isStagesSectionExpanded = true;

    /// <summary>Whether the "5 · Horizon" section is expanded.</summary>
    [ObservableProperty]
    private bool _isHorizonSectionExpanded = true;

    /// <summary>Whether the "6 · Advanced" section is expanded.</summary>
    [ObservableProperty]
    private bool _isAdvancedSectionExpanded = true;

    /// <summary>Collapses every configuration section to its header (Phase 8D.3).</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        IsModelSectionExpanded = false;
        IsParametersSectionExpanded = false;
        IsStagesSectionExpanded = false;
        IsHorizonSectionExpanded = false;
        IsAdvancedSectionExpanded = false;
    }

    /// <summary>
    /// Copies the two default families onto every existing stage row (Phase 8K, D-150,
    /// task 8K.4) — the one and only way a default propagates to rows that already exist.
    /// </summary>
    /// <remarks>
    /// Spread parameters are cleared for the rows that lose their family, because
    /// <see cref="StageRow.ClearShapeParametersFor"/> runs from the family change and
    /// a surviving σ under a Gamma family would be read as a shape.
    /// </remarks>
    [RelayCommand]
    private void ApplyDefaultsToAllStages()
    {
        foreach (var row in StageRows)
        {
            // ApplyFamilies also marks the notation "Custom", because after this the
            // shorthand no longer describes the row and leaving a stale M/M/1 beside a
            // Normal family would misreport what the run will do.
            row.ApplyFamilies(DefaultStageArrivalFamily, DefaultStageServiceFamily);
        }

        RecomputeRho();
        RecomputeBlockingState();
    }

    /// <summary>Expands every configuration section (Phase 8D.3).</summary>
    [RelayCommand]
    private void ExpandAll()
    {
        IsModelSectionExpanded = true;
        IsParametersSectionExpanded = true;
        IsStagesSectionExpanded = true;
        IsHorizonSectionExpanded = true;
        IsAdvancedSectionExpanded = true;
    }

    // ── Stage-data mismatch warning (Phase 5d, D-114) ─────────────────────

    /// <summary>True while the loaded data's detected stage count differs from the configured stage list.</summary>
    [ObservableProperty]
    private bool _isStageMismatchWarningVisible;

    /// <summary>The amber "Uploaded data has N stage(s)…" message (or empty when none).</summary>
    [ObservableProperty]
    private string _stageMismatchMessage = "";

    /// <summary>Sync-stages button: raises <see cref="SyncStagesRequested"/> (the view confirms via ThemedDialog first).</summary>
    public event EventHandler? SyncStagesRequested;

    /// <summary>Keep-stages button: raises <see cref="KeepStageMismatchRequested"/> (no destructive change, no dialog).</summary>
    public event EventHandler? KeepStageMismatchRequested;

    /// <summary>Replaces the configured stages with the data's stages (view confirms first, then calls <see cref="SyncStagesToData"/>).</summary>
    [RelayCommand]
    private void SyncStagesFromData() => SyncStagesRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Dismisses the mismatch warning for this session without changing the stage list.</summary>
    [RelayCommand]
    private void KeepCurrentStages() => KeepStageMismatchRequested?.Invoke(this, EventArgs.Empty);

    // ── Phase 7D: Simulation-tab data status strip ───────────────────────

    /// <summary>One-line status of the active configuration path, shown in the Simulation tab's data strip.</summary>
    [ObservableProperty]
    private string configSourceStatus = "No data — enter manually or upload in the Input tab.";

    /// <summary>The strip's full display text.</summary>
    public string ConfigSourceText => $"Data source: {ConfigSourceStatus}";

    partial void OnConfigSourceStatusChanged(string value) => OnPropertyChanged(nameof(ConfigSourceText));

    /// <summary>Alias of <see cref="IsStageMismatchWarningVisible"/> for the strip's one-line indicator.</summary>
    public bool HasStageMismatch => IsStageMismatchWarningVisible;

    partial void OnIsStageMismatchWarningVisibleChanged(bool value) => OnPropertyChanged(nameof(HasStageMismatch));

    /// <summary>Raised when the strip's "Manage input →" button asks the shell to open the Input tab.</summary>
    public event EventHandler? NavigateToInputTabRequested;

    /// <summary>Opens the Input tab (handled by <see cref="MainViewModel"/>, Phase 7D).</summary>
    [RelayCommand]
    private void NavigateToInputTab() => NavigateToInputTabRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Unloads the current data file without resetting the configuration fields
    /// (Phase 7D: the Input tab's Clear File action). Mirrors the file-related
    /// part of <see cref="ResetToDefaults"/> and raises
    /// <see cref="DataBindingChanged"/> so the Input tab clears too.
    /// </summary>
    public void ClearLoadedFile()
    {
        Binding = null;
        LoadedFileName = null;
        DataStatus = "No file loaded";
        IsStageMismatchWarningVisible = false;
        StageMismatchMessage = "";
        RecomputeBlockingState();
        RaiseDataBindingChanged();
    }

    /// <summary>Random seed the run will use (42 while the Advanced section is off).</summary>
    public int EffectiveSeed =>
        AdvancedIsOptionalEnabled && int.TryParse(Seed.Value, out var seed) ? seed : 42;

    /// <summary>Trace level the run will use (Detailed while the Advanced section is off).</summary>
    public string EffectiveTraceLevel => AdvancedIsOptionalEnabled ? TraceLevel ?? "Detailed" : "Detailed";

    // ── Footer ──────────────────────────────────────────────────────────

    /// <summary>
    /// True while every required field validates clean. Any inline error that
    /// would make the run meaningless (invalid p_exit, λ, μ, servers, …)
    /// disables the Start button with the field's inline error explaining why.
    /// </summary>
    [ObservableProperty]
    private bool _startIsEnabled = true;

    /// <summary>
    /// Human-readable summary of why Start is disabled (empty when it is
    /// enabled). Names the exact missing field(s) per FR-UI-9/D-128, e.g.
    /// "Cannot start: Reception has no μ, p_exit must be less than 1.".
    /// </summary>
    [ObservableProperty]
    private string _startBlockedMessage = "";

    private bool CanStartCalculation() => StartIsEnabled;

    /// <summary>Blur validation for the manual λ field (blank is valid — the fitted value is used).</summary>
    public void ValidateManualLambda()
    {
        var value = ManualLambda.Value;
        if (string.IsNullOrWhiteSpace(value) || (double.TryParse(value, out var lambda) && lambda > 0))
        {
            ManualLambda.ClearError();
        }
        else
        {
            ManualLambda.SetError($"Manual λ must be a positive number. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the manual μ field (comma-separated positives; blank is valid).</summary>
    public void ValidateManualMuPerStage()
    {
        var value = ManualMuPerStage.Value;
        if (string.IsNullOrWhiteSpace(value) || AllPartsPositive(value))
        {
            ManualMuPerStage.ClearError();
        }
        else
        {
            ManualMuPerStage.SetError(
                $"Enter comma-separated positive rates, e.g. 0.8, 0.6, 0.4. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>
    /// Blur validation for p_exit. The Core contract is 0 ≤ p &lt; 1 — the GUI
    /// matches it exactly: empty is valid, anything outside [0,1) is an error
    /// (D-XXX — see DECISIONS.md).
    /// </summary>
    public void ValidatePExit()
    {
        var value = PExit.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            PExit.ClearError();
        }
        else if (!double.TryParse(value, out var pExit) || pExit < 0)
        {
            PExit.SetError("Enter a number between 0 and 1 (exclusive).");
        }
        else if (pExit >= 1)
        {
            PExit.SetError($"Exit probability must be less than 1. You entered {pExit}.");
        }
        else
        {
            PExit.ClearError();
        }

        RecomputeBlockingState();
    }

    /// <summary>
    /// Blur validation for p_bypass. Same [0, 1) contract as p_exit — the engine
    /// draws U once per completion and bypasses when U &lt; p, so a probability of
    /// exactly 1 is a configuration mistake rather than a routing rule (D-179).
    /// </summary>
    public void ValidatePBypass()
    {
        var value = PBypass.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            PBypass.ClearError();
        }
        else if (!double.TryParse(value, out var pBypass) || pBypass < 0)
        {
            PBypass.SetError("Enter a number between 0 and 1 (exclusive).");
        }
        else if (pBypass >= 1)
        {
            PBypass.SetError($"Bypass probability must be less than 1. You entered {pBypass}.");
        }
        else
        {
            PBypass.ClearError();
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the number-of-stages field (integer 1–5).</summary>
    public void ValidateStageCount()
    {
        var value = StageCount.Value;
        if (TryStageCount(out var n) && n is >= 1 and <= 5)
        {
            StageCount.ClearError();
        }
        else
        {
            StageCount.SetError($"Number of stages must be between 1 and 5. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the custom diagnostic minutes field (integer ≥ 1; D-174).</summary>
    public void ValidateCustomMinutes()
    {
        var value = CustomMinutes.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            CustomMinutes.ClearError();
        }
        else
        {
            CustomMinutes.SetError($"Duration must be a whole number of minutes above 0. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the horizon days field (integer ≥ 1).</summary>
    public void ValidateDays()
    {
        var value = Days.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            Days.ClearError();
        }
        else
        {
            Days.SetError($"Days must be a whole number of at least 1. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the optional daily cap (integer ≥ 1; blank = unlimited).</summary>
    public void ValidateDailyCap()
    {
        var value = DailyCap.Value;
        if (string.IsNullOrWhiteSpace(value) || (int.TryParse(value, out var n) && n >= 1))
        {
            DailyCap.ClearError();
        }
        else
        {
            DailyCap.SetError($"Daily cap must be a whole number of at least 1. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>
    /// Blur validation for α (D-113): any parseable number strictly inside
    /// (0, 1). α is a required Model field, not an optional-section one, so it
    /// participates in Start gating unconditionally.
    /// </summary>
    public void ValidateSignificanceLevel()
    {
        var value = SignificanceLevel.Value;
        if (!double.TryParse(value, out var alpha))
        {
            SignificanceLevel.SetError("Enter a number between 0 and 1.");
        }
        else if (alpha <= 0 || alpha >= 1)
        {
            SignificanceLevel.SetError($"Significance level must be strictly between 0 and 1. You entered {alpha}.");
        }
        else
        {
            SignificanceLevel.ClearError();
        }

        RecomputeBlockingState();
    }

    /// <summary>Blur validation for the random seed (any integer).</summary>
    public void ValidateSeed()
    {
        var value = Seed.Value;
        if (int.TryParse(value, out _))
        {
            Seed.ClearError();
        }
        else
        {
            Seed.SetError($"Random seed must be a whole number. You entered \"{value}\".");
        }

        RecomputeBlockingState();
    }

    /// <summary>
    /// The analysed binding for the last loaded file, or null. The run reads
    /// its fitted λ / μ / p_exit from here when a manual value is absent
    /// (D-104); the results panel previews the same dataset.
    /// </summary>
    public DataBindingResult? Binding { get; private set; }

    /// <summary>
    /// Raised whenever <see cref="Binding"/> changes — a file was loaded or the
    /// config was reset. Subscribers (the Input Analysis tab) re-derive their
    /// charts from the new binding (Phase 6C, 6c.2).
    /// </summary>
    public event EventHandler? DataBindingChanged;

    /// <summary>
    /// Raised when any stage row's <see cref="StageRow.ServiceFamily"/> changes
    /// (Phase 8L). The Input tab's per-stage chi-square is computed against each
    /// stage's own family, so it has to be re-derived when that changes — and
    /// nothing else in this class re-raises a panel-level notification for a row
    /// edit. Before 8L the tab listened to a single global dropdown that no longer
    /// affected anything, so it never refreshed on the edit that actually mattered.
    /// </summary>
    public event EventHandler? StageServiceFamiliesChanged;

    /// <summary>
    /// Reads the MLE-vs-window λ choice owned by <see cref="MainViewModel"/>
    /// (D-173, ruling 7). Set once by the shell; null in standalone unit tests.
    /// </summary>
    /// <remarks>
    /// A delegate rather than a property so this panel stores no second copy of
    /// the choice — see <c>SelectedLambdaSource()</c>.
    /// </remarks>
    public Func<LambdaSource>? LambdaSourceAccessor { get; set; }

    internal void RaiseDataBindingChanged() => DataBindingChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Applies a loaded data file: the view resolves the picker path, this
    /// method analyses the file (load, validate, fit λ / μ / p_exit) and
    /// reports the outcome.
    /// </summary>
    /// <param name="filePath">Absolute path to an .xlsx or .csv patient file.</param>
    public void ApplyLoadedFile(string filePath)
    {
        Binding = DataAnalyzer.Analyze(filePath);
        LoadedFileName = Path.GetFileName(filePath);
        if (Binding.IsUsable)
        {
            DataStatus = $"Loaded {Binding.DataSet!.RowCount} rows from {LoadedFileName}";
        }
        else if (Binding.ErrorMessage is not null)
        {
            Log.Warning("Data file could not be used: {Path}", filePath);
            LoadedFileName = null;
            DataStatus = $"Could not load file: {Path.GetFileName(filePath)}";
        }
        else
        {
            DataStatus = $"Loaded {Binding.DataSet!.RowCount} rows — {Binding.Issues.Count} issue(s) to review";
        }

        RefreshStageSourceLabels();
        RecomputeStagesMismatch();
        // A usable file can complete fit mode, so the gate must re-evaluate
        // immediately (7C.6); an unusable one keeps Start blocked.
        RecomputeBlockingState();
        RaiseDataBindingChanged();
    }

    /// <summary>
    /// Compares the detected stage count from the loaded data with the
    /// configured stage list and shows the amber warning (or clears it) when
    /// they differ (5d.3, D-114). A mismatch means some stage has no service
    /// rate — the run would be refused until it is resolved.
    /// </summary>
    private void RecomputeStagesMismatch()
    {
        if (Binding is not { IsUsable: true })
        {
            IsStageMismatchWarningVisible = false;
            StageMismatchMessage = "";
            return;
        }

        int configured = StageRows.Count;
        int detected = Binding.StageNames.Count;
        string names = string.Join(", ", Binding.StageNames);
        if (detected < configured)
        {
            StageMismatchMessage =
                $"⚠ Uploaded data has {detected} stage(s) — {names}, but {configured} stage(s) are configured. Configured stages not covered by the data will have no service rate.";
            IsStageMismatchWarningVisible = true;
        }
        else if (detected > configured)
        {
            StageMismatchMessage =
                $"⚠ Uploaded data has {detected} stage(s) — {names}, but {configured} stage(s) are configured. Extra stages in the data will be ignored.";
            IsStageMismatchWarningVisible = true;
        }
        else
        {
            IsStageMismatchWarningVisible = false;
            StageMismatchMessage = "";
        }
    }

    /// <summary>
    /// Replaces the configured stage list with the stages detected in the
    /// loaded data (names and count). Called by the code-behind after the user
    /// confirms the themed dialog (5d.3).
    /// </summary>
    public void SyncStagesToData()
    {
        if (Binding?.StageNames is not { Count: > 0 } names)
        {
            return;
        }

        StageCount.Value = names.Count.ToString();
        StageRows.Clear();
        EnsureStageCount(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            StageRows[i].StageName = names[i];
        }

        RefreshStageSourceLabels();
        // The rows were just renamed to the data's stages; the gate must see the
        // new names before deciding whether every stage has a μ (7C.6).
        RecomputeBlockingState();
        IsStageMismatchWarningVisible = false;
        StageMismatchMessage = "";
        Log.Information("Stages synced to the loaded data: {Stages}", string.Join(", ", names));
    }

    /// <summary>
    /// Copies recorded historical server counts into the Stages section's Servers
    /// fields (Phase 8Q.1, rulings 6, D-178).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are different numbers and this is the only place they meet.</b> The
    /// Input tab's counts are a recollection about the clinic as it was when the
    /// data was recorded, and they drive the historical-utilisation readout. The
    /// Stages section's counts are a decision about the model the simulation will
    /// run. Seeding is a one-way copy made once, when the user asks for it, and it
    /// is never re-applied: editing the Input tab afterwards does not rewrite
    /// Stages, so a hand-tuned configuration cannot be overwritten by a later
    /// visit to a historical field.
    /// </para>
    /// <para>
    /// Stages whose name matches no recorded count are left alone rather than
    /// reset, and a recorded count with no matching row is reported as skipped.
    /// Silently writing a 1 over a configured 3 would be the one outcome the
    /// separation exists to prevent.
    /// </para>
    /// </remarks>
    /// <param name="counts">Stage name → recorded server count.</param>
    /// <returns>The number of Stages rows written.</returns>
    public int SeedServerCountsFromHistory(IReadOnlyDictionary<string, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        if (counts.Count == 0)
        {
            return 0;
        }

        int applied = 0;
        var unmatched = new List<string>();

        foreach (var row in StageRows)
        {
            if (counts.TryGetValue(row.StageName, out int servers) && servers >= 1)
            {
                row.Servers.Value = servers.ToString();
                applied++;
            }
            else if (!counts.ContainsKey(row.StageName))
            {
                unmatched.Add(row.StageName);
            }
        }

        // A recorded count the Stages section has no row for is worth saying out
        // loud: the user recorded a stage the model does not have.
        foreach (string stage in counts.Keys)
        {
            if (!StageRows.Any(r => string.Equals(r.StageName, stage, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning(
                    "Recorded server count for {Stage} did not seed any Stages row — the "
                  + "configured stages do not include it.",
                    stage);
            }
        }

        if (unmatched.Count > 0)
        {
            Log.Information(
                "Stages left unchanged while seeding server counts (no recorded count): {Stages}",
                string.Join(", ", unmatched));
        }

        // The gate reads every stage's fields, so it has to re-evaluate after the
        // write (7C.6, same reason SyncStagesToData does).
        RecomputeBlockingState();

        Log.Information(
            "Seeded simulation server counts from recorded historical counts: {Applied} of {Total} rows",
            applied,
            StageRows.Count);

        return applied;
    }

    /// <summary>Dismisses the stage-data mismatch warning for the rest of the session (5d.3).</summary>
    public void DismissStageMismatchWarning()
    {
        IsStageMismatchWarningVisible = false;
        StageMismatchMessage = "";
    }

    /// <summary>
    /// Resets every configuration field to its factory default (FR-UI-21: no
    /// session auto-restore, no last-used values). Called after the user
    /// confirms the "Clear All" themed dialog.
    /// </summary>
    public void ResetToDefaults()
    {
        DataStatus = "No file loaded";
        LoadedFileName = null;
        Binding = null;
        // Reset the source mode first: while EnterManually is active the
        // Parameters toggle is locked on, so it must be cleared from fit mode.
        SourceMode = DataSourceMode.FitFromData;
        InterArrivalDistribution = "Exponential";
        // Phase 8L: the old reset wrote the deleted global `ServiceDistribution`
        // string. The value that actually matters is the one new stages are seeded
        // from, so that is what goes back to its default.
        DefaultStageServiceFamily = DistributionFamily.Exponential;
        SignificanceLevel.Value = "0.05";
        IsRateWise = true;
        IsMeanWise = false;
        ParameterMode = ParameterMode.RateWise;
        TimeUnit = TimeUnit.Minutes;
        ManualLambda.Value = "";
        ManualMuPerStage.Value = "";
        PExit.Value = "";
        PBypass.Value = "";
        IsStageMismatchWarningVisible = false;
        StageMismatchMessage = "";
        ParametersIsOptionalEnabled = false;
        AdvancedIsOptionalEnabled = false;
        Seed.Value = "42";
        TraceLevel = "Detailed";
        IsMultiDay = false;
        IsDiagnosticTrace = false;
        IsSingleDay = true;
        StartDay = "Monday";
        Duration = DiagnosticDurationPreset.OneHour;
        CustomMinutes.Value = "10000";
        Days.Value = "1";
        DailyCap.Value = DefaultDailyCap;

        foreach (var field in AllFieldErrors())
        {
            field.ClearError();
        }

        StageRows.Clear();
        EnsureStageCount(DefaultStages);
        StageCount.Value = DefaultStages.ToString();
        RefreshStageSourceLabels();
        RecomputeRho();
        RecomputeBlockingState();
        RaiseDataBindingChanged();
    }

    private IEnumerable<ConfigFieldViewModel> AllFieldErrors()
    {
        yield return ManualLambda;
        yield return ManualMuPerStage;
        yield return PExit;
        yield return SignificanceLevel;
        yield return StageCount;
        yield return Days;
        yield return DailyCap;
        yield return CustomMinutes;
        yield return Seed;
        foreach (var row in StageRows)
        {
            yield return row.Servers;
        }
    }

    private void OnStageCountEdited()
    {
        if (TryStageCount(out var n) && n is >= 1 and <= 5)
        {
            EnsureStageCount(n);
        }

        OnPropertyChanged(nameof(PExitVisible));
        RecomputeBlockingState();
    }

    private bool TryStageCount(out int count) => int.TryParse(StageCount.Value, out count);

    /// <summary>
    /// Grows/shrinks <see cref="StageRows"/> to <paramref name="count"/>,
    /// creating (or discarding) rows at the end. Created rows get the default
    /// names (Reception / Screening / Doctor then "Stage N").
    /// </summary>
    private void EnsureStageCount(int count)
    {
        while (StageRows.Count < count)
        {
            var index = StageRows.Count;
            var name = index < DefaultStageNames.Length
                ? DefaultStageNames[index]
                : $"Stage {index + 1}";
            // Phase 8K (D-150, task 8K.4): new rows start from the defaults, not from a
            // hardcoded family. Read once per row so a row keeps the default that was
            // current when it was created.
            var row = new StageRow
            {
                StageName = name,
                ArrivalFamily = DefaultStageArrivalFamily,
                ServiceFamily = DefaultStageServiceFamily,
            };
            row.Servers.ValueChanged += (_, _) => RecomputeRho();
            // A manual per-stage μ edit changes both ρ and Start readiness (7C.4/7C.6).
            row.MuValueChanged += (_, _) =>
            {
                RecomputeRho();
                RecomputeBlockingState();
            };
            // G/G/c asks for a family to be chosen from the data. Only this class knows
            // whether a file is loaded and can reach its samples, so the row asks and
            // the answer is applied here (Phase 8K, D-151).
            row.AutoFitRequested += (_, _) => HandleAutoFitRequested(row);
            // Phase 8L: forward a row's family change to panel level. The Input tab
            // tests each stage's samples against that stage's own family, so it must
            // refresh when the user picks a different one. Subscribing to the row's
            // own PropertyChanged is the least of the three options here: the row has
            // no semantic "family changed" event (it has AutoFitRequested, which means
            // the opposite direction), and nothing else in this class re-raises for a
            // row edit. Row and panel end up mutually referenced, which the collector
            // reclaims together — the same shape as the ValueChanged subscriptions above.
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(StageRow.ServiceFamily))
                {
                    StageServiceFamiliesChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            StageRows.Add(row);
        }

        while (StageRows.Count > count)
        {
            StageRows.RemoveAt(StageRows.Count - 1);
        }

        OnPropertyChanged(nameof(PExitVisible));
        RefreshStageSourceLabels();
        RecomputeRho();
        RecomputeBlockingState();
    }

    /// <summary>
    /// Live utilisation per stage: ρ = λ / (c·μ), using the same sources the run
    /// will use (5d.1, 7C): in manual mode the entered λ and per-stage μ; in fit
    /// mode the fitted values (with the comma-list / per-stage fallback). A stage
    /// missing an input shows as "—".
    /// </summary>
    private void RecomputeRho()
    {
        double lambda = double.NaN;
        if (SourceMode == DataSourceMode.EnterManually)
        {
            if (double.TryParse(ManualLambda.Value, out var manualLambda) && manualLambda > 0)
            {
                lambda = manualLambda;
            }
        }
        else if (ParametersIsOptionalEnabled && double.TryParse(ManualLambda.Value, out var manual) && manual > 0)
        {
            lambda = manual;
        }
        else if (Binding?.FittedArrivalRate is { } fitted && fitted > 0)
        {
            lambda = fitted;
        }

        bool lambdaKnown = lambda > 0;
        var manualParts = ManualMuParts();
        var parts = StageRows.Select((row, i) =>
        {
            double? mu = EffectiveMu(row, i, manualParts);
            if (lambdaKnown
                && mu is { } m && m > 0
                && int.TryParse(row.Servers.Value, out var servers) && servers >= 1)
            {
                var rho = lambda / (m * servers);
                return $"{row.StageName}: {rho:0.00}";
            }

            return $"{row.StageName}: —";
        });

        RhoSummary = parts.Any() ? string.Join("   ", parts) : "—";
    }

    /// <summary>
    /// Answers a stage row's G/G/c request: fit a family to the loaded data for that
    /// stage, apply the winner, and say so — or refuse with a reason and put the
    /// notation back (Phase 8K, D-151).
    /// </summary>
    /// <remarks>
    /// The three refusals are the contract, not incidental branches. A G/G/c row
    /// names no family, so a refusal that left the notation in place would hand the
    /// run a stage whose family nobody chose. Every refusal therefore reverts the
    /// dropdown and leaves a cause-and-remedy message in the row's single inline
    /// error channel, and the threshold for "enough data" is deliberately not
    /// restated here: it lives in <see cref="GeneralDistributionFitter"/> and is
    /// reported through the fitter's own rejection reason, so there is one number.
    /// </remarks>
    /// <param name="row">The row that asked. Reverts its notation on every refusal.</param>
    private void HandleAutoFitRequested(StageRow row)
    {
        if (Binding is not { IsUsable: true })
        {
            row.SetInlineError(
                "G/G/c needs observed service times to choose a family, and no usable " +
                "data file is loaded. Load one on the Input tab, or pick a specific " +
                "family (M/M, M/D, D/M).");
            row.RevertSelectedModel();
            return;
        }

        var samples = StageServiceSamples(row);
        var fit = GeneralDistributionFitter.FitBest(
            samples ?? Array.Empty<double>(),
            SignificanceLevelForRun);

        if (fit.Best is null)
        {
            // FitBest states its own reason per candidate — "insufficient samples",
            // a degenerate variance, and so on — so the user is told which one bit
            // rather than a generic failure.
            var reason = fit.AllCandidates
                .Select(c => c.RejectionReason)
                .FirstOrDefault(r => !string.IsNullOrEmpty(r))
                ?? "No family fits this stage's observed service times.";
            row.SetInlineError($"G/G/c could not fit stage '{row.StageName}': {reason}");
            row.RevertSelectedModel();
            return;
        }

        row.ApplyFittedSpec(fit.Best.Spec);
        row.AutoFitBadge =
            $"Best fit: {fit.Best.Family} (AIC {fit.Best.Aic:F0}, " +
            $"p={fit.Best.ChiSquare?.PValue:F3})";

        // A filled μ changes what this row's service rate is, and the auto-fit flag
        // changes how the source label reads, so both are re-derived before the UI
        // is next asked for them.
        RefreshStageSourceLabels();
    }

    /// <summary>
    /// The observed service times for a stage, taken from the loaded file's
    /// per-stage service column, matched by stage name. Null when no file is
    /// loaded or the file does not cover that stage.
    /// </summary>
    /// <remarks>
    /// Same lookup as <c>SimulationCoordinator</c> reads the fitted rates from, so
    /// the stage that is auto-fitted and the stage whose rate the coordinator
    /// resolves are guaranteed to be the same stage.
    /// </remarks>
    /// <param name="row">The stage whose samples are wanted.</param>
    private IReadOnlyList<double>? StageServiceSamples(StageRow row) =>
        Binding?.ServiceMinutesByStage.TryGetValue(row.StageName, out var times) == true
            ? times
            : null;

    /// <summary>
    /// Re-derives each stage row's read-only service-rate label and its μ-field
    /// visibility after any change that could affect them: the Parameters comma
    /// list, the loaded data, a mode switch or a stage-list resize (5d.1, D-112,
    /// 7C.5).
    /// </summary>
    private void RefreshStageSourceLabels()
    {
        var manualParts = ManualMuParts();
        for (int i = 0; i < StageRows.Count; i++)
        {
            var row = StageRows[i];
            row.ServiceRateLabel = BuildServiceRateLabel(row, i, manualParts);
            // The editable per-stage μ is always available in manual mode; in
            // fit mode it appears only for a stage the data did not cover (7C.5).
            row.IsMuVisible = IsManualMuEditable || !(FittedRateFor(row.StageName) is > 0);
            // Pushed per row rather than bound from the panel, because the extracted
            // StageRowControl cannot resolve a typed $parent[ItemsControl] binding.
            row.IsRateSourceVisible = IsCommaListMuVisible;
        }
    }

    /// <summary>
    /// "μ = 0.25 (from data)" for a fitted stage, "μ = 0.80 (manual)" for a
    /// comma-list entry, "μ = 0.60 (per-stage)" for a fallback field, or
    /// "μ = — (no source)". In fit mode a fitted rate wins over the comma-list
    /// and per-stage overrides (7C.5).
    /// </summary>
    private string BuildServiceRateLabel(StageRow row, int index, IReadOnlyList<double?> manualParts)
    {
        // Checked before the mode branches: a μ the G/G/c auto-fit wrote into this row
        // is the most specific description of it, and the coordinator resolves it as a
        // per-stage value, so this is the number the run will actually use. The
        // remaining branches are untouched, so a row that was never auto-fitted keeps
        // exactly the label it had.
        if (row.IsMuFittedLocally && double.TryParse(row.MuValue, out var fittedMu) && fittedMu > 0)
        {
            return $"μ = {fittedMu:0.###} (fitted)";
        }

        if (SourceMode == DataSourceMode.EnterManually)
        {
            return double.TryParse(row.MuValue, out var perStage) && perStage > 0
                ? $"μ = {(IsMeanWise ? 1.0 / perStage : perStage):0.##} (per-stage)"
                : "μ = — (no source)";
        }

        if (FittedRateFor(row.StageName) is { } fitted && fitted > 0)
        {
            return $"μ = {fitted:0.##} (from data)";
        }

        if (index < manualParts.Count && manualParts[index] is { } manual)
        {
            double effective = IsMeanWise && manual > 0 ? 1.0 / manual : manual;
            return $"μ = {effective:0.##} (manual)";
        }

        if (double.TryParse(row.MuValue, out var fallback) && fallback > 0)
        {
            return $"μ = {(IsMeanWise ? 1.0 / fallback : fallback):0.##} (per-stage)";
        }

        return "μ = — (no source)";
    }

    /// <summary>
    /// The manual comma-list μ per stage in index order (null for entries that
    /// are blank or unparsable). Empty in manual mode — the list is hidden and
    /// the per-stage fields are authoritative (7C.5) — and while Parameters is
    /// OFF, because OFF means "not supplied" (D-103).
    /// </summary>
    private IReadOnlyList<double?> ManualMuParts()
    {
        if (SourceMode != DataSourceMode.FitFromData
            || !ParametersIsOptionalEnabled
            || string.IsNullOrWhiteSpace(ManualMuPerStage.Value))
        {
            return Array.Empty<double?>();
        }

        return ManualMuPerStage.Value.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Select(part => double.TryParse(part, out var rate) && rate > 0 ? rate : (double?)null)
            .ToArray();
    }

    /// <summary>The data-fitted service rate for a stage name, or null.</summary>
    private double? FittedRateFor(string stageName)
    {
        var binding = Binding;
        if (binding is null)
        {
            return null;
        }

        int index = -1;
        for (int i = 0; i < binding.StageNames.Count; i++)
        {
            if (string.Equals(binding.StageNames[i], stageName, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index >= binding.FittedServiceRates.Count)
        {
            return null;
        }

        double rate = binding.FittedServiceRates[index];
        return double.IsFinite(rate) && rate > 0 ? rate : null;
    }

    /// <summary>
    /// The effective μ in the entered unit for display/gating (mean-wise is
    /// inverted). Manual mode uses the per-stage field; fit mode prefers a
    /// fitted rate, then the comma list, then the per-stage fallback (7C.5).
    /// </summary>
    private double? EffectiveMu(StageRow row, int index, IReadOnlyList<double?> manualParts)
    {
        if (SourceMode == DataSourceMode.EnterManually)
        {
            return double.TryParse(row.MuValue, out var perStage) && perStage > 0
                ? (IsMeanWise ? 1.0 / perStage : perStage)
                : null;
        }

        if (FittedRateFor(row.StageName) is { } fitted && fitted > 0)
        {
            return fitted;
        }

        if (index < manualParts.Count && manualParts[index] is { } manual)
        {
            return IsMeanWise && manual > 0 ? 1.0 / manual : manual;
        }

        return double.TryParse(row.MuValue, out var fallback) && fallback > 0
            ? (IsMeanWise ? 1.0 / fallback : fallback)
            : null;
    }

    /// <summary>
    /// Re-derives <see cref="StartIsEnabled"/> from every field's error state
    /// AND the current mode's completeness rules (D-128, 7C.6). The Start button
    /// stays enabled only while the configuration is runnable: in FitFromData
    /// mode that means a usable file and a resolvable μ for every stage; in
    /// EnterManually mode a valid λ, every per-stage μ, a server count and a
    /// valid p_exit. Fields of an optional section that is switched OFF do
    /// **not** participate — OFF means "not supplied" (D-103). The same rule
    /// applies to fields hidden by the current run mode (D-105, 4-c.1).
    /// <see cref="StartBlockedMessage"/> names exactly what is missing.
    /// </summary>
    private void RecomputeBlockingState()
    {
        var advancedInUse = AdvancedIsOptionalEnabled;

        var blocked = StageCount.HasError
            || SignificanceLevel.HasError
            || (IsMultiDay && Days.HasError)
            // The cap is visible in BOTH calendar modes (D-190), so an invalid cap
            // blocks a single-day run too — previously it only blocked multi-day,
            // which let a single-day run start with a cap the engine cannot apply.
            || (DailyCapVisible && DailyCap.HasError)
            || (IsDiagnosticTrace && IsCustomMinutesVisible && CustomMinutes.HasError)
            || StageRows.Any(row => row.HasErrors)
            || (ParametersIsOptionalEnabled && (ManualLambda.HasError || ManualMuPerStage.HasError || PExit.HasError))
            || (advancedInUse && Seed.HasError);

        var missing = new List<string>();
        if (!blocked)
        {
            CollectServerGaps(missing);
            if (SourceMode == DataSourceMode.EnterManually)
            {
                CollectManualModeGaps(missing);
            }
            else
            {
                CollectFitModeGaps(missing);
            }
        }

        if (missing.Count > 0)
        {
            blocked = true;
        }

        StartIsEnabled = !blocked;
        StartBlockedMessage = missing.Count == 0
            ? ""
            : "Cannot start: " + string.Join(", ", missing) + ".";
        StartCalculationCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Adds a message for every stage whose server count is not an integer ≥ 1 (defence in depth on top of the blur error).</summary>
    private void CollectServerGaps(List<string> missing)
    {
        foreach (var row in StageRows)
        {
            if (!int.TryParse(row.Servers.Value, out var servers) || servers < 1)
            {
                missing.Add($"{row.StageName} needs at least one server");
            }
        }
    }

    /// <summary>
    /// Completeness gaps for EnterManually mode (7C.6): λ, every per-stage μ and
    /// p_exit (for 2+ stages). Each gap is named with the offending field.
    /// </summary>
    private void CollectManualModeGaps(List<string> missing)
    {
        if (string.IsNullOrWhiteSpace(ManualLambda.Value))
        {
            missing.Add("λ (arrival rate) is missing");
        }
        else if (!(double.TryParse(ManualLambda.Value, out var lambda) && lambda > 0))
        {
            missing.Add("λ (arrival rate) must be a positive number");
        }

        foreach (var row in StageRows)
        {
            if (string.IsNullOrWhiteSpace(row.MuValue))
            {
                missing.Add($"{row.StageName} has no μ");
            }
            else if (!(double.TryParse(row.MuValue, out var mu) && mu > 0))
            {
                missing.Add($"{row.StageName} has an invalid μ");
            }
        }

        if (PExitVisible)
        {
            if (string.IsNullOrWhiteSpace(PExit.Value))
            {
                missing.Add("p_exit is missing");
            }
            else if (!(double.TryParse(PExit.Value, out var pExit) && pExit >= 0 && pExit < 1))
            {
                missing.Add("p_exit must be less than 1");
            }
        }
    }

    /// <summary>
    /// Completeness gaps for FitFromData mode (7C.6): a usable file and a
    /// resolvable μ for every configured stage (fitted or overridden).
    /// </summary>
    private void CollectFitModeGaps(List<string> missing)
    {
        if (Binding is not { IsUsable: true })
        {
            missing.Add("upload a usable data file");
            return;
        }

        var manualParts = ManualMuParts();
        for (int i = 0; i < StageRows.Count; i++)
        {
            if (EffectiveMu(StageRows[i], i, manualParts) is not { } mu || mu <= 0)
            {
                missing.Add($"{StageRows[i].StageName} has no μ");
            }
        }
    }

    /// <summary>
    /// Snaps the current validated fields into a <see cref="SimulationParameters"/>
    /// the coordinator can run. Manual values are mode-converted (rate vs
    /// mean-wise); a blank manual value becomes null so the coordinator falls
    /// back to the fitted values (D-104).
    /// </summary>
    /// <returns>The parameters, or null when a required field cannot be parsed (defence in depth — Start is already gated).</returns>
    public SimulationParameters? TryBuildRunParameters()
    {
        if (!TryStageCount(out var stageCount) || stageCount is < 1 or > 5)
        {
            return null;
        }

        // The diagnostic duration dropdown owns the arrival window (D-174 ruling 6);
        // the free-text field backs only its Custom option. The calendar run modes
        // do not read it AT ALL — they take their length from GeneratorDays — so
        // a malformed hidden field must not refuse a run that never looks at it.
        // Resolving it unconditionally would have done exactly that: a user who
        // typed a bad custom value, switched to "Multi-day", and forgot the field
        // was still there would find Start silently refusing, with the offending
        // control scrolled out of sight.
        int horizonMinutes = IsDiagnosticTrace ? ResolveDiagnosticMinutes() : DefaultDiagnosticMinutes;
        if (horizonMinutes < 1)
        {
            return null;
        }

        var mode = ParameterMode;
        // Rate-wise values convert straight to per-minute; mean-wise values are
        // inverted (rate = 1 / mean) BEFORE the unit conversion. Both paths
        // then land in the engine's native minutes (D-125).
        double? Convert(double? value) => value is { } v
            ? ToPerMinute(mode == ParameterMode.MeanWise ? 1.0 / v : v, TimeUnit)
            : null;
        double? ParsePositive(ConfigFieldViewModel field) =>
            !ParametersSupplied || string.IsNullOrWhiteSpace(field.Value) || !double.TryParse(field.Value, out var v) || !(v > 0)
                ? null
                : v;
        double? ParseRawPositive(string? value) =>
            double.TryParse(value, out var v) && v > 0 ? v : null;

        // FitFromData: the comma list is the manual μ override for stages the
        // data did not cover (a fitted rate wins over it — 7C.5). EnterManually:
        // the list is hidden and each row's own μ field is authoritative.
        double?[] commaRates = ManualMuParts()
            .Select(rate => rate is { } r ? Convert(r) : null)
            .ToArray();

        double? manualLambda = Convert(ParsePositive(ManualLambda));

        var names = new List<string>(stageCount);
        var serverCounts = new List<int>(stageCount);
        var manualRates = new List<double?>(stageCount);

        // Phase 8J: the per-stage service family and the μ it belongs to, built here
        // from the same `rate` value so the two can never disagree. `ServiceFamilies`
        // carries the family; `serviceRates` carries μ as entered (null = not entered,
        // so the coordinator fits it). The spec's Mean is filled from the same μ for
        // the same reason — it is never recovered by inverting a Mean, because that
        // round trip is not bit-reversible (D-146 caveat 1, D-147).
        var serviceFamilies = new List<DistributionSpec>(stageCount);
        var serviceRates = new List<double?>(stageCount);
        int rowIndex = 0;
        foreach (var row in StageRows)
        {
            names.Add(row.StageName);
            if (!int.TryParse(row.Servers.Value, out var servers) || servers < 1)
            {
                return null;
            }

            serverCounts.Add(servers);

            // A null entry tells the coordinator to use the fitted rate. In fit
            // mode a fitted rate therefore wins (7C.5); only uncovered stages
            // fall through to the comma list, then to the per-stage field.
            //
            // `rate` is what the COORDINATOR resolves against, and `specRate` is what
            // the spec is BUILT from. They differ in exactly one case — a stage the
            // file covers, where the fitted rate is deliberately left null above so
            // the coordinator keeps 7C.5's precedence. The MEAN, though, is not
            // unknown in that case (D-159, B-011 case 1): the fit already knows it, so
            // the spec is built from it and its mean-dependent fields come out
            // complete. Building from null instead left Gamma's `Scale = mean/k` and
            // Uniform's `Min`/`Max = mean ∓ w` as NaN, and the sampler factory then
            // threw on a configuration the Start gate had every reason to accept.
            double? rate;
            double? specRate;
            if (SourceMode == DataSourceMode.EnterManually)
            {
                rate = specRate = Convert(ParseRawPositive(row.MuValue));
            }
            else if (FittedRateFor(row.StageName) is > 0 and var fitted)
            {
                rate = null;
                specRate = fitted;
            }
            else
            {
                rate = specRate = rowIndex < commaRates.Length && commaRates[rowIndex] is { } comma
                    ? comma
                    : Convert(ParseRawPositive(row.MuValue));
            }

            manualRates.Add(rate);
            serviceRates.Add(rate);

            // Phase 8K (D-150): build the spec from the family and the SAME resolved μ
            // the coordinator will use, so Mean and ServiceRate cannot disagree.
            //
            // Cleared here, before the checks, so fixing the field clears the error on
            // the very next attempt (FR-UI-17) rather than needing a separate reset.
            row.InlineError = string.Empty;

            // B-011 case 2: no μ from any of the three sources, for a family that cannot
            // be built without one. Refuse HERE, naming the stage and the field, instead
            // of building a NaN-mean spec and letting the coordinator discover it — the
            // refusal that eventually surfaced ("requires Scale > 0", from inside Core)
            // named a spread field the user never touched.
            //
            // SCOPE, and it is deliberate: this fires for Gamma and Uniform ONLY. Those
            // two encode the mean a SECOND time — `Scale = mean/k` and
            // `Min`/`Max = mean ∓ w` — so an unknown mean leaves them NaN and the
            // sampler throws on a configuration the Start gate had every reason to
            // accept. Exponential, Deterministic, Normal and Lognormal need no mean at
            // build time: their spread is either absent or a mean-independent σ, and a
            // blank μ for them still means "fit me at run time" (D-104/7C.5) — the Path A
            // workflow §19.1 requires, where a user uploads a file and types no
            // parameters. Refusing every family here broke exactly that (9 tests), so
            // the check is scoped to the families where the failure was reachable.
            // "finite positive", not merely "> 0": double.TryParse happily accepts
            // "Infinity" and "1e400" (the latter overflows to +∞), so a μ that LOOKS
            // present would otherwise pass this test and build a spec with mean 0.
            // FittedRateFor already rejects non-finite rates (line 1388); this line is
            // what keeps the comma list and the row field to the same standard.
            // "finite positive", not merely "> 0": double.TryParse accepts "1e400" and
            // returns +infinity, so a mu that LOOKS present would otherwise pass this
            // test. In rate-wise mode nothing downstream catches it -- the spec is built
            // with mean 0, no error is raised, and the simulation quietly produces
            // meaningless output. That is worse than a refusal, so it is refused.
            // FittedRateFor already rejects non-finite rates; this line holds the
            // comma list and the row field to the same standard.
            // "finite positive", not merely "> 0": double.TryParse accepts "1e400" and
            // returns +infinity, so a mu that LOOKS present would otherwise pass this
            // test. In rate-wise mode nothing downstream catches it -- the spec is built
            // with mean 0, no error is raised, and the simulation quietly produces
            // meaningless output. That is worse than a refusal, so it is refused.
            // FittedRateFor already rejects non-finite rates; this line holds the
            // comma list and the row field to the same standard.
            bool muResolved = specRate is { } resolvedMu
                && double.IsFinite(resolvedMu)
                && resolvedMu > 0;
            if (!muResolved
                && row.ServiceFamily is DistributionFamily.Gamma or DistributionFamily.Uniform)
            {
                row.InlineError =
                    $"No service rate for {row.StageName}: enter μ on the row, in the " +
                    $"per-stage μ list, or load a data file that covers {row.StageName}. " +
                    $"A {row.ServiceFamily} stage needs a mean service time before its " +
                    "spread can be applied.";
                return null;
            }

            var spec = BuildSpec(row, specRate);
            if (spec is null)
            {
                // BuildSpec has written the reason on the row. Refusing the run here is
                // what makes the message reachable: the alternative is the sampler
                // factory throwing an opaque "requires StdDev > 0" from inside Core.
                return null;
            }

            serviceFamilies.Add(spec);
            rowIndex++;
        }

        double? pExitOverride = ParametersSupplied && !string.IsNullOrWhiteSpace(PExit.Value)
            && double.TryParse(PExit.Value, out var pExit) && pExit >= 0 && pExit < 1
                ? pExit
                : null;

        // Same shape as p_exit. Blank means "not overridden", which lets the
        // coordinator fall back to the value fitted from the file.
        double? pBypassOverride = ParametersSupplied && !string.IsNullOrWhiteSpace(PBypass.Value)
            && double.TryParse(PBypass.Value, out var pBypass) && pBypass >= 0 && pBypass < 1
                ? pBypass
                : null;

        var runMode = ActiveRunMode;
        int generatorDays = runMode == RunMode.MultiDay && int.TryParse(Days.Value, out var days)
            ? days
            : runMode == RunMode.MultiDay ? 0 : 1;
        if (runMode == RunMode.MultiDay && generatorDays < 1)
        {
            return null;
        }

        int? dailyCap = int.TryParse(DailyCap.Value, out var cap) && cap >= 1 ? cap : null;

        _ = Enum.TryParse<DayOfWeek>(StartDay, ignoreCase: true, out var startDay);

        // Phase 8K (D-150, Ruling 3): arrivals are NOT per stage. Core samples
        // inter-arrivals from one engine-wide exponential process, so the Model
        // section's dropdown stays authoritative and each row's ArrivalFamily is
        // informational only. Previously this read StageRows[0].ArrivalFamily, which
        // made the first stage silently govern every other stage.
        return new SimulationParameters(
            mode,
            InterArrivalDistribution ?? "Exponential",
            manualLambda,
            names,
            serverCounts,
            manualRates,
            runMode,
            horizonMinutes,
            generatorDays,
            startDay,
            dailyCap,
            EffectiveSeed,
            pExitOverride,
            EffectiveTraceLevel)
        {
            PBypassOverride = pBypassOverride,
            ServiceFamilies = serviceFamilies,
            ServiceRates = serviceRates,
            LambdaSource = SelectedLambdaSource(),
            WindowLambdaOverride = WindowLambdaAccessor?.Invoke(),
            // Read through the same delegate in the same object-initializer, so the
            // window and the λ divided by it come from one state (D-173).
            SelectedWindow = SelectedWindowAccessor?.Invoke(),
        };
    }

    /// <summary>
    /// Reads the user's MLE-vs-window λ choice (D-173, ruling 7).
    /// </summary>
    /// <remarks>
    /// A delegate, not a stored field, so <see cref="MainViewModel"/> stays the only
    /// place the value lives — the config panel reads the live value at build time
    /// and keeps no copy that could fall out of step with it. Unset (the unit
    /// tests construct this view model standalone) falls back to MLE, which is the
    /// default the record already carries.
    /// </remarks>
    /// <returns>The chosen estimator, or <see cref="LambdaSource.Mle"/> if unwired.</returns>
    private LambdaSource SelectedLambdaSource() => LambdaSourceAccessor?.Invoke() ?? LambdaSource.Mle;

    /// <summary>
    /// Reads the Input tab's window λ for the window the user selected, so the
    /// run uses the same figure the Input tab displays (D-172, D-173).
    /// </summary>
    /// <remarks>
    /// A delegate for the same reason as <see cref="LambdaSourceAccessor"/>: the
    /// Input tab owns the window selection and re-derives this number whenever
    /// the dropdown changes, so a stored copy here would be a second answer to a
    /// question that already has one. Unset (standalone unit tests, and the CLI)
    /// yields null, and the coordinator falls back to the binding's auto-detected
    /// window λ.
    /// </remarks>
    public Func<double?>? WindowLambdaAccessor { get; set; }

    /// <summary>
    /// Reads the window the selected-window λ was divided by (D-173).
    /// </summary>
    /// <remarks>
    /// A delegate for the same reason as <see cref="WindowLambdaAccessor"/>, and
    /// the reason it is a SEPARATE delegate rather than a stored copy is the
    /// receipt: the calculations dialog has to name the divisor of the λ that
    /// ran, and a window captured at a different moment from the λ could name a
    /// different one. Unset yields null and the receipt falls back to the
    /// file's own observed window.
    /// </remarks>
    public Func<ObservationWindow?>? SelectedWindowAccessor { get; set; }

    /// <summary>
    /// Builds the <see cref="DistributionSpec"/> for one stage from its row's family
    /// and the mode-converted μ that will also be handed to the engine as
    /// <c>StageSpec.ServiceRate</c> (Phase 8K, D-150).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The architecture is that <b>μ is the only location parameter</b>: the user types
    /// one mean service time and picks a family, and the family's own parameters control
    /// <i>spread only</i>. That is the fix for the disagreement that made an earlier
    /// draft impossible — a row where μ and the raw parameters were both entered could
    /// describe two different distributions at once.
    /// </para>
    /// <para>
    /// Consequently every family satisfies one invariant:
    /// <c>ServiceRate == 1 / Mean == μ</c>. The D-147 debug check in
    /// <c>StageSpec</c> tests exactly that, so it holds globally with no scoping and no
    /// Core change. The sampler's own mean agrees too: Gamma draws from
    /// <c>k · Scale = k · (Mean / k) = Mean</c>, and Uniform from
    /// <c>((Mean − w) + (Mean + w)) / 2 = Mean</c>.
    /// </para>
    /// <para>
    /// When μ is null the mean is genuinely unknown — the coordinator will fit it from
    /// data — so the spec is built with a NaN mean, exactly as Phase 8J did, and the
    /// spread fields are still carried through for the coordinator to use.
    /// </para>
    /// </remarks>
    /// <param name="row">The stage row, holding the family and the spread text.</param>
    /// <param name="rate">Mode-converted μ per minute, or null when not entered.</param>
    /// <returns>
    /// The spec, or null when the family needs a spread parameter that is missing or
    /// invalid. Null is a refusal, not a default: the caller stops the run so the user
    /// gets the reason on the row instead of an exception from the sampler factory.
    /// </returns>
    internal static DistributionSpec? BuildSpec(StageRow row, double? rate)
    {
        var family = row.ServiceFamily;
        double mean = rate is > 0 ? 1.0 / rate.Value : double.NaN;

        switch (family)
        {
            case DistributionFamily.Exponential:
            case DistributionFamily.Deterministic:
                return new DistributionSpec(family, mean);

            case DistributionFamily.Normal:
            case DistributionFamily.Lognormal:
                if (!TryPositive(row.ServiceStdDev, out var stdDev))
                {
                    row.InlineError =
                        $"{family} needs a standard deviation greater than 0. " +
                        $"You entered \"{row.ServiceStdDev ?? "blank"}\".";
                    return null;
                }

                return new DistributionSpec(family, mean, StdDev: stdDev);

            case DistributionFamily.Gamma:
                if (!TryPositive(row.ServiceShape, out var shape))
                {
                    row.InlineError =
                        "Gamma needs a shape k greater than 0. " +
                        $"You entered \"{row.ServiceShape ?? "blank"}\".";
                    return null;
                }

                // Scale is derived, never entered: it is the one value that makes the
                // sampler's mean (k·Scale) equal the mean the user asked for with μ.
                return new DistributionSpec(family, mean, Shape: shape, Scale: mean / shape);

            case DistributionFamily.Uniform:
                if (!TryPositive(row.ServiceSpread, out var halfWidth))
                {
                    row.InlineError =
                        "Uniform needs a half-width w greater than 0. " +
                        $"You entered \"{row.ServiceSpread ?? "blank"}\".";
                    return null;
                }

                // A half-width at or beyond the mean drives the derived lower bound to
                // zero or below, and a negative service time is not a service time.
                // DistributionSpec only requires Min < Max, so nothing downstream would
                // catch it — the run would quietly sample negative durations and every
                // wait time derived from them would be wrong. Refuse it here, where the
                // mean is known; the blur check cannot do this, because it would have to
                // re-derive the mean from the row's own μ and risk disagreeing with this.
                if (halfWidth >= mean)
                {
                    row.InlineError =
                        $"Uniform needs a half-width w less than the mean service time " +
                        $"({mean:0.####} min), or Min would be 0 or negative. " +
                        $"You entered \"{row.ServiceSpread ?? "blank"}\".";
                    return null;
                }

                // Bounds derived from mean ± half-width, so a symmetric spread about the
                // user's μ is guaranteed rather than typed twice and able to disagree.
                return new DistributionSpec(family, mean, Min: mean - halfWidth, Max: mean + halfWidth);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(row), family, "Unsupported service distribution family.");
        }
    }

    /// <summary>
    /// Parses a spread parameter the user typed, accepting only a strictly positive
    /// number. Blank and malformed both fail: the sampler factory rejects a zero or
    /// negative shape, scale or half-width, and a negative half-width would also make
    /// the derived Min exceed the derived Max.
    /// </summary>
    /// <param name="text">The raw field text.</param>
    /// <param name="value">The parsed value when the text is a positive number.</param>
    /// <returns>True when <paramref name="value"/> was set.</returns>
    private static bool TryPositive(string? text, out double value)
    {
        if (double.TryParse(text, out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }

        value = 0.0;
        return false;
    }

    /// <summary>
    /// Converts a user-entered rate or inverted mean to the engine's native
    /// per-minute scale (D-125): seconds × 60, hours ÷ 60, minutes unchanged.
    /// </summary>
    private static double ToPerMinute(double value, TimeUnit unit) =>
        unit switch
        {
            TimeUnit.Minutes => value,
            TimeUnit.Seconds => value * 60.0,
            TimeUnit.Hours => value / 60.0,
            _ => throw new ArgumentOutOfRangeException(nameof(unit)),
        };

    private static bool AllPartsPositive(string value) =>
        value.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .All(part => double.TryParse(part, out var rate) && rate > 0);
}

/// <summary>
/// Arrival-window presets for the Horizon section's DIAGNOSTIC mode (D-174).
/// </summary>
/// <remarks>
/// This type was <c>TimeSpanPreset</c>, and its day-or-longer members drove a
/// calendar run's generator-day count. That coupling is withdrawn: a calendar
/// run's length is the <c>Days</c> field and nothing else, so a preset that can
/// only be expressed in minutes has nothing to say about it. The rename is the
/// point — the old name is a claim about scope that the type no longer has.
/// </remarks>
public enum DiagnosticDurationPreset
{
    /// <summary>60 simulated minutes. The default (D-174 ruling 6).</summary>
    OneHour,
    /// <summary>15 simulated minutes — long enough to see a few patients clear the network.</summary>
    FifteenMinutes,
    /// <summary>A user-entered number of arrival-window minutes.</summary>
    CustomMinutes,
}
