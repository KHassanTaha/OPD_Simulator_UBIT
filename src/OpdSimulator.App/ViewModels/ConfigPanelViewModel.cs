using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.Data.Loaders;
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
        ManualMuPerStage.ValueChanged += (_, _) => RecomputeRho();
        StageCount.ValueChanged += (_, _) => OnStageCountEdited();
        EnsureStageCount(DefaultStages);
    }

    private const int DefaultStages = 3;

    /// <summary>Upload button: raises <see cref="UploadRequested"/> (the view resolves the file path).</summary>
    public event EventHandler? UploadRequested;

    /// <summary>Clear-All button: raises <see cref="ClearAllRequested"/> (the view confirms via ThemedDialog first).</summary>
    public event EventHandler? ClearAllRequested;

    /// <summary>Runs the configured simulation (Phase 5 wires the engine).</summary>
    [RelayCommand(CanExecute = nameof(CanStartCalculation))]
    private void StartCalculation()
    {
    }

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
    }

    partial void OnIsMeanWiseChanged(bool value)
    {
        if (value)
        {
            IsRateWise = false;
        }
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

    /// <summary>Number of clinic days (visible only in multi-day mode).</summary>
    public ConfigFieldViewModel Days { get; } = new();

    /// <summary>Optional daily patient cap; blank = unlimited.</summary>
    public ConfigFieldViewModel DailyCap { get; } = new();

    /// <summary>First day of a multi-day run (clinic week, CONTEXT §1.1).</summary>
    [ObservableProperty]
    private string? _startDay = "Monday";

    partial void OnIsMultiDayChanged(bool value)
    {
        if (value)
        {
            IsSingleDay = false;
            ValidateDays();
        }
        else
        {
            Days.ClearError();
        }

        RecomputeBlockingState();
    }

    partial void OnIsSingleDayChanged(bool value)
    {
        if (value)
        {
            IsMultiDay = false;
            RecomputeBlockingState();
        }
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

    /// <summary>True only when the manual parameter overrides are switched on.</summary>
    public bool ParametersSupplied => ParametersIsOptionalEnabled;

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
    /// Applies a loaded data file: the view resolves the picker path, this
    /// method loads through the Data subsystem and reports the row count.
    /// </summary>
    /// <param name="filePath">Absolute path to an .xlsx or .csv patient file.</param>
    public void ApplyLoadedFile(string filePath)
    {
        try
        {
            var data = new DataLoaderFactory().Create(filePath).Load(filePath);
            LoadedFileName = Path.GetFileName(filePath);
            DataStatus = $"Loaded {data.RowCount} rows from {LoadedFileName}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load uploaded data file {Path}", filePath);
            LoadedFileName = null;
            DataStatus = $"Could not load file: {Path.GetFileName(filePath)}";
        }
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
        InterArrivalDistribution = "Exponential";
        ServiceDistribution = "Exponential";
        IsRateWise = true;
        IsMeanWise = false;
        ManualLambda.Value = "";
        ManualMuPerStage.Value = "";
        PExit.Value = "";
        ParametersIsOptionalEnabled = false;
        AdvancedIsOptionalEnabled = false;
        Seed.Value = "42";
        TraceLevel = "State";
        IsMultiDay = false;
        IsSingleDay = true;
        StartDay = "Monday";
        Days.Value = "1";
        DailyCap.Value = "";

        foreach (var field in AllFieldErrors())
        {
            field.ClearError();
        }

        StageRows.Clear();
        EnsureStageCount(DefaultStages);
        StageCount.Value = DefaultStages.ToString();
        RecomputeRho();
        RecomputeBlockingState();
    }

    private IEnumerable<ConfigFieldViewModel> AllFieldErrors()
    {
        yield return ManualLambda;
        yield return ManualMuPerStage;
        yield return PExit;
        yield return StageCount;
        yield return Days;
        yield return DailyCap;
        yield return Seed;
        foreach (var row in StageRows)
        {
            yield return row.Servers;
            yield return row.ServiceRate;
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
            row.ServiceRate.ValueChanged += (_, _) => RecomputeRho();
            StageRows.Add(row);
        }

        while (StageRows.Count > count)
        {
            StageRows.RemoveAt(StageRows.Count - 1);
        }

        OnPropertyChanged(nameof(PExitVisible));
        RecomputeRho();
        RecomputeBlockingState();
    }

    /// <summary>
    /// Live utilisation per stage: ρ = λ / (c·μ). λ is the manual override
    /// when present (the fitted λ arrives in Phase 5); a missing piece shows
    /// as "—" for that stage.
    /// </summary>
    private void RecomputeRho()
    {
        // λ is only known while the optional Parameters section is switched on.
        bool lambdaKnown = false;
        double lambda = double.NaN;
        if (ParametersIsOptionalEnabled && double.TryParse(ManualLambda.Value, out lambda) && lambda > 0)
        {
            lambdaKnown = true;
        }
        var parts = StageRows.Select(row =>
        {
            if (lambdaKnown
                && double.TryParse(row.ServiceRate.Value, out var mu) && mu > 0
                && int.TryParse(row.Servers.Value, out var servers) && servers >= 1)
            {
                var rho = lambda / (mu * servers);
                return $"{row.StageName}: {rho:0.00}";
            }

            return $"{row.StageName}: —";
        });

        RhoSummary = parts.Any() ? string.Join("   ", parts) : "—";
    }

    /// <summary>
    /// Re-derives <see cref="StartIsEnabled"/> from every field's error state.
    /// The Start button stays enabled only while the configuration is runnable.
    /// Fields of an optional section that is switched OFF do **not** participate
    /// in this computation — OFF means "not supplied" (D-103). Turning a section
    /// back ON does not pre-flag anything; fields re-validate on the next blur.
    /// </summary>
    private void RecomputeBlockingState()
    {
        var parametersInUse = ParametersIsOptionalEnabled;
        var advancedInUse = AdvancedIsOptionalEnabled;

        var blocked = StageCount.HasError
            || (IsMultiDay && Days.HasError)
            || DailyCap.HasError
            || StageRows.Any(row => row.HasErrors)
            || (parametersInUse && (ManualLambda.HasError || ManualMuPerStage.HasError || PExit.HasError))
            || (advancedInUse && Seed.HasError);

        StartIsEnabled = !blocked;
        StartCalculationCommand.NotifyCanExecuteChanged();
    }

    private static bool AllPartsPositive(string value) =>
        value.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .All(part => double.TryParse(part, out var rate) && rate > 0);
}

/// <summary>
/// One configurable simulation stage row (Section 4). A dedicated nested
/// view-model (not a bare tuple) so each row owns its validated Servers and
/// Service-rate fields and its display name.
/// </summary>
public partial class StageRow : ObservableObject
{
    [ObservableProperty]
    private string _stageName = "";

    /// <summary>Servers-count input (integer ≥ 1; default 1).</summary>
    public ConfigFieldViewModel Servers { get; } = new() { Value = "1" };

    /// <summary>
    /// Per-server service-rate override (double &gt; 0; blank is valid and
    /// falls back to the fitted value in Phase 5).
    /// </summary>
    public ConfigFieldViewModel ServiceRate { get; } = new();

    /// <summary>True while either of this row's fields is invalid.</summary>
    public bool HasErrors => Servers.HasError || ServiceRate.HasError;

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

    /// <summary>
    /// Blur validation for the service rate: blank uses the fitted value;
    /// otherwise it must be a positive number.
    /// </summary>
    public void ValidateServiceRate()
    {
        var value = ServiceRate.Value;
        if (string.IsNullOrWhiteSpace(value) || (double.TryParse(value, out var mu) && mu > 0))
        {
            ServiceRate.ClearError();
        }
        else
        {
            ServiceRate.SetError($"Service rate must be a positive number. You entered \"{value}\".");
        }
    }
}