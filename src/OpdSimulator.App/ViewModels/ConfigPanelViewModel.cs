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

    partial void OnIsRateWiseChanged(bool value)
    {
        if (value)
        {
            IsMeanWise = false;
        }

        RecomputeRho();
        RefreshStageSourceLabels();
    }

    partial void OnIsMeanWiseChanged(bool value)
    {
        if (value)
        {
            IsRateWise = false;
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

    /// <summary>When Parameters is OFF, its entries are "not supplied" — the µ from data only.</summary>
    public bool ParametersSupplied => ParametersIsOptionalEnabled;

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
        InterArrivalDistribution = "Exponential";
        ServiceDistribution = "Exponential";
        SignificanceLevel.Value = "0.05";
        IsRateWise = true;
        IsMeanWise = false;
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
    /// Live utilisation per stage: ρ = λ / (c·μ). λ and μ use the SAME sources
    /// the run will use (5d.1): the manual override when Parameters is on
    /// (blank falls back to the fitted value), else the fitted value from the
    /// loaded data; a missing piece shows as "—" for that stage.
    /// </summary>
    private void RecomputeRho()
    {
        double lambda = double.NaN;
        if (ParametersIsOptionalEnabled && double.TryParse(ManualLambda.Value, out var manualLambda) && manualLambda > 0)
        {
            lambda = manualLambda;
        }
        else if (Binding?.FittedArrivalRate is { } fitted && fitted > 0)
        {
            lambda = fitted;
        }

        bool lambdaKnown = lambda > 0;
        var manualParts = ManualMuParts();
        var parts = StageRows.Select((row, i) =>
        {
            double? mu = EffectiveMu(row.StageName, i, manualParts);
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
    /// Re-derives each stage row's read-only service-rate label after any
    /// change that could affect it: the Parameters comma list, the load of a
    /// data file, a mode switch or a stage-list resize (5d.1, D-112).
    /// </summary>
    private void RefreshStageSourceLabels()
    {
        var manualParts = ManualMuParts();
        for (int i = 0; i < StageRows.Count; i++)
        {
            StageRows[i].ServiceRateLabel = BuildServiceRateLabel(StageRows[i].StageName, i, manualParts);
        }
    }

    /// <summary>
    /// "μ = 0.80 (manual)" when the Parameters comma list has a value for this
    /// stage, "μ = 0.25 (from data)" when a fitted rate exists in the binding,
    /// or "μ = — (no source)" — the exact state that refuses a run with the
    /// per-stage banner (5d.1).
    /// </summary>
    private string BuildServiceRateLabel(string stageName, int index, IReadOnlyList<double?> manualParts)
    {
        if (index < manualParts.Count && manualParts[index] is { } manual)
        {
            double effective = IsMeanWise && manual > 0 ? 1.0 / manual : manual;
            return $"μ = {effective:0.##} (manual)";
        }

        double? fitted = FittedRateFor(stageName);
        return fitted is > 0
            ? $"μ = {fitted.Value:0.##} (from data)"
            : "μ = — (no source)";
    }

    /// <summary>
    /// The manual μ per stage in index order (null for entries that are blank
    /// or unparsable). Empty while Parameters is OFF — OFF means "not supplied",
    /// so no stage gets a manual source (D-103).
    /// </summary>
    private IReadOnlyList<double?> ManualMuParts()
    {
        if (!ParametersIsOptionalEnabled || string.IsNullOrWhiteSpace(ManualMuPerStage.Value))
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

    private double? EffectiveMu(string stageName, int index, IReadOnlyList<double?> manualParts)
    {
        if (index < manualParts.Count && manualParts[index] is { } manual)
        {
            return IsMeanWise && manual > 0 ? 1.0 / manual : manual;
        }

        return FittedRateFor(stageName);
    }

    /// <summary>
    /// Re-derives <see cref="StartIsEnabled"/> from every field's error state.
    /// The Start button stays enabled only while the configuration is runnable.
    /// Fields of an optional section that is switched OFF do **not** participate
    /// in this computation — OFF means "not supplied" (D-103). The same rule
    /// applies to fields hidden by the current run mode: Days / Daily-cap only
    /// count in multi-day mode, the horizon only in the diagnostic trace mode
    /// (D-105, 4-c.1).
    /// </summary>
    private void RecomputeBlockingState()
    {
        var parametersInUse = ParametersIsOptionalEnabled;
        var advancedInUse = AdvancedIsOptionalEnabled;

        var blocked = StageCount.HasError
            || SignificanceLevel.HasError
            || (IsMultiDay && (Days.HasError || DailyCap.HasError))
            || (IsDiagnosticTrace && HorizonMinutes.HasError)
            || StageRows.Any(row => row.HasErrors)
            || (parametersInUse && (ManualLambda.HasError || ManualMuPerStage.HasError || PExit.HasError))
            || (advancedInUse && Seed.HasError);

        StartIsEnabled = !blocked;
        StartCalculationCommand.NotifyCanExecuteChanged();
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

        if (!int.TryParse(HorizonMinutes.Value, out var horizonMinutes) || horizonMinutes < 1)
        {
            return null;
        }

        var mode = IsMeanWise ? ParameterMode.MeanWise : ParameterMode.RateWise;
        double? Convert(double? value) => value is { } v ? mode == ParameterMode.RateWise ? v : 1.0 / v : null;
        double? ParsePositive(ConfigFieldViewModel field) =>
            !ParametersSupplied || string.IsNullOrWhiteSpace(field.Value) || !double.TryParse(field.Value, out var v) || !(v > 0)
                ? null
                : v;

        // Manual μ has ONE source since Phase 5d-D-112: the Parameters comma
        // list, applied to the configured stages in order. A missing/blank
        // entry falls back to the fitted value inside the coordinator. (The
        // per-row μ input was removed in 5d.1 — stages are topology only.)
        double?[] commaRates = !ParametersSupplied || string.IsNullOrWhiteSpace(ManualMuPerStage.Value)
            ? Array.Empty<double?>()
            : ManualMuPerStage.Value.Split(',')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .Select(part => double.TryParse(part, out var rate) && rate > 0 ? Convert(rate) : null)
                .ToArray()!;

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
            manualRates.Add(rowIndex < commaRates.Length ? commaRates[rowIndex] : null);
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

        return new SimulationParameters(
            mode,
            InterArrivalDistribution ?? "Exponential",
            ServiceDistribution ?? "Exponential",
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
    /// Read-only service-rate source for this stage: the fitted value with
    /// "(from data)", the Parameters manual entry with "(manual)", or
    /// "μ = — (no source)". Set by <see cref="ConfigPanelViewModel"/>
    /// whenever the source could change (5d.1).
    /// </summary>
    [ObservableProperty]
    private string _serviceRateLabel = "μ = — (no source)";

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