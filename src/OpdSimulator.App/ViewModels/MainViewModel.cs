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

    /// <summary>Raised when the view model asks the shell to select a tab index (RULING 2, Phase 7D).</summary>
    public event EventHandler<int>? TabSelectionChanged;

    /// <summary>
    /// Set by the root view to open the OS file picker. The single load path
    /// (RULING 3, Phase 7D): both Upload buttons route here.
    /// </summary>
    public Func<Task<string?>>? PickDataFileAsync { get; set; }

    public MainViewModel()
    {
        InputTab = new InputTabViewModel(InputAnalysis);

        Config.RunRequested += OnRunRequested;
        Config.PropertyChanged += OnConfigPropertyChanged;
        Config.DataBindingChanged += OnConfigDataBindingChanged;
        Config.SignificanceLevel.PropertyChanged += OnSignificanceLevelChanged;

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
    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConfigPanelViewModel.InterArrivalDistribution)
            or nameof(ConfigPanelViewModel.ServiceDistribution))
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
    /// same distribution choices the run will use. Runs the fit on the
    /// background thread and builds the chart controls on the UI thread (G5).
    /// </summary>
    private void SyncInputAnalysis()
        => InputAnalysis.ApplyAsync(
            Config.Binding,
            Config.InterArrivalDistribution ?? "Exponential",
            Config.ServiceDistribution ?? "Exponential",
            Config.SignificanceLevelForRun);

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
        Log.Information("Clear All requested: config, uploaded data and results reset to the launch state");
    }

    private void OnRunRequested(object? sender, EventArgs e)
    {
        if (Config.TryBuildRunParameters() is not { } parameters)
        {
            // Defence in depth: Start is gated on the config, so this should be
            // unreachable; still surface a clean banner rather than crash.
            Results.StartRun();
            Results.CompleteRun(new RunOutcome(null, Array.Empty<Models.FitReport>(),
                Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability,
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
                    Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability,
                    "The simulator stopped unexpectedly. See logs/errors-*.log for details.");
            }

            Dispatcher.UIThread.Post(() => Results.CompleteRun(outcome));
        });
    }
}
