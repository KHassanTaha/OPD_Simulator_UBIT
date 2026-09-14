namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Data;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Parameters;
using OpdSimulator.Data.Validation;
using HorizonModeEnum = OpdSimulator.App.Models.HorizonMode;

/// <summary>
/// Left-panel configuration state (M5-D/F/G/J): every field, its validation,
/// the uploaded-data binding, and the preset surface. All raw field values are
/// text so the exact user input round-trips presets losslessly; conversion and
/// validation happen only when <see cref="TryBuildParameters"/> runs.
/// </summary>
public partial class ConfigViewModel : ViewModelBase
{
    /// <summary>Display option values for the parameter interpretation dropdown.</summary>
    public const string RateLabel = "Rate-wise (λ, μ per minute)";
    private const string MeanLabel = "Mean-wise (mean minutes)";
    private const string MinutesLabel = "Minutes";
    private const string DaysLabel = "Days";

    /// <summary>Firing ordering of fields for focus-first-invalid navigation (FR-UI-17).</summary>
    private static readonly string[] FocusOrder =
    {
        "arrival", "receptionServers", "screeningServers", "doctorServers",
        "receptionRate", "screeningRate", "doctorRate", "horizon",
        "dailyCap", "seed", "pExit",
    };

    private static readonly IReadOnlyList<string> ModeOptions = new[] { RateLabel, MeanLabel };
    private static readonly IReadOnlyList<string> HorizonOptions = new[] { MinutesLabel, DaysLabel };
    private static readonly IReadOnlyList<string> DayOptions =
        new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };

    private readonly PresetStore _presetStore;

    /// <summary>Events when the view should focus the first invalid field.</summary>
    public event Action<string>? FocusFieldRequested;

    /// <summary>Events to request the OS file picker; the view provides the implementation.</summary>
    public Func<Task<string?>>? PickFileRequested { get; set; }

    /// <summary>Creates the config view model with factory defaults (fields empty per FR-UI-21).</summary>
    public ConfigViewModel()
        : this(new PresetStore())
    {
    }

    /// <summary>Creates the config view model with an explicit preset store (tests).</summary>
    public ConfigViewModel(PresetStore presetStore)
    {
        _presetStore = presetStore ?? throw new ArgumentNullException(nameof(presetStore));

        _mode = RateLabel;
        _interArrivalDistribution = "Exponential";
        _serviceDistribution = "Exponential";
        _horizonMode = DaysLabel;
        _startDay = "Monday";

        Arrival = new ConfigFieldViewModel("arrival");
        ReceptionServers = new ConfigFieldViewModel("receptionServers", "1");
        ScreeningServers = new ConfigFieldViewModel("screeningServers", "2");
        DoctorServers = new ConfigFieldViewModel("doctorServers", "3");
        ReceptionRate = new ConfigFieldViewModel("receptionRate", "10");
        ScreeningRate = new ConfigFieldViewModel("screeningRate", "4");
        DoctorRate = new ConfigFieldViewModel("doctorRate", "1.6");
        Horizon = new ConfigFieldViewModel("horizon", "3");
        DailyCap = new ConfigFieldViewModel("dailyCap");
        Seed = new ConfigFieldViewModel("seed", "42");
        PExitOverride = new ConfigFieldViewModel("pExit");

        RefreshPresetNames();
    }

    // ---- Parameter interpretation / distributions -------------------------

    /// <summary>Gets or sets the rate-vs-mean interpretation.</summary>
    [ObservableProperty]
    private string _mode;

    /// <summary>Gets or sets the inter-arrival fitting family.</summary>
    [ObservableProperty]
    private string _interArrivalDistribution;

    /// <summary>Gets or sets the service fitting family.</summary>
    [ObservableProperty]
    private string _serviceDistribution;

    // ---- Numeric fields ---------------------------------------------------

    /// <summary>Arrival parameter (λ per minute or mean minutes, per mode).</summary>
    public ConfigFieldViewModel Arrival { get; }

    /// <summary>Reception server count.</summary>
    public ConfigFieldViewModel ReceptionServers { get; }

    /// <summary>Screening server count.</summary>
    public ConfigFieldViewModel ScreeningServers { get; }

    /// <summary>Doctor server count.</summary>
    public ConfigFieldViewModel DoctorServers { get; }

    /// <summary>Reception service parameter (μ per minute or mean minutes).</summary>
    public ConfigFieldViewModel ReceptionRate { get; }

    /// <summary>Screening service parameter.</summary>
    public ConfigFieldViewModel ScreeningRate { get; }

    /// <summary>Doctor service parameter.</summary>
    public ConfigFieldViewModel DoctorRate { get; }

    /// <summary>Time-horizon value (minutes or days, per <see cref="HorizonMode"/>).</summary>
    public ConfigFieldViewModel Horizon { get; }

    /// <summary>Daily admissions cap (empty = unlimited).</summary>
    public ConfigFieldViewModel DailyCap { get; }

    /// <summary>Random seed.</summary>
    public ConfigFieldViewModel Seed { get; }

    /// <summary>Post-Screening exit probability override (empty = derived).</summary>
    public ConfigFieldViewModel PExitOverride { get; }

    // ---- Horizon / trace / presets -----------------------------------------

    /// <summary>Gets or sets the horizon interpretation (minutes or days).</summary>
    [ObservableProperty]
    private string _horizonMode;

    /// <summary>Gets or sets the weekday of day block 0.</summary>
    [ObservableProperty]
    private string _startDay;

    /// <summary>Gets or sets the trace detail level ("None", "Events", "State", "Rng").</summary>
    [ObservableProperty]
    private string _traceLevel = "State";

    /// <summary>Gets the option lists the dropdowns bind to.</summary>
    public IReadOnlyList<string> ModeOptionsList => ModeOptions;
    public IReadOnlyList<string> HorizonOptionsList => HorizonOptions;
    public IReadOnlyList<string> DayOptionsList => DayOptions;
    public IReadOnlyList<string> TraceLevelOptionsList => new[] { "None", "Events", "State", "Rng" };

    /// <summary>Gets or sets whether the Data section is expanded.</summary>
    [ObservableProperty]
    private bool _dataSectionExpanded = true;

    /// <summary>Gets or sets whether the Arrival &amp; service section is expanded.</summary>
    [ObservableProperty]
    private bool _parametersSectionExpanded = true;

    /// <summary>Gets or sets whether the Servers section is expanded.</summary>
    [ObservableProperty]
    private bool _serversSectionExpanded = true;

    /// <summary>Gets or sets whether the Horizon section is expanded.</summary>
    [ObservableProperty]
    private bool _horizonSectionExpanded = true;

    /// <summary>Gets or sets whether the Advanced section is expanded.</summary>
    [ObservableProperty]
    private bool _advancedSectionExpanded = true;

    /// <summary>Restores the collapsed-section state persisted across sessions (AGENTS §16.11).</summary>
    public void ApplyCollapsedSections(IEnumerable<string> collapsedKeys)
    {
        var collapsed = collapsedKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        DataSectionExpanded = !collapsed.Contains("data");
        ParametersSectionExpanded = !collapsed.Contains("parameters");
        ServersSectionExpanded = !collapsed.Contains("servers");
        HorizonSectionExpanded = !collapsed.Contains("horizon");
        AdvancedSectionExpanded = !collapsed.Contains("advanced");
    }

    /// <summary>Gets the names of the sections that are currently collapsed.</summary>
    public IReadOnlyList<string> CollapsedSectionKeys
    {
        get
        {
            var keys = new List<string>();
            if (!DataSectionExpanded) keys.Add("data");
            if (!ParametersSectionExpanded) keys.Add("parameters");
            if (!ServersSectionExpanded) keys.Add("servers");
            if (!HorizonSectionExpanded) keys.Add("horizon");
            if (!AdvancedSectionExpanded) keys.Add("advanced");
            return keys;
        }
    }

    /// <summary>Gets the supported fitting families (FR-STAT-2/GUI).</summary>
    public IReadOnlyList<string> DistributionOptions
        => OpdSimulator.Data.Fitting.DistributionFitterFactory.SupportedNames;

    // ---- Labels (mode-dependent) ------------------------------------------

    /// <summary>Gets whether the mode is rate-wise; drives the field labels.</summary>
    public bool IsRateMode => Mode == RateLabel;

    /// <summary>Gets the arrival-field label for the current mode.</summary>
    public string ArrivalLabel => IsRateMode ? "Arrival rate λ (per minute)" : "Mean inter-arrival time (minutes)";

    /// <summary>Gets the stage service-field label for the current mode.</summary>
    public string ServiceLabel => IsRateMode ? "Service rate μ (per minute)" : "Mean service time (minutes)";

    /// <summary>Gets the horizon-field label for the current mode.</summary>
    public string HorizonLabel => HorizonMode == DaysLabel ? "Number of clinic days" : "Arrival window (minutes)";

    /// <summary>Gets whether daily-cap/start-day fields are relevant (days mode).</summary>
    public bool IsDaysMode => HorizonMode == DaysLabel;

    // ---- Data file binding --------------------------------------------------

    /// <summary>Gets or sets the full path of the uploaded data file.</summary>
    [ObservableProperty]
    private string? _dataFilePath;

    /// <summary>Gets whether a data file is currently loaded (drives the Clear/Preview visibility).</summary>
    public bool HasDataFile => DataFilePath is not null;

    /// <summary>Gets the outcome of analysing the uploaded file (fitted params + issues).</summary>
    public DataBindingResult? Binding { get; private set; }

    /// <summary>Gets the preview rows shown behind validation (FR-UI-20).</summary>
    public DataPreviewStore Preview { get; } = new();

    /// <summary>Gets a short human summary of the loaded file for the upload row.</summary>
    [ObservableProperty]
    private string _fileSummary = "No file loaded.";

    /// <summary>Gets whether the data file row is currently showing an error.</summary>
    [ObservableProperty]
    private bool _fileHasError;

    // ---- Presets ------------------------------------------------------------

    /// <summary>Gets the preset names currently on disk (sorted).</summary>
    public ObservableCollection<string> PresetNames { get; } = new();

    /// <summary>Gets or sets the preset selected from the dropdown (null = none).</summary>
    [ObservableProperty]
    private string? _selectedPresetName;

    /// <summary>Gets whether the preset controls are meaningful (a selection exists).</summary>
    public bool HasPresetSelection => SelectedPresetName is not null;

    /// <summary>Gets the preset store backing this config.</summary>
    public PresetStore PresetStore => _presetStore;

    partial void OnModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsRateMode));
        OnPropertyChanged(nameof(ArrivalLabel));
        OnPropertyChanged(nameof(ServiceLabel));
    }

    partial void OnHorizonModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDaysMode));
        OnPropertyChanged(nameof(HorizonLabel));
    }

    partial void OnSelectedPresetNameChanged(string? value) => OnPropertyChanged(nameof(HasPresetSelection));

    partial void OnDataFilePathChanged(string? value) => OnPropertyChanged(nameof(HasDataFile));

    /// <summary>
    /// Uploads a data file: analyses it, populates the arrival/service fields
    /// from the fitted values, and prepares the validation preview.
    /// </summary>
    /// <returns>True when the file was usable; false when it failed validation
    /// or could not be read (the file row shows the reason, FR-UI-9).</returns>
    public bool LoadData(string path)
    {
        var binding = DataAnalyzer.Analyze(path);
        Binding = binding;
        DataFilePath = path;

        Preview.SetData(
            binding.DataSet?.Columns ?? Array.Empty<string>(),
            binding.DataSet is null
                ? Enumerable.Empty<DataPreviewRow>()
                : BuildPreviewRows(binding.DataSet, binding.Issues, binding.DataSet.Columns));

        if (!binding.IsUsable)
        {
            FileHasError = true;
            FileSummary = binding.ErrorMessage
                ?? (binding.Issues.Count == 1
                    ? "The file has 1 issue: " + binding.Issues[0].Reason
                    : $"The file has {binding.Issues.Count} issues. Hover the flagged rows in the preview for details.");
            return false;
        }

        FileHasError = false;

        // Populate fitted parameters; manual overrides the user types later win.
        if (binding.FittedArrivalRate is { } lambda)
        {
            Arrival.Value = FormatParameter(lambda, IsRateMode);
        }
        foreach (var pair in binding.StageNames.Zip(binding.FittedServiceRates))
        {
            FieldFor(pair.First).Value = FormatParameter(pair.Second, IsRateMode);
        }

        FileSummary = BuildFileSummary(binding);
        return true;
    }

    /// <summary>Clears the uploaded file and resets the fitted fields.</summary>
    public void ClearData()
    {
        Binding = null;
        DataFilePath = null;
        FileHasError = false;
        FileSummary = "No file loaded.";
        Preview.SetData(Array.Empty<string>(), Enumerable.Empty<DataPreviewRow>());
    }

    /// <summary>
    /// Converts the raw fields into runnable <see cref="SimulationParameters"/>.
    /// On failure every offending field is flagged and <paramref name="issues"/>
    /// receives human messages; the caller focuses the first invalid field.
    /// </summary>
    public bool TryBuildParameters(out SimulationParameters? parameters, out IReadOnlyList<string> issues)
    {
        var errors = new List<string>();
        var mode = IsRateMode ? ParameterMode.RateWise : ParameterMode.MeanWise;

        double arrival = TryPositive(Arrival, "arrival", errors, mode);
        double muR = TryPositive(ReceptionRate, "reception service", errors, mode);
        double muS = TryPositive(ScreeningRate, "screening service", errors, mode);
        double muD = TryPositive(DoctorRate, "doctor service", errors, mode);

        int cR = TryServerCount(ReceptionServers, errors);
        int cS = TryServerCount(ScreeningServers, errors);
        int cD = TryServerCount(DoctorServers, errors);

        double horizonMinutes = 0;
        int generatorDays = 0;
        if (HorizonMode == DaysLabel)
        {
            generatorDays = TryDays(Horizon, errors);
        }
        else
        {
            horizonMinutes = TryMinutes(Horizon, errors);
        }

        int? dailyCap = TryOptionalInt(DailyCap, "daily cap", errors, 1, null);
        int seed = TrySeed(Seed, errors);
        double? pExit = TryOptionalDouble(PExitOverride, "p_exit override", errors, 0.0, 1.0);

        if (errors.Count > 0)
        {
            parameters = null;
            issues = errors;
            RequestFocusOnFirstInvalid();
            return false;
        }

        parameters = new SimulationParameters(
            mode,
            InterArrivalDistribution,
            ServiceDistribution,
            IsRateMode ? arrival : 1.0 / arrival,
            SimulationCoordinator.FlowStageNames,
            new[] { cR, cS, cD },
            new[]
            {
                IsRateMode ? muR : 1.0 / muR,
                IsRateMode ? muS : 1.0 / muS,
                IsRateMode ? muD : 1.0 / muD,
            },
            HorizonMode == DaysLabel ? HorizonModeEnum.Days : HorizonModeEnum.Minutes,
            horizonMinutes,
            generatorDays,
            ParseDay(StartDay),
            dailyCap,
            seed,
            pExit,
            TraceLevel);

        issues = Array.Empty<string>();
        return true;
    }

    /// <summary>
    /// Reports the mode-driven stability warnings for a built parameter set
    /// (soft — the user may still want to run an unstable queue).
    /// </summary>
    /// <returns>Warning lines per stage, empty when everything is steady-state safe.</returns>
    public static IReadOnlyList<string> ComputeWarnings(SimulationParameters p)
    {
        var warnings = new List<string>();
        double lambdaScreening = p.ArrivalRate;
        double lambdaDoctor = p.ArrivalRate * (1.0 - (p.PExitOverride ?? 0.0));

        foreach (var (name, lambda, c, mu) in new[]
                 {
                     (p.StageNames[0], p.ArrivalRate, p.ServerCounts[0], p.ServiceRates[0]),
                     (p.StageNames[1], lambdaScreening, p.ServerCounts[1], p.ServiceRates[1]),
                     (p.StageNames[2], lambdaDoctor, p.ServerCounts[2], p.ServiceRates[2]),
                 })
        {
            double rho = lambda / (c * mu);
            if (rho >= 1.0)
            {
                warnings.Add($"{name}: ρ = {rho:0.###} ≥ 1 — the queue never reaches steady state.");
            }
        }

        return warnings;
    }

    /// <summary>Requests the OS file picker through the view and uploads the result.</summary>
    [RelayCommand]
    public async Task PickDataFileAsync()
    {
        if (PickFileRequested is null)
        {
            return;
        }

        string? path = await PickFileRequested();
        if (path is not null)
        {
            LoadData(path);
        }
    }

    /// <summary>Clears every field and the loaded file (empty startup, FR-UI-21).</summary>
    [RelayCommand]
    public void ResetFields()
    {
        Mode = RateLabel;
        InterArrivalDistribution = "Exponential";
        ServiceDistribution = "Exponential";
        HorizonMode = DaysLabel;
        StartDay = "Monday";
        TraceLevel = "State";
        Seed.Value = "42";
        ClearData();

        Arrival.Reset();
        ReceptionServers.Reset(defaultValue: "1");
        ScreeningServers.Reset(defaultValue: "2");
        DoctorServers.Reset(defaultValue: "3");
        ReceptionRate.Reset(defaultValue: "10");
        ScreeningRate.Reset(defaultValue: "4");
        DoctorRate.Reset(defaultValue: "1.6");
        Horizon.Reset(defaultValue: "3");
        DailyCap.Reset();
        Seed.Reset(defaultValue: "42");
        PExitOverride.Reset();

        RaiseAllLabels();
        RefreshPresetNames();
    }

    // ---- Preset operations -------------------------------------------------

    /// <summary>Saves the current fields as a preset under the given name.</summary>
    /// <returns>True on success; false when the name is invalid or a collision needs handling.</returns>
    public bool SavePreset(string name, IReadOnlyList<string> visibleWidgets, IReadOnlyList<string> collapsedSections)
    {
        string sanitised = PresetNaming.Sanitize(name);
        if (sanitised.Length == 0)
        {
            return false;
        }

        var preset = BuildPreset(sanitised, visibleWidgets, collapsedSections);
        _presetStore.Save(preset);
        RefreshPresetNames();
        SelectedPresetName = sanitised;
        return true;
    }

    /// <summary>Loads the stored preset into the fields (if its data file is gone, flags "reselect data").</summary>
    /// <returns>True when the preset was found and applied.</returns>
    public bool ApplyPreset(string name)
    {
        Preset preset;
        try
        {
            preset = _presetStore.Load(name);
        }
        catch (PresetException)
        {
            RefreshPresetNames();
            return false;
        }

        var c = preset.Config;
        if (c is null)
        {
            return false;
        }

        Mode = c.ParameterMode?.Equals("mean", StringComparison.OrdinalIgnoreCase) == true ? MeanLabel : RateLabel;
        InterArrivalDistribution = string.IsNullOrEmpty(c.InterArrivalDistribution) ? "Exponential" : c.InterArrivalDistribution;
        ServiceDistribution = string.IsNullOrEmpty(c.ServiceDistribution) ? "Exponential" : c.ServiceDistribution;
        Arrival.Value = c.ArrivalParameter ?? string.Empty;
        ReceptionServers.Value = c.Servers?.Reception?.ToString() ?? "1";
        ScreeningServers.Value = c.Servers?.Screening?.ToString() ?? "2";
        DoctorServers.Value = c.Servers?.Doctor?.ToString() ?? "3";
        ReceptionRate.Value = c.ServiceRates?.Reception ?? "10";
        ScreeningRate.Value = c.ServiceRates?.Screening ?? "4";
        DoctorRate.Value = c.ServiceRates?.Doctor ?? "1.6";
        HorizonMode = c.Horizon?.Mode?.Equals("days", StringComparison.OrdinalIgnoreCase) == true ? DaysLabel : MinutesLabel;
        Horizon.Value = c.Horizon?.Value ?? "3";
        StartDay = ParseDayName(c.StartDay);
        DailyCap.Value = c.DailyCap ?? string.Empty;
        Seed.Value = c.RandomSeed ?? "42";
        PExitOverride.Value = c.PExitOverride ?? string.Empty;
        TraceLevel = c.TraceLevel ?? "State";

        RaiseAllLabels();
        SelectedPresetName = preset.Name;

        if (string.IsNullOrEmpty(preset.DataFile))
        {
            ClearData();
        }
        else if (File.Exists(preset.DataFile))
        {
            LoadData(preset.DataFile);
        }
        else
        {
            // Populate every other field, but show the "reselect data" inline
            // error (AGENTS §17.2 requires this, never a silent skip).
            ClearData();
            DataFilePath = preset.DataFile;
            FileHasError = true;
            FileSummary = "The data file this preset refers to is missing. Reselect the data file to continue.";
        }

        ApplyCollapsedSections(preset.View?.CollapsedSections ?? Array.Empty<string>());
        return true;
    }

    /// <summary>Deletes the named preset (with confirmation handled by the caller).</summary>
    public void DeletePreset(string name)
    {
        _presetStore.Delete(name);
        RefreshPresetNames();
        if (SelectedPresetName == name)
        {
            SelectedPresetName = null;
        }
    }

    /// <summary>Reloads the preset dropdown from disk.</summary>
    public void RefreshPresetNames()
    {
        string? selected = SelectedPresetName;
        PresetNames.Clear();
        foreach (var name in _presetStore.List())
        {
            PresetNames.Add(name);
        }
        if (selected is not null && PresetNames.Contains(selected))
        {
            SelectedPresetName = selected;
        }
    }

    /// <summary>Rebuilds a preset document from the current fields.</summary>
    public Preset BuildPreset(string name, IReadOnlyList<string> visibleWidgets, IReadOnlyList<string> collapsedSections)
    {
        return new Preset
        {
            SchemaVersion = Preset.CurrentSchemaVersion,
            Name = name,
            CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
            Config = new PresetConfig
            {
                ParameterMode = IsRateMode ? "rate" : "mean",
                InterArrivalDistribution = InterArrivalDistribution,
                ServiceDistribution = ServiceDistribution,
                ArrivalParameter = Arrival.Value,
                Servers = new PresetStageNumbers
                {
                    Reception = ParseIntOrNull(ReceptionServers.Value),
                    Screening = ParseIntOrNull(ScreeningServers.Value),
                    Doctor = ParseIntOrNull(DoctorServers.Value),
                },
                ServiceRates = new PresetStageRates
                {
                    Reception = ReceptionRate.Value,
                    Screening = ScreeningRate.Value,
                    Doctor = DoctorRate.Value,
                },
                Horizon = new PresetHorizon
                {
                    Mode = IsDaysMode ? "days" : "minutes",
                    Value = Horizon.Value,
                },
                StartDay = StartDay,
                DailyCap = string.IsNullOrEmpty(DailyCap.Value) ? null : DailyCap.Value,
                RandomSeed = Seed.Value,
                PExitOverride = string.IsNullOrEmpty(PExitOverride.Value) ? null : PExitOverride.Value,
                TraceLevel = TraceLevel,
            },
            DataFile = DataFilePath,
            View = new PresetView
            {
                VisibleWidgets = visibleWidgets.ToArray(),
                CollapsedSections = collapsedSections.ToArray(),
            },
        };
    }

    // ---- Validation helpers --------------------------------------------------

    private double TryPositive(ConfigFieldViewModel field, string what, List<string> errors, ParameterMode mode)
    {
        if (!TryDouble(field.Value, out double value))
        {
            field.SetError(WhatFor(what, mode) + " must be a positive number. You entered '" + field.Value + "'.");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        if (value <= 0)
        {
            field.SetError(WhatFor(what, mode) + " must be greater than 0. You entered " + Format(value) + ".");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        return value;
    }

    private int TryServerCount(ConfigFieldViewModel field, List<string> errors)
    {
        if (!int.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 1)
        {
            field.SetError("Server count must be a whole number ≥ 1 (a clinic always has at least one server).");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        return count;
    }

    private double TryMinutes(ConfigFieldViewModel field, List<string> errors)
    {
        if (!TryDouble(field.Value, out double minutes) || minutes <= 0)
        {
            field.SetError("The arrival window must be a positive number of minutes (e.g. 10000).");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        return minutes;
    }

    private int TryDays(ConfigFieldViewModel field, List<string> errors)
    {
        if (!int.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int days) || days < 1)
        {
            field.SetError("The number of clinic days must be a whole number ≥ 1 (e.g. 3).");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        return days;
    }

    private int? TryOptionalInt(ConfigFieldViewModel field, string what, List<string> errors, int min, int? max)
    {
        if (string.IsNullOrWhiteSpace(field.Value))
        {
            return null;
        }
        if (!int.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            || value < min || (max is { } m && value > m))
        {
            field.SetError($"{what} must be a whole number between {min} and {max?.ToString() ?? "unlimited"}. You entered '{field.Value}'.");
            errors.Add(field.ErrorMessage);
            return null;
        }
        return value;
    }

    private double? TryOptionalDouble(ConfigFieldViewModel field, string what, List<string> errors, double min, double max)
    {
        if (string.IsNullOrWhiteSpace(field.Value))
        {
            return null;
        }
        if (!TryDouble(field.Value, out double value) || value < min || value > max)
        {
            field.SetError($"{what} must be a number between {min:F0} and {max:F0} (a probability). You entered '{field.Value}'.");
            errors.Add(field.ErrorMessage);
            return null;
        }
        return value;
    }

    private int TrySeed(ConfigFieldViewModel field, List<string> errors)
    {
        if (!int.TryParse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int seed))
        {
            field.SetError("The random seed must be a whole number (e.g. 42). Reproducibility requires it.");
            errors.Add(field.ErrorMessage);
            return 0;
        }
        return seed;
    }

    private static string WhatFor(string what, ParameterMode mode)
        => mode == ParameterMode.RateWise
            ? what + " rate"
            : "Mean " + what + " time";

    private static bool TryDouble(string? text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private string FormatParameter(double value, bool rateMode)
    {
        double display = rateMode ? value : 1.0 / value;
        return display.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static int? ParseIntOrNull(string? text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : null;

    private static DayOfWeek ParseDay(string name)
        => Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var day) ? day : DayOfWeek.Monday;

    private static string ParseDayName(string? name)
        => Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var day) ? day.ToString() : "Monday";

    private void RaiseAllLabels()
    {
        OnPropertyChanged(nameof(IsRateMode));
        OnPropertyChanged(nameof(ArrivalLabel));
        OnPropertyChanged(nameof(ServiceLabel));
        OnPropertyChanged(nameof(IsDaysMode));
        OnPropertyChanged(nameof(HorizonLabel));
    }

    private ConfigFieldViewModel FieldFor(string stage)
        => stage.Equals("Reception", StringComparison.OrdinalIgnoreCase) ? ReceptionRate
            : stage.Equals("Screening", StringComparison.OrdinalIgnoreCase) ? ScreeningRate
            : DoctorRate;

    /// <summary>Raises <see cref="FocusFieldRequested"/> for the first invalid field in tab order (FR-UI-17).</summary>
    private void RequestFocusOnFirstInvalid()
    {
        foreach (var key in FocusOrder)
        {
            if (FieldByKey(key)?.HasError == true)
            {
                FocusFieldRequested?.Invoke(key);
                return;
            }
        }
    }

    private ConfigFieldViewModel? FieldByKey(string key)
        => key switch
        {
            "arrival" => Arrival,
            "receptionServers" => ReceptionServers,
            "screeningServers" => ScreeningServers,
            "doctorServers" => DoctorServers,
            "receptionRate" => ReceptionRate,
            "screeningRate" => ScreeningRate,
            "doctorRate" => DoctorRate,
            "horizon" => Horizon,
            "dailyCap" => DailyCap,
            "seed" => Seed,
            "pExit" => PExitOverride,
            _ => null,
        };

    private static string BuildFileSummary(DataBindingResult binding)
    {
        var parts = new List<string> { "File loaded and valid." };
        if (binding.FittedArrivalRate is { } lambda)
        {
            parts.Add($"fitted λ = {lambda:0.###}/min");
        }
        var rates = string.Join(", ", binding.StageNames.Select((n, i) =>
            $"{n} μ = {(binding.FittedServiceRates[i] is double r ? r : double.NaN):0.###}/min"));
        if (rates.Length > 0)
        {
            parts.Add(rates);
        }
        if (binding.FittedExitProbability is { } pExit)
        {
            parts.Add($"p_exit = {pExit:0.###}");
        }
        return string.Join("  |  ", parts);
    }

    private static IEnumerable<DataPreviewRow> BuildPreviewRows(
        DataSet dataSet, IReadOnlyList<ValidationIssue> issues, IReadOnlyList<string> columns)
    {
        var byRow = issues.Where(i => i.RowNumber >= 0)
            .GroupBy(i => i.RowNumber)
            .ToDictionary(g => g.Key, g => g.First().ToString());

        return dataSet.Rows.Select((row, index) =>
        {
            var cells = columns.Select(c => row.TryGetValue(c, out string? v) ? v ?? string.Empty : string.Empty).ToArray();
            return new DataPreviewRow
            {
                Cells = cells,
                SourceIndex = index + 1,
                IsInvalid = byRow.ContainsKey(index + 1),
                ValidationReason = byRow.TryGetValue(index + 1, out string? reason) ? reason : null,
            };
        });
    }
}