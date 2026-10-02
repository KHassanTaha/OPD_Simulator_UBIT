using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using Serilog;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Window-level view model: owns the config, results and Input-tab panels and
/// bridges the user's Start click to a background simulation run (Phase 5). It
/// also routes the Input tab's intent events (upload, clear, use-for-run,
/// stage-sync) to the one data path and the config panel (Phase 7D, RULING 3).
/// The engine runs on a worker thread; every UI update is marshalled back
/// through <see cref="Dispatcher.UIThread"/>. Refusals and errors surface as
/// clean banners on the results panel — never as exceptions in the UI.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    /// <summary>Configuration panel state (Simulation tab, left column).</summary>
    public ConfigPanelViewModel Config { get; } = new();

    /// <summary>Results panel state (Simulation tab, right column). Persisted widget visibility is restored (FR-UI-14).</summary>
    public ResultsPanelViewModel Results { get; } = new(WidgetPreferences.Load());

    /// <summary>Input Analysis state (P1 input charts, Phase 6C) — shared with the Input tab (Phase 7D).</summary>
    public InputAnalysisViewModel InputAnalysis { get; } = new();

    /// <summary>Merged Input tab state (upload + preview + fit analysis, Phase 7D).</summary>
    public InputTabViewModel InputTab { get; }

    /// <summary>
    /// Output-side chi-square verification of the last run's generated samples
    /// (Phase 8B). Owned here and shared with <see cref="ResultsPanelViewModel"/>
    /// so the Results widget binds it under the panel's DataContext; populated
    /// when a run completes, cleared on Clear All and on a refused run.
    /// </summary>
    public SimulationVerificationViewModel SimulationVerification { get; } = new();

    /// <summary>
    /// Analytical validation of the last run against the closed-form M/M/c
    /// metrics (Phase 8C). Owned here and shared with
    /// <see cref="ResultsPanelViewModel"/> so the Results widget binds it under
    /// the panel's DataContext; populated when a comparable run completes,
    /// cleared on Clear All and on a refused run.
    /// </summary>
    public AnalyticalValidationViewModel AnalyticalValidation { get; } = new();

    /// <summary>Raised when the view model asks the shell to select a tab index (RULING 2, Phase 7D).</summary>
    public event EventHandler<int>? TabSelectionChanged;

    /// <summary>
    /// Which arrival-rate estimate the run should use: MLE or window (D-173,
    /// ruling 7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Owned HERE, at the top, because the control that sets it is on the Input
    /// tab and the value it feeds is built on the Simulation tab. Holding it in
    /// either panel would mean one of them keeps a copy, and two copies of a
    /// parameter is how a run ends up using a λ the user did not pick.
    /// </para>
    /// <para>
    /// Defaults to MLE. Window λ is the better estimator for the window a run
    /// simulates over, but MLE is what the course teaches and what every
    /// pre-existing result was produced with, so changing the default silently
    /// would have moved every historical number.
    /// </para>
    /// </remarks>
    [ObservableProperty]
    private LambdaSource selectedLambdaSource = LambdaSource.Mle;

    /// <summary>
    /// Pushes the choice down to the Input tab's radios whenever the shell
    /// changes it from anywhere other than the radios themselves (D-173).
    /// </summary>
    /// <remarks>
    /// A preset load, a config reset or a test can all set the field directly.
    /// Without this the radio buttons would keep rendering the old choice while
    /// the run used the new one — the display and the arithmetic disagreeing,
    /// which is the failure this separation of ownership was built to prevent.
    /// </remarks>
    partial void OnSelectedLambdaSourceChanged(LambdaSource value)
        => InputTab.RefreshLambdaSource();

    /// <summary>
    /// Set by the root view to open the OS file picker. The single load path
    /// (RULING 3, Phase 7D): both Upload buttons route here.
    /// </summary>
    public Func<Task<string?>>? PickDataFileAsync { get; set; }

    public MainViewModel()
    {
        InputTab = new InputTabViewModel(InputAnalysis);

        // Ruling 7: the choice lives here; both panels read and write it through
        // delegates so no second copy is kept (D-173).
        Config.LambdaSourceAccessor = () => SelectedLambdaSource;
        InputTab.LambdaSourceAccessor = () => SelectedLambdaSource;
        InputTab.LambdaSourceSetter = source => SelectedLambdaSource = source;

        // The Input tab owns the window selection and derives the λ that
        // selection implies; the config panel reads it at build time so the run
        // uses the figure on screen (D-172).
        Config.WindowLambdaAccessor = () => InputTab.SelectedWindowLambda;
        Config.SelectedWindowAccessor = () => InputTab.SelectedWindow;

        // The verification widget lives in the Results panel, whose DataContext
        // is the ResultsPanelViewModel; share the single instance so its XAML
        // can bind it (Phase 8B).
        Results.SimulationVerification = SimulationVerification;
        Results.AnalyticalValidation = AnalyticalValidation;

        Config.RunRequested += OnRunRequested;
        Config.PropertyChanged += OnConfigPropertyChanged;
        Config.DataBindingChanged += OnConfigDataBindingChanged;
        Config.SignificanceLevel.PropertyChanged += OnSignificanceLevelChanged;
        // Phase 8L: a row's family is an input to the Input tab's per-stage
        // chi-square, so a family edit refreshes it.
        Config.StageServiceFamiliesChanged += OnStageServiceFamiliesChanged;

        // One picker, one load path: both the config panel's Upload and the
        // Input tab's Upload converge on PickAndLoadDataFileAsync (RULING 3).
        Config.UploadRequested += OnUploadRequested;
        Config.NavigateToInputTabRequested += OnNavigateToInputTabRequested;
        InputTab.UploadFileRequested += OnUploadRequested;
        InputTab.ClearFileRequested += OnClearFileRequested;
        InputTab.UseForSimulationRequested += OnUseForSimulationRequested;
        InputTab.StageMismatchSyncRequested += OnStageMismatchSyncRequested;
        InputTab.StageMismatchKeepRequested += OnStageMismatchKeepRequested;

        UpdateConfigSourceStatus();
    }

    /// <summary>
    /// Refreshes the Input Analysis charts whenever the config's distribution
    /// choices change (user switches a dropdown), and keeps the data strip and
    /// the Input tab's mismatch warning in sync with the config.
    /// </summary>
    /// <remarks>
    /// Only the inter-arrival dropdown is watched here. The service side used to be
    /// watched too, but that property no longer reached the run (Phase 8K made the
    /// per-stage families authoritative), so watching it refreshed the tab in
    /// response to an edit that changed nothing. Per-stage service families are
    /// watched through <see cref="ConfigPanelViewModel.StageServiceFamiliesChanged"/>
    /// instead, which fires for the edit that actually moves a verdict (8L).
    /// </remarks>
    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConfigPanelViewModel.InterArrivalDistribution))
        {
            SyncInputAnalysis();
        }

        if (e.PropertyName == nameof(ConfigPanelViewModel.SourceMode))
        {
            UpdateConfigSourceStatus();
        }

        if (e.PropertyName is nameof(ConfigPanelViewModel.IsStageMismatchWarningVisible)
            or nameof(ConfigPanelViewModel.StageMismatchMessage))
        {
            SyncStageMismatchToInputTab();
        }
    }

    /// <summary>Refreshes the Input Analysis charts when a data file is loaded or cleared.</summary>
    private void OnConfigDataBindingChanged(object? sender, EventArgs e)
    {
        SyncInputAnalysis();

        if (Config.Binding is { } binding)
        {
            InputTab.SetLoadedFile(binding);
        }
        else
        {
            InputTab.Clear();
        }

        UpdateConfigSourceStatus();
        SyncStageMismatchToInputTab();
    }

    /// <summary>
    /// The significance level feeds the chi-square verdict captions on the
    /// Input Analysis cards, so an α edit refreshes them too.
    /// </summary>
    private void OnSignificanceLevelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConfigFieldViewModel.Value))
        {
            SyncInputAnalysis();
        }
    }

    /// <summary>
    /// Re-derives the Input Analysis charts from the current binding and the
    /// distribution choices the run will use. Runs the fit on the background
    /// thread and builds the chart controls on the UI thread (G5).
    /// </summary>
    /// <remarks>
    /// The service families are read per stage from the configured rows, not from
    /// one global value (Phase 8L). That is the same source the coordinator and the
    /// Results Panel use, so the Input tab and the Results Panel cannot disagree
    /// about which family a stage was tested against. Inter-arrival stays a single
    /// global value because arrivals really are one engine-wide stream.
    /// </remarks>
    private void SyncInputAnalysis()
        => InputAnalysis.ApplyAsync(
            Config.Binding,
            Config.InterArrivalDistribution ?? "Exponential",
            Config.StageServiceFamilies,
            Config.SignificanceLevelForRun);

    /// <summary>
    /// A stage's family changed, so the tab's per-stage verdicts are stale.
    /// </summary>
    private void OnStageServiceFamiliesChanged(object? sender, EventArgs e) => SyncInputAnalysis();

    /// <summary>Mirrors the config panel's stage-mismatch state into the Input tab's warning.</summary>
    private void SyncStageMismatchToInputTab()
        => InputTab.ApplyStageMismatch(Config.IsStageMismatchWarningVisible, Config.StageMismatchMessage);

    /// <summary>Derives the Simulation-tab strip's one-line status from the active data path.</summary>
    private void UpdateConfigSourceStatus()
        => Config.ConfigSourceStatus = Config.LoadedFileName is { Length: > 0 } file
            ? $"Using file: {file}"
            : Config.SourceMode == DataSourceMode.EnterManually
                ? "Entering parameters manually"
                : "No data — enter manually or upload in the Input tab.";

    private void OnNavigateToInputTabRequested(object? sender, EventArgs e) => SetSelectedTabIndex(1);

    /// <summary>Asks the shell to select a tab (handled by the root view, RULING 2, Phase 7D).</summary>
    public void SetSelectedTabIndex(int index) => TabSelectionChanged?.Invoke(this, index);

    private void OnUseForSimulationRequested(object? sender, EventArgs e)
    {
        Config.SourceMode = DataSourceMode.FitFromData;

        // Phase 8Q.1 (rulings 6): the recorded HISTORICAL server counts seed the
        // Stages section once, on this explicit click. They are acknowledged
        // straight after so a later visit to this tab — with no edit in between —
        // cannot overwrite a configuration the user has since tuned by hand.
        if (InputTab.SeedableServerCounts is { Count: > 0 } counts)
        {
            Config.SeedServerCountsFromHistory(counts);
            InputTab.AcknowledgeSeededServerCounts();
        }

        SetSelectedTabIndex(0);
    }

    private void OnStageMismatchSyncRequested(object? sender, EventArgs e) => Config.SyncStagesToData();

    private void OnStageMismatchKeepRequested(object? sender, EventArgs e) => Config.DismissStageMismatchWarning();

    private void OnClearFileRequested(object? sender, EventArgs e) => Config.ClearLoadedFile();

    private async void OnUploadRequested(object? sender, EventArgs e) => await PickAndLoadDataFileAsync();

    /// <summary>
    /// The one data-file load path (RULING 3, Phase 7D): both Upload buttons
    /// reach here, the shell's picker resolves a path, and the file is applied
    /// once to the config panel — which then pushes the binding to every
    /// subscriber through <see cref="ConfigPanelViewModel.DataBindingChanged"/>.
    /// </summary>
    public async Task PickAndLoadDataFileAsync()
    {
        if (PickDataFileAsync is null)
        {
            return;
        }

        try
        {
            string? path = await PickDataFileAsync();
            if (!string.IsNullOrWhiteSpace(path))
            {
                Config.ApplyLoadedFile(path);
            }
        }
        catch (Exception ex)
        {
            // AGENTS §12.4: never swallow; the picker is the only failure point.
            Log.Error(ex, "Data-file picker failed");
        }
    }

    /// <summary>
    /// Full reset to the fresh-launch state (Phase 5c.4): clears every config
    /// field, which also unloads the uploaded data file and drops its fitted
    /// parameters, and returns the results panel to the welcome card (empty
    /// widgets, no banner). Invoked by the ConfigPanel view after the user
    /// confirms the "Clear All" themed dialog.
    /// </summary>
    public void ResetAll()
    {
        Config.ResetToDefaults();
        Results.Reset();
        SimulationVerification.Clear();
        AnalyticalValidation.Clear();
        Log.Information("Clear All requested: config, uploaded data and results reset to the launch state");
    }

    /// <summary>
    /// One line describing where this run's parameters came from, for the
    /// calculations dialog (Phase 8M, D-164). The two paths of AGENTS §19 read
    /// very differently to someone auditing the maths, so the dialog says which
    /// one produced the numbers rather than leaving it to be guessed.
    /// </summary>
    private string DescribeParameterSource()
    {
        var file = Config.LoadedFileName;
        var filePart = string.IsNullOrWhiteSpace(file)
            ? "no data file loaded"
            : $"fitted from {file}";
        return Config.SourceMode == DataSourceMode.EnterManually
            ? "entered manually (with fitted values shown where a field was left blank)"
            : filePart;
    }

    private void OnRunRequested(object? sender, EventArgs e)
    {
        if (Config.TryBuildRunParameters() is not { } parameters)
        {
            // Defence in depth: Start is gated on the config, so this should be
            // unreachable; still surface a clean banner rather than crash.
            Results.StartRun();
            Results.CompleteRun(new RunOutcome(null, Array.Empty<Models.FitReport>(),
                Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability, 0.0,
                "The run could not be built from the current configuration. Check the fields marked in red."));
            return;
        }

        Results.StartRun();
        Results.SetChiSquareAlpha(Config.SignificanceLevelForRun);

        Task.Run(() =>
        {
            RunOutcome outcome;
            try
            {
                outcome = SimulationCoordinator.Run(parameters, Config.Binding,
                    status => Dispatcher.UIThread.Post(() => Results.StatusText = status),
                    Config.SignificanceLevelForRun);
            }
            catch (Exception ex)
            {
                // The coordinator already converts known refusals into an
                // outcome; anything that fell through is a defect — log it and
                // still refuse cleanly (AGENTS §12.4: never swallow).
                Log.Error(ex, "Unexpected simulator failure");
                outcome = new RunOutcome(null, Array.Empty<Models.FitReport>(),
                    Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability, 0.0,
                    "The simulator stopped unexpectedly. See logs/errors-*.log for details.");
            }

            Dispatcher.UIThread.Post(() =>
            {
                Results.CompleteRun(outcome, parameters, DescribeParameterSource(), Config.Binding);
                ApplyVerification(outcome, parameters);
                ApplyAnalyticalValidation(outcome, parameters);
            });
        });
    }

    /// <summary>
    /// Populates the output-side verification widget from the finished run
    /// (Phase 8B). The engine samples every stage exponentially regardless of
    /// the configured family (D-126), so the one configured service family is
    /// applied to every stage here. A refused/crashed run clears the widget.
    /// </summary>
    /// <param name="outcome">The completed run outcome.</param>
    /// <param name="parameters">The parameters the run used (supplies the configured families).</param>
    private void ApplyVerification(RunOutcome outcome, SimulationParameters parameters)
    {
        if (outcome.Result is null)
        {
            SimulationVerification.Clear();
            return;
        }

        // Per-stage specs, not one family repeated: 8K lets each stage differ, and the
        // verifier must test each stage against the distribution that stage actually
        // used. The old Repeat(shim) path made a Normal stage verify as if it were the
        // first stage's family.
        SimulationVerification.ApplyAsync(
            outcome.Result,
            parameters.InterArrivalDistribution,
            parameters.ServiceFamilies,
            Config.SignificanceLevelForRun);
    }

    /// <summary>
    /// Populates the analytical-validation widget from the finished run
    /// (Phase 8C). The per-stage λ, μ and c are read back from the run's own
    /// <see cref="StageMetrics"/> so the comparison is apples-to-apples with the
    /// simulated numbers (including the exit-probability-adjusted downstream
    /// arrival rates). A refused/crashed run clears the widget; a run that fails
    /// the M/M/c assumptions yields an empty table plus the explanatory message.
    /// </summary>
    /// <param name="outcome">The completed run outcome.</param>
    /// <param name="parameters">The parameters the run used (supplies the configured families).</param>
    private void ApplyAnalyticalValidation(RunOutcome outcome, SimulationParameters parameters)
    {
        if (outcome.Result is null)
        {
            AnalyticalValidation.Clear();
            return;
        }

        // Analytical comparison is M/M/c only, and AnalyticalValidationService refuses a
        // run outright if ANY stage is not Exponential. It therefore takes family NAMES,
        // not specs: handing it the real per-stage names is what makes that refusal
        // actually fire for a mixed-configuration run, which the old
        // Repeat(ServiceDistribution) shim could never do.
        var serviceFamilies = parameters.ServiceFamilies
            .Select(spec => spec.Family.ToString())
            .ToList();
        var stageInputs = outcome.Result.StageMetrics
            .Select(m => (m.ArrivalRate, m.ServiceRate, m.ServerCount))
            .ToList();
        AnalyticalValidation.ApplyAsync(
            outcome.Result,
            parameters.InterArrivalDistribution,
            serviceFamilies,
            stageInputs);
    }
}
