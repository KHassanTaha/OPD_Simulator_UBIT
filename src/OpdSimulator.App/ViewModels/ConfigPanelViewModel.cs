using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
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

    /// <summary>Day-of-week options for a multi-day run (clinic week, CONTEXT §1.1).</summary>
    public IReadOnlyList<string> StartDays { get; } =
        new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Saturday" };

    /// <summary>Trace detail levels offered by the advanced dropdown.</summary>
    public IReadOnlyList<string> TraceLevels { get; } =
        new[] { "None", "Events", "State", "Rng" };

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

    /// <summary>Selected service distribution (label; value maps in the VM).</summary>
    [ObservableProperty]
    private string? _serviceDistribution = "Exponential";

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
    /// Whether the p_exit override is shown. Per spec it appears only when 2+
    /// stages are configured (an early-exit route needs at least one downstream stage).
    /// </summary>
    public bool PExitVisible => TryStageCount(out var n) && n >= 2;

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

    // ── Time-span presets (Phase 7A) ──────────────────────────────────────

    /// <summary>Time-span labels offered by the Horizon dropdown, in dropdown order (D-125).</summary>
    public IReadOnlyList<string> TimeSpanOptions { get; } =
        new[] { "15 minutes", "1 hour", "1 day", "1 week", "1 month", "Custom days…" };

    /// <summary>Currently selected span preset (the label drives the dropdown via <see cref="TimeSpanSelection"/>).</summary>
    [ObservableProperty]
    private TimeSpanPreset timeSpan = TimeSpanPreset.OneDay;

    /// <summary>The dropdown's committed label, kept in sync with <see cref="TimeSpan"/> (string-based SearchableDropdown seam).</summary>
    [ObservableProperty]
    private string timeSpanSelection = "1 day";

    /// <summary>Whether the "Custom days" field is shown — only while that span is selected.</summary>
    [ObservableProperty]
    private bool isCustomDaysVisible;

    /// <summary>Custom generator-days input, visible only for <see cref="TimeSpanPreset.CustomDays"/>.</summary>
    [ObservableProperty]
    private ConfigFieldViewModel customDays = new();

    partial void OnTimeSpanSelectionChanged(string value)
    {
        var preset = ParseTimeSpanPreset(value);
        if (preset != TimeSpan)
        {
            TimeSpan = preset;
        }
    }

    partial void OnTimeSpanChanged(TimeSpanPreset value)
    {
        string label = TimeSpanLabel(value);
        if (!string.Equals(TimeSpanSelection, label, StringComparison.Ordinal))
        {
            TimeSpanSelection = label;
        }

        IsCustomDaysVisible = value == TimeSpanPreset.CustomDays;
        ApplyTimeSpanToRunMode();
        RecomputeBlockingState();
    }

    /// <summary>
    /// Resolves the span to a number of generator days, per D-125: the short
    /// presets still span one operating day (bounded by their minute horizon);
    /// "1 day" = one full operating day; "1 week" = 6 operating days
    /// (Mon/Tue/Wed/Thu/Sat/Mon within a 7-day span); "1 month" ≈ 26 operating
    /// days (30 × 5/7 ≈ 21.4 → 22, × 1.2 buffer → 26); Custom days = the
    /// validated field value (0 when it does not parse — a multi-day run then
    /// refuses at build time).
    /// </summary>
    public int ResolveGeneratorDays() =>
        TimeSpan switch
        {
            TimeSpanPreset.FifteenMinutes => 1,
            TimeSpanPreset.OneHour => 1,
            TimeSpanPreset.OneDay => 1,
            TimeSpanPreset.OneWeek => 6,
            TimeSpanPreset.OneMonth => 26,
            TimeSpanPreset.CustomDays => int.TryParse(CustomDays.Value, out var days) && days >= 1 ? days : 0,
            _ => 1,
        };

    /// <summary>
    /// Applies the selected span to the run-mode's driving field when the mode
    /// consumes it (D-125): multi-day takes GeneratorDays from the span;
    /// single-day stays one full calendar day (the short presets bound it via
    /// the effective horizon at build time instead). Diagnostic-trace mode is
    /// deliberately untouched — its horizon stays the user's field.
    /// </summary>
    private void ApplyTimeSpanToRunMode()
    {
        if (IsMultiDay)
        {
            Days.Value = ResolveGeneratorDays().ToString();
        }
    }

    private static string TimeSpanLabel(TimeSpanPreset preset) =>
        preset switch
        {
            TimeSpanPreset.FifteenMinutes => "15 minutes",
            TimeSpanPreset.OneHour => "1 hour",
            TimeSpanPreset.OneDay => "1 day",
            TimeSpanPreset.OneWeek => "1 week",
            TimeSpanPreset.OneMonth => "1 month",
            TimeSpanPreset.CustomDays => "Custom days…",
            _ => "1 day",
        };

    private static TimeSpanPreset ParseTimeSpanPreset(string label) =>
        label switch
        {
            "15 minutes" => TimeSpanPreset.FifteenMinutes,
            "1 hour" => TimeSpanPreset.OneHour,
            "1 day" => TimeSpanPreset.OneDay,
            "1 week" => TimeSpanPreset.OneWeek,
            "1 month" => TimeSpanPreset.OneMonth,
            "Custom days…" => TimeSpanPreset.CustomDays,
            _ => TimeSpanPreset.OneDay,
        };

    /// <summary>Arrival-window minutes for a diagnostic run (int ≥ 1, default 10000; D-105).</summary>
    public ConfigFieldViewModel HorizonMinutes { get; } = new() { Value = "10000" };

    /// <summary>Number of clinic days (visible only in multi-day mode).</summary>
    public ConfigFieldViewModel Days { get; } = new();

    /// <summary>Optional daily patient cap; blank = unlimited. Visible only in multi-day mode.</summary>
    public ConfigFieldViewModel DailyCap { get; } = new();

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
        ApplyTimeSpanToRunMode();
        RecomputeBlockingState();
    }

    partial void OnIsSingleDayChanged(bool value)
    {
        if (value)
        {
            IsMultiDay = false;
            IsDiagnosticTrace = false;
            ApplyTimeSpanToRunMode();
            RecomputeBlockingState();
        }
    }

    partial void OnIsDiagnosticTraceChanged(bool value)
    {
        if (value)
        {
            IsSingleDay = false;
            IsMultiDay = false;
            ValidateHorizonMinutes();
        }
        else
        {
            HorizonMinutes.ClearError();
        }
        RecomputeBlockingState();
    }

    // ── Section 6 · Advanced ────────────────────────────────────────────

    /// <summary>Random seed (default 42) — deterministic runs.</summary>
    public ConfigFieldViewModel Seed { get; } = new();

    /// <summary>Selected trace level (default State).</summary>
    [ObservableProperty]
    private string? _traceLevel = "State";

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
    /// the random seed defaults to 42 and the trace level to State.
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

    /// <summary>Random seed the run will use (42 while the Advanced section is off).</summary>
    public int EffectiveSeed =>
        AdvancedIsOptionalEnabled && int.TryParse(Seed.Value, out var seed) ? seed : 42;

    /// <summary>Trace level the run will use (State while the Advanced section is off).</summary>
    public string EffectiveTraceLevel => AdvancedIsOptionalEnabled ? TraceLevel ?? "State" : "State";

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

    /// <summary>Blur validation for the diagnostic-run horizon minutes field (integer ≥ 1; D-105).</summary>
    public void ValidateHorizonMinutes()
    {
        var value = HorizonMinutes.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            HorizonMinutes.ClearError();
        }
        else
        {
            HorizonMinutes.SetError($"Horizon must be a whole number of minutes above 0. You entered \"{value}\".");
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

    /// <summary>Blur validation for the custom-days span field (integer &gt; 0; only shown while that span is selected).</summary>
    public void ValidateCustomDays()
    {
        var value = CustomDays.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            CustomDays.ClearError();
        }
        else
        {
            CustomDays.SetError($"Days must be a whole number of at least 1. You entered \"{value}\".");
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
        ServiceDistribution = "Exponential";
        SignificanceLevel.Value = "0.05";
        IsRateWise = true;
        IsMeanWise = false;
        ParameterMode = ParameterMode.RateWise;
        TimeUnit = TimeUnit.Minutes;
        ManualLambda.Value = "";
        ManualMuPerStage.Value = "";
        PExit.Value = "";
        IsStageMismatchWarningVisible = false;
        StageMismatchMessage = "";
        ParametersIsOptionalEnabled = false;
        AdvancedIsOptionalEnabled = false;
        Seed.Value = "42";
        TraceLevel = "State";
        IsMultiDay = false;
        IsDiagnosticTrace = false;
        IsSingleDay = true;
        StartDay = "Monday";
        TimeSpan = TimeSpanPreset.OneDay;
        CustomDays.Value = "";
        HorizonMinutes.Value = "10000";
        Days.Value = "1";
        DailyCap.Value = "";

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
        yield return HorizonMinutes;
        yield return CustomDays;
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
            var row = new StageRow { StageName = name };
            row.Servers.ValueChanged += (_, _) => RecomputeRho();
            // A manual per-stage μ edit changes both ρ and Start readiness (7C.4/7C.6).
            row.MuValueChanged += (_, _) =>
            {
                RecomputeRho();
                RecomputeBlockingState();
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
            || (IsMultiDay && (Days.HasError || DailyCap.HasError))
            || (IsDiagnosticTrace && HorizonMinutes.HasError)
            || (TimeSpan == TimeSpanPreset.CustomDays && CustomDays.HasError)
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

        // The short span presets bound the arrival window to 15 or 60 minutes
        // (D-125); every other span uses the user's horizon field. The calendar
        // run modes ignore this record field, so the override is harmless there.
        int horizonMinutes;
        if (TimeSpan is TimeSpanPreset.FifteenMinutes)
        {
            horizonMinutes = 15;
        }
        else if (TimeSpan is TimeSpanPreset.OneHour)
        {
            horizonMinutes = 60;
        }
        else if (!int.TryParse(HorizonMinutes.Value, out horizonMinutes) || horizonMinutes < 1)
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
            double? rate;
            if (SourceMode == DataSourceMode.EnterManually)
            {
                rate = Convert(ParseRawPositive(row.MuValue));
            }
            else if (FittedRateFor(row.StageName) is > 0)
            {
                rate = null;
            }
            else
            {
                rate = rowIndex < commaRates.Length && commaRates[rowIndex] is { } comma
                    ? comma
                    : Convert(ParseRawPositive(row.MuValue));
            }

            manualRates.Add(rate);
            rowIndex++;
        }

        double? pExitOverride = ParametersSupplied && !string.IsNullOrWhiteSpace(PExit.Value)
            && double.TryParse(PExit.Value, out var pExit) && pExit >= 0 && pExit < 1
                ? pExit
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

        // Phase 7B: the per-stage model notation is now the source of the
        // distribution families. Arrivals are external, so they take the
        // FIRST stage's arrival family; services take the first stage's
        // service family — SimulationParameters carries a single service
        // family, so per-stage service override is deferred (D-126).
        return new SimulationParameters(
            mode,
            StageRows[0].ArrivalFamily,
            StageRows[0].ServiceFamily,
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
            EffectiveTraceLevel);
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
/// One configurable simulation stage row (Section 4). A dedicated nested
/// view-model (not a bare tuple) so each row owns its validated Servers field,
/// its display name and its read-only service-rate source label. Since Phase
/// 5d (D-112) the stages section is topology ONLY — the editable service-rate
/// field was removed; the single manual μ entry lives in Parameters and the
/// label below reports where this stage's effective μ comes from.
/// </summary>
public partial class StageRow : ObservableObject
{
    [ObservableProperty]
    private string _stageName = "";

    /// <summary>Servers-count input (integer ≥ 1; default 1).</summary>
    public ConfigFieldViewModel Servers { get; } = new() { Value = "1" };

    /// <summary>
    /// Manual service rate μ for this stage, interpreted per the current
    /// Parameter mode and Time unit. Authoritative in EnterManually mode; in
    /// FitFromData mode it is the fallback shown only for a stage with no
    /// fitted rate (7C.4).
    /// </summary>
    [ObservableProperty]
    private string muValue = string.Empty;

    /// <summary>True while <see cref="MuValue"/> is non-blank and not a positive number.</summary>
    [ObservableProperty]
    private bool muHasError;

    /// <summary>Inline cause+remedy for an invalid <see cref="MuValue"/> (blank is valid — it is simply not entered yet).</summary>
    [ObservableProperty]
    private string muError = string.Empty;

    /// <summary>
    /// Whether this row's editable μ field is shown: always in manual mode, and
    /// in fit mode only when the loaded data has no fitted rate for this stage
    /// (7C.5). Set by <see cref="ConfigPanelViewModel.RefreshStageSourceLabels"/>.
    /// </summary>
    [ObservableProperty]
    private bool isMuVisible;

    /// <summary>Raised whenever <see cref="MuValue"/> changes so the parent VM can re-gate Start (7C.6).</summary>
    public event EventHandler? MuValueChanged;

    partial void OnMuValueChanged(string value)
    {
        // FR-UI-17: editing clears the stale error; blur re-validates.
        MuHasError = false;
        MuError = string.Empty;
        MuValueChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Blur validation for the manual per-stage μ: blank is valid (not entered); otherwise it must parse as a positive number.</summary>
    public void ValidateMu()
    {
        if (string.IsNullOrWhiteSpace(MuValue) || (double.TryParse(MuValue, out var mu) && mu > 0))
        {
            MuHasError = false;
            MuError = string.Empty;
        }
        else
        {
            MuHasError = true;
            MuError = $"μ must be a positive number. You entered \"{MuValue}\".";
        }
    }

    /// <summary>
    /// Read-only service-rate source for this stage: the fitted value with
    /// "(from data)", the Parameters manual entry with "(manual)", or
    /// "μ = — (no source)". Set by <see cref="ConfigPanelViewModel"/>
    /// whenever the source could change (5d.1).
    /// </summary>
    [ObservableProperty]
    private string _serviceRateLabel = "μ = — (no source)";

    /// <summary>
    /// Kendall-notation shortcut chosen for this stage (Phase 7B), e.g.
    /// "M/M/2". Ignored while <see cref="UseAdvancedSetup"/> is on.
    /// </summary>
    [ObservableProperty]
    private string _selectedModel = "M/M/1";

    /// <summary>
    /// When false the arrival/service families and the server count are derived
    /// from <see cref="SelectedModel"/>; when true the two family dropdowns are
    /// revealed and edited independently (Phase 7B).
    /// </summary>
    [ObservableProperty]
    private bool _useAdvancedSetup = false;

    /// <summary>Arrival distribution family ("Exponential", "Deterministic" or "General").</summary>
    [ObservableProperty]
    private string _arrivalFamily = "Exponential";

    /// <summary>Service distribution family ("Exponential", "Deterministic" or "General").</summary>
    [ObservableProperty]
    private string _serviceFamily = "Exponential";

    /// <summary>Kendall model shortcuts offered by the per-stage model dropdown.</summary>
    public IReadOnlyList<string> ModelOptions => ModelNotationParser.StandardModels;

    /// <summary>Distribution families offered when Advanced setup is on.</summary>
    public IReadOnlyList<string> DistributionFamilies { get; } =
        new[] { "Exponential", "Deterministic", "General" };

    /// <summary>
    /// Applies a model-notation shortcut to the row: arrival family, service
    /// family and server count. Skipped while Advanced setup owns those fields.
    /// </summary>
    partial void OnSelectedModelChanged(string value)
    {
        if (UseAdvancedSetup)
        {
            return;
        }

        var parsed = ModelNotationParser.Parse(value);
        ArrivalFamily = parsed.ArrivalFamily;
        ServiceFamily = parsed.ServiceFamily;
        Servers.Value = parsed.ServerCount.ToString();
    }

    /// <summary>True while this row's servers field is invalid.</summary>
    public bool HasErrors => Servers.HasError;

    /// <summary>Blur validation for the servers count: integer ≥ 1.</summary>
    public void ValidateServers()
    {
        var value = Servers.Value;
        if (int.TryParse(value, out var n) && n >= 1)
        {
            Servers.ClearError();
        }
        else
        {
            Servers.SetError($"Servers must be a whole number of at least 1. You entered \"{value}\".");
        }
    }
}

/// <summary>
/// Time-span presets for the Horizon section (Phase 7A, D-125). Short presets
/// bound a run to a minute horizon; day+ presets drive a calendar run's
/// generator-day count through <see cref="ConfigPanelViewModel.ResolveGeneratorDays"/>.
/// </summary>
public enum TimeSpanPreset
{
    FifteenMinutes,
    OneHour,
    OneDay,
    OneWeek,
    OneMonth,
    CustomDays,
}