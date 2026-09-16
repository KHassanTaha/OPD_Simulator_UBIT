using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;
using Serilog;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Window-level view model: owns the config and results panels and bridges the
/// user's Start click to a background simulation run (Phase 5). The engine
/// runs on a worker thread; every UI update is marshalled back through
/// <see cref="Dispatcher.UIThread"/>. Refusals and errors surface as clean
/// banners on the results panel — never as exceptions in the UI.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    /// <summary>Configuration panel state (Simulation tab, left column).</summary>
    public ConfigPanelViewModel Config { get; } = new();

    /// <summary>Results panel state (Simulation tab, right column). Persisted widget visibility is restored (FR-UI-14).</summary>
    public ResultsPanelViewModel Results { get; } = new(WidgetPreferences.Load());

    public MainViewModel()
    {
        Config.RunRequested += OnRunRequested;
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
        Results.SetPreview(Config.Binding);

        Task.Run(() =>
        {
            RunOutcome outcome;
            try
            {
                outcome = SimulationCoordinator.Run(parameters, Config.Binding,
                    status => Dispatcher.UIThread.Post(() => Results.StatusText = status));
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