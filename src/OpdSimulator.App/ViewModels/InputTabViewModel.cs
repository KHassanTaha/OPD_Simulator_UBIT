namespace OpdSimulator.App.ViewModels;

using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;

/// <summary>
/// Phase 7D: drives the merged Input tab — the single home for the uploaded
/// data file, its preview and validation banner, the stage-mismatch warning,
/// and the distribution-fit analysis. Before 7D these lived on the (deleted)
/// "Data" section of the Simulation tab and on the separate "Input Analysis"
/// tab; <see cref="Analysis"/> is the same instance the shell already owned so
/// the charts are not rebuilt twice.
/// </summary>
/// <remarks>
/// The tab owns no file-picker code and no fitting logic: it raises intent
/// events (<see cref="UploadFileRequested"/>, <see cref="UseForSimulationRequested"/>,
/// <see cref="StageMismatchSyncRequested"/>, <see cref="StageMismatchKeepRequested"/>,
/// <see cref="ClearFileRequested"/>) which <see cref="MainViewModel"/> routes
/// to the one load path and the config panel (RULING 3, Phase 7D).
/// </remarks>
public partial class InputTabViewModel : ObservableObject
{
    private readonly InputAnalysisViewModel _analysis;

    /// <summary>Creates the tab with its own fit-analysis view model (tests).</summary>
    public InputTabViewModel()
        : this(new InputAnalysisViewModel())
    {
    }

    /// <summary>Creates the tab sharing the shell's fit-analysis view model.</summary>
    /// <param name="analysis">The fit-analysis view model the shell already owns.</param>
    public InputTabViewModel(InputAnalysisViewModel analysis)
    {
        _analysis = analysis;

        // A dropdown change must re-derive the window λ and the divergence note,
        // and the tab's own changed-signal fires for exactly those edits. The
        // binding is remembered rather than re-passed: the shell re-supplies it
        // whenever the file changes.
        PropertyChanged += OnWindowSelectionChanged;
    }

    private DataBindingResult? _lastBinding;

    private void OnWindowSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectedObservationWindow)
            || e.PropertyName == nameof(CustomHours))
        {
            // The custom-hours row's visibility is bound to this, so the
            // generated setter has already raised it by the time we recompute
            // the window.
            OnPropertyChanged(nameof(IsCustomWindowSelected));
            ApplyWindowLambda(_lastBinding);
        }
    }

    /// <summary>The loaded-file preview and validation summary.</summary>
    public InputPreviewViewModel Preview { get; } = new();

    /// <summary>The distribution-fit charts for the loaded file (shared with the shell).</summary>
    public InputAnalysisViewModel Analysis => _analysis;

    /// <summary>True once a file has been loaded (valid or rejected).</summary>
    [ObservableProperty]
    private bool hasFile;

    /// <summary>
    /// The window the file's own session dates imply, chosen from the preset
    /// dropdown (D-173, ruling 3).
    /// </summary>
    [ObservableProperty]
    private ObservationWindowSelection selectedObservationWindow = ObservationWindowSelection.Auto;

    /// <summary>Custom window length in operating hours, for the Custom option (ruling 3).</summary>
    [ObservableProperty]
    private string customHours = "5.5";

    /// <summary>
    /// The window λ the run will actually use: arrivals ÷ the SELECTED window's
    /// operating minutes (rulings 3, 7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is the reason the panel and the engine cannot disagree. The
    /// number shown in <see cref="WindowLambdaText"/> and the number the
    /// coordinator receives are read from the same computation, so choosing
    /// "1 week" on a six-day file changes both together. Reading
    /// <c>DataBindingResult.WindowLambda</c> at run time instead would have left
    /// the auto-detected figure driving the simulation while the panel displayed
    /// the selected one — two answers to one question, which is the drift D-165
    /// ruled out for the calculations dialog.
    /// </para>
    /// <para>
    /// Null when no window λ can exist: no file, a file with fewer than two
    /// arrivals, every session on a closed day, or invalid custom hours.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    private double? selectedWindowLambda;

    /// <summary>
    /// The window <see cref="SelectedWindowLambda"/> was divided by, published
    /// beside it so a receipt can name the divisor (D-173, the receipt defect).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The λ alone is not a checkable number. "λ — window = 0.0303" is only
    /// verifiable if the reader also knows it came from 60 arrivals over 1980
    /// minutes; without the window, the calculations dialog would have to print
    /// the FILE's window beside the SELECTED window's λ, and dividing one by
    /// the other visibly fails to give the number printed. That is the same
    /// "two numbers, one on screen" defect the override exists to prevent, moved
    /// from the run path to the receipt.
    /// </para>
    /// <para>
    /// Published from the same local <c>window</c> that produces
    /// <see cref="SelectedWindowLambda"/>, and cleared at every point that
    /// clears it, so the two cannot disagree about which window was used.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    private ObservationWindow? selectedWindow;

    /// <summary>True when the custom-hours field cannot be used (FR-UI-17).</summary>
    [ObservableProperty]
    private bool isCustomWindowInvalid;

    /// <summary>Why the custom-hours value is unusable, stated in the field's terms.</summary>
    [ObservableProperty]
    private string customWindowErrorText = "";

    /// <summary>
    /// The window λ and its label, or the reason there is none (rulings 3, 4).
    /// </summary>
    [ObservableProperty]
    private string windowLambdaText = "";

    /// <summary>Whether a window λ exists to offer (rulings 4).</summary>
    [ObservableProperty]
    private bool hasWindowLambda;

    /// <summary>λ from the inter-arrival fit, or the reason there is none (ruling 2).</summary>
    [ObservableProperty]
    private string mleLambdaText = "";

    /// <summary>Whether an MLE λ exists to offer.</summary>
    [ObservableProperty]
    private bool hasMleLambda;

    /// <summary>
    /// How far the two estimates disagree, with the direction stated (ruling 2).
    /// </summary>
    [ObservableProperty]
    private string lambdaDivergenceText = "";

    /// <summary>
    /// True when Window is selected but unavailable, so the radio is disabled
    /// and the reason is visible rather than silent (ruling 4).
    /// </summary>
    [ObservableProperty]
    private bool isWindowChoiceDisabled;

    /// <summary>Dropdown labels for the observation window, in display order (D-172).</summary>
    public IReadOnlyList<string> WindowOptions => ObservationWindowService.SelectionOptions;

    /// <summary>
    /// The selected window's label, bound to the dropdown by text (D-172).
    /// </summary>
    /// <remarks>
    /// A string rather than the enum because the dropdown is populated from
    /// <see cref="WindowOptions"/>; parsing the label back through the service
    /// keeps the label strings defined in exactly one place. The parse is
    /// forgiving by design — an unrecognised label is Auto, the preset that
    /// needs no interpretation.
    /// </remarks>
    public string SelectedWindowLabel
    {
        get => ObservationWindowService.SelectionLabel(SelectedObservationWindow);
        set => SelectedObservationWindow = ObservationWindowService.ParseSelection(value);
    }

    /// <summary>True when the custom-hours row should be visible (ruling 3).</summary>
    public bool IsCustomWindowSelected => SelectedObservationWindow == ObservationWindowSelection.Custom;

    /// <summary>Reads the MLE-vs-window choice owned by the shell (ruling 7).</summary>
    public Func<LambdaSource>? LambdaSourceAccessor { get; set; }

    /// <summary>Writes the MLE-vs-window choice on the shell's behalf (ruling 7).</summary>
    public Action<LambdaSource>? LambdaSourceSetter { get; set; }

    /// <summary>
    /// Re-reads the shell's choice and refreshes both radios.
    /// </summary>
    /// <remarks>
    /// The two booleans below are computed from a shell-owned value, so nothing
    /// raises <c>PropertyChanged</c> for them when the shell changes it from
    /// anywhere other than these radio setters — a preset load, a reset, a test,
    /// or a path that sets <c>SelectedLambdaSource</c> directly. Without this,
    /// the radio could keep showing MLE while the run used the window rate, and
    /// the divergence note stays hidden because it is bound to the same flag.
    /// </remarks>
    public void RefreshLambdaSource()
    {
        // IsMleSelected's change is also what re-evaluates the divergence note's
        // IsVisible, since that is bound to `!IsMleSelected` — no need to re-raise
        // the text itself.
        OnPropertyChanged(nameof(IsMleSelected));
        OnPropertyChanged(nameof(IsWindowSelected));
        OnPropertyChanged(nameof(IsWindowChoiceDisabled));
    }

    /// <summary>True when the run should use the MLE estimate (binds the first radio).</summary>
    public bool IsMleSelected
    {
        get => LambdaSourceAccessor?.Invoke() != LambdaSource.Window;
        set
        {
            if (value)
            {
                LambdaSourceSetter?.Invoke(LambdaSource.Mle);
            }
        }
    }

    /// <summary>True when the run should use the window estimate (binds the second radio).</summary>
    public bool IsWindowSelected
    {
        get => LambdaSourceAccessor?.Invoke() == LambdaSource.Window;
        set
        {
            if (value)
            {
                LambdaSourceSetter?.Invoke(LambdaSource.Window);
            }
        }
    }

    /// <summary>One-line status under the upload buttons.</summary>
    [ObservableProperty]
    private string statusText = "No file loaded.";

    /// <summary>True while the configured stage list differs from the loaded data's stages.</summary>
    [ObservableProperty]
    private bool hasStageMismatch;

    /// <summary>The full mismatch explanation, shown in the tab's amber warning.</summary>
    [ObservableProperty]
    private string stageMismatchMessage = "";

    /// <summary>Raised when the user asks to open the file picker; the shell owns the one picker.</summary>
    public event EventHandler? UploadFileRequested;

    /// <summary>Raised when the user asks to run from this file; the shell sets FitFromData and shows the Simulation tab.</summary>
    public event EventHandler? UseForSimulationRequested;

    /// <summary>Raised when the user asks to replace the config's stages with the data's stages.</summary>
    public event EventHandler? StageMismatchSyncRequested;

    /// <summary>Raised when the user dismisses the mismatch warning without changing the stages.</summary>
    public event EventHandler? StageMismatchKeepRequested;

    /// <summary>Raised after the file is cleared so the shell can unload it from the config panel too.</summary>
    public event EventHandler? ClearFileRequested;

    /// <summary>Hosts the file picker: raises <see cref="UploadFileRequested"/> (the shell resolves the path).</summary>
    [RelayCommand]
    private void UploadFile() => UploadFileRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Unloads the current file and returns the tab to its empty state.</summary>
    [RelayCommand]
    private void ClearFile()
    {
        Clear();
        ClearFileRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks the shell to fit the run from this file and show the Simulation tab.</summary>
    [RelayCommand]
    private void UseForSimulation() => UseForSimulationRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the shell to replace the config's stage list with the data's stages.</summary>
    [RelayCommand]
    private void SyncStagesFromData() => StageMismatchSyncRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Dismisses the mismatch warning for this session (handled by the shell).</summary>
    [RelayCommand]
    private void KeepCurrentStages() => StageMismatchKeepRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Shows a loaded binding: populates the preview (rows, invalid-row map,
    /// validation banner) and moves the tab out of its empty state. The fit
    /// analysis itself is applied by the shell through the shared
    /// <see cref="Analysis"/> instance.
    /// </summary>
    /// <param name="binding">The analysed data binding.</param>
    public void SetLoadedFile(DataBindingResult binding)
    {
        Preview.SetPreview(binding);
        HasFile = true;

        string name = binding.SourcePath is { Length: > 0 } path ? Path.GetFileName(path) : "data file";
        StatusText = binding.ErrorMessage is not null
            ? $"Could not load {name}."
            : $"Loaded {binding.DataSet?.RowCount ?? 0} rows from {name}.";

        ApplyBindingLambda(binding);
    }

    /// <summary>
    /// Recomputes the two λ readouts and the window control from a binding
    /// (rulings 2, 3, 4).
    /// </summary>
    /// <remarks>
    /// Called on load and again whenever the window dropdown changes, because
    /// the window λ is a property of the CHOSEN window as much as of the file.
    /// </remarks>
    /// <param name="binding">The analysed data binding, or null to blank the panel.</param>
    public void ApplyBindingLambda(DataBindingResult? binding)
    {
        _lastBinding = binding;

        if (binding?.ErrorMessage is not null)
        {
            MleLambdaText = "Fit unavailable \u2014 the file could not be analysed.";
            WindowLambdaText = "Fit unavailable \u2014 the file could not be analysed.";
            HasMleLambda = false;
            HasWindowLambda = false;
            SelectedWindowLambda = null;
            SelectedWindow = null;
            LambdaDivergenceText = "";
            IsWindowChoiceDisabled = true;
            return;
        }

        HasMleLambda = binding?.FittedArrivalRate is > 0;
        MleLambdaText = HasMleLambda
            ? $"\u03bb = {binding!.FittedArrivalRate:F4} patients/min  (1 \u00f7 mean of within-session inter-arrival gaps)"
            : "MLE \u03bb unavailable \u2014 the file has no usable inter-arrival gaps.";

        ApplyWindowLambda(binding);
    }

    /// <summary>
    /// Recomputes the window λ for the current dropdown choice (rulings 3, 4).
    /// </summary>
    /// <remarks>
    /// The single place a window λ is derived from the SELECTED window. Both the
    /// readout and <see cref="SelectedWindowLambda"/> come out of it, so what the
    /// user sees and what the run uses are the same figure by construction.
    /// </remarks>
    /// <param name="binding">The analysed data binding, or null to blank the panel.</param>
    public void ApplyWindowLambda(DataBindingResult? binding)
    {
        ObservationWindow? window = ResolveSelectedWindow(binding);
        if (window is null)
        {
            // Two different reasons land here, and they need different words: a
            // typo in a field the user is editing, versus a file that covers no
            // operating time at all (rulings 3, 4).
            bool customInvalid = SelectedObservationWindow == ObservationWindowSelection.Custom;
            IsCustomWindowInvalid = customInvalid;
            CustomWindowErrorText = customInvalid ? DescribeCustomHoursError() : "";

            HasWindowLambda = false;
            SelectedWindowLambda = null;
            SelectedWindow = null;
            WindowLambdaText = customInvalid
                ? ""
                : "No operating sessions detected in this file.";
            LambdaDivergenceText = "";
            IsWindowChoiceDisabled = true;
            return;
        }

        IsCustomWindowInvalid = false;
        CustomWindowErrorText = "";

        // Window λ₂ = arrivals ÷ the operating time of the CHOSEN window. The
        // numerator is every row in the file; only the denominator moves when the
        // dropdown changes, because the arrivals are what was actually observed.
        int arrivals = binding?.DataSet?.RowCount ?? 0;
        double? windowLambda = arrivals >= 2 ? arrivals / window.OperatingMinutes : null;

        if (windowLambda is not > 0)
        {
            HasWindowLambda = false;
            SelectedWindowLambda = null;
            SelectedWindow = null;
            WindowLambdaText =
                $"{ObservationWindowService.Describe(window)} \u2014 the file has fewer than two "
                + "arrivals, so a window \u03bb cannot be formed.";
            LambdaDivergenceText = "";
            IsWindowChoiceDisabled = true;
            return;
        }

        IsWindowChoiceDisabled = false;
        HasWindowLambda = true;
        SelectedWindowLambda = windowLambda;
        SelectedWindow = window;
        WindowLambdaText =
            $"λ = {windowLambda:F4} patients/min  ({arrivals} arrivals ÷ "
            + $"{window.OperatingMinutes:F0} operating minutes across "
            + $"{ObservationWindowService.Describe(window)})";

        LambdaDivergenceText = DescribeDivergence(binding!.FittedArrivalRate, windowLambda.Value);
    }

    /// <summary>
    /// The custom-hours error in the form FR-UI-17 requires: what is wrong, what
    /// was entered, and what a correct value looks like.
    /// </summary>
    private string DescribeCustomHoursError() =>
        $"Enter an operating-hours number greater than 0. You entered "
        + $"\"{CustomHours}\". A value like 5.5 means 5½ hours, or two operating "
        + "sessions of 2.75 hours each.";

    /// <summary>
    /// The window the dropdown currently resolves to (ruling 3).
    /// </summary>
    /// <param name="binding">The analysed data file, for the Auto case.</param>
    /// <returns>The resolved window, or null when it cannot be known yet.</returns>
    private ObservationWindow? ResolveSelectedWindow(DataBindingResult? binding)
    {
        // The UI edits HOURS for Custom; the service owns the conversion to a
        // window (rule 3), so the field's text is passed through unconverted and
        // a value that is not a positive number becomes null rather than a
        // zero-minute window.
        double? customHours = SelectedObservationWindow == ObservationWindowSelection.Custom
            && double.TryParse(CustomHours, out double hours)
            && hours > 0
                ? hours
                : null;

        return ObservationWindowService.Resolve(
            ObservationWindowService.FromFile(binding),
            SelectedObservationWindow,
            customHours);
    }

    /// <summary>
    /// States the gap between the estimates in the units the user chose, or
    /// empty when there is nothing to compare (ruling 2).
    /// </summary>
    private static string DescribeDivergence(double? mle, double window)
    {
        if (mle is not > 0)
        {
            return "";
        }

        double m = mle.Value;
        if (Math.Abs(m - window) < 1e-9)
        {
            return "Both estimates agree — the file's arrival rate is constant across its sessions.";
        }

        string direction = window > m ? "lower" : "higher";
        double pct = Math.Abs(window - m) / m * 100.0;
        return $"Window λ is {pct:F1}% {direction} than MLE λ. "
             + "MLE measures arrival while the clinic was open; window λ measures it across the whole session. "
             + "A multi-day file diverges because overnight and closed-day gaps are excluded from the MLE sample.";
    }

    /// <summary>Mirrors the config panel's stage-mismatch state into the tab's warning.</summary>
    /// <param name="visible">Whether the warning should show.</param>
    /// <param name="message">The full mismatch explanation.</param>
    public void ApplyStageMismatch(bool visible, string message)
    {
        HasStageMismatch = visible;
        StageMismatchMessage = message;
    }

    /// <summary>Returns the tab to its no-file state: empty preview, empty charts, no warning.</summary>
    public void Clear()
    {
        Preview.Clear();
        HasFile = false;
        StatusText = "No file loaded.";
        HasStageMismatch = false;
        StageMismatchMessage = "";
        MleLambdaText = "";
        WindowLambdaText = "";
        LambdaDivergenceText = "";
        HasMleLambda = false;
        HasWindowLambda = false;
        IsWindowChoiceDisabled = true;

        // The cached binding is dropped too. It is what
        // OnWindowSelectionChanged re-derives the λ from, so a cleared tab that
        // kept it would recompute a stale rate from a file the user just
        // dismissed — for a file that no longer has any visible preview, stage
        // rows or status text to contradict it.
        _lastBinding = null;
        SelectedWindowLambda = null;
        SelectedWindow = null;
        IsCustomWindowInvalid = false;
        CustomWindowErrorText = "";
        // Phase 8L: an empty per-stage list, not a placeholder "Exponential". There
        // is no binding here, so FitAll returns before it reads any family — an
        // empty list states that honestly instead of implying a family was chosen.
        _analysis.Apply(null, "Exponential", Array.Empty<StageServiceFamily>(), 0.05);
    }
}
