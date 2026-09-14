namespace OpdSimulator.App.ViewModels;

using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Engine;
using Serilog;

/// <summary>
/// Root view model of the main window (M5-D): owns the left config panel, the
/// right results panel, the in-program guide and the toast stack, and runs one
/// simulation on a background thread so the UI never blocks (M5-F).
/// Configuration, data and results are never persisted across sessions
/// (FR-UI-21) — only widget/section preferences are.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly Action<Action> _uiPost;
    private readonly ToastService _toasts;
    private readonly WidgetPreferences _prefs;

    /// <summary>Window title shown in the OS title bar.</summary>
    public string Title => "OPD Clinic Queue Simulator";

    /// <summary>Gets the left-panel configuration state.</summary>
    public ConfigViewModel Config { get; }

    /// <summary>Gets the right-panel result state.</summary>
    public ResultsViewModel Results { get; }

    /// <summary>Gets the in-program guide state (§17.1).</summary>
    public GuideViewModel Guide { get; }

    /// <summary>Gets the live toast stack.</summary>
    public ToastService Toasts => _toasts;

    /// <summary>Gets whether the guide overlay is open.</summary>
    [ObservableProperty]
    private bool _isGuideOpen;

    /// <summary>Gets whether a run is in progress.</summary>
    public bool IsRunning => Results.IsRunning;

    /// <summary>Creates the root view model with UI-thread marshalling.</summary>
    public MainViewModel()
        : this(a => Dispatcher.UIThread.Post(a), new ToastService(), WidgetPreferences.Load())
    {
    }

    /// <summary>Creates the root view model with an explicit dispatcher (tests).</summary>
    public MainViewModel(Action<Action> uiPost, ToastService toasts, WidgetPreferences prefs)
    {
        _uiPost = uiPost ?? throw new ArgumentNullException(nameof(uiPost));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _prefs = prefs ?? throw new ArgumentNullException(nameof(prefs));

        Config = new ConfigViewModel();
        Results = new ResultsViewModel(Config.Preview);
        Guide = GuideViewModel.FromEmbedded();

        Config.ApplyCollapsedSections(_prefs.CollapsedSections);
        if (_prefs.VisibleWidgets.Count > 0)
        {
            Results.SetWidgets(_prefs.VisibleWidgets);
        }

        Results.VisibleWidgets.CollectionChanged += OnWidgetsChanged;
        Config.PropertyChanged += OnConfigPropertyChanged;
    }

    private void OnConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Persist section collapse state as soon as any section toggles.
        if (e.PropertyName == nameof(ConfigViewModel.DataSectionExpanded)
            || e.PropertyName == nameof(ConfigViewModel.ParametersSectionExpanded)
            || e.PropertyName == nameof(ConfigViewModel.ServersSectionExpanded)
            || e.PropertyName == nameof(ConfigViewModel.HorizonSectionExpanded)
            || e.PropertyName == nameof(ConfigViewModel.AdvancedSectionExpanded))
        {
            SavePreferences();
        }
    }

    private void OnWidgetsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SavePreferences();

    private void SavePreferences()
    {
        _prefs.VisibleWidgets = Results.VisibleWidgets.ToList();
        _prefs.CollapsedSections = Config.CollapsedSectionKeys.ToList();
        _prefs.Save();
    }

    /// <summary>
    /// Validates the config fields, runs the simulation on a background thread
    /// and lands the outcome on the results panel (FR-UI-1 Start Calculation).
    /// </summary>
    [RelayCommand]
    public async Task RunAsync()
    {
        if (Results.IsRunning)
        {
            return;
        }

        if (!Config.TryBuildParameters(out var parameters, out var issues))
        {
            _uiPost(() => _toasts.Show(
                issues.Count == 1 ? issues[0] : $"{issues.Count} fields need attention.",
                ToastKind.Error, TimeSpan.FromSeconds(6)));
            return;
        }

        var p = parameters!;
        var warnings = ConfigViewModel.ComputeWarnings(p);
        if (warnings.Count > 0)
        {
            Log.Warning("Run started with unstable stage(s): {Stages}", string.Join("; ", warnings));
            _toasts.Show("Warning: " + string.Join("; ", warnings), ToastKind.Error, TimeSpan.FromSeconds(8));
        }

        Results.BeginRun();

        try
        {
            var outcome = await Task.Run(() =>
                SimulationCoordinator.Run(p, Config.Binding, status =>
                    _uiPost(() => Results.StatusText = status)));

            _uiPost(() =>
            {
                Results.EndRun(outcome, BuildRunSummaryHeader(p));
                if (outcome.Error is not null)
                {
                    _toasts.Show(outcome.Error, ToastKind.Error, TimeSpan.FromSeconds(8));
                }
                else
                {
                    _toasts.Show("Simulation complete.", ToastKind.Success);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Simulation run failed unexpectedly");
            _uiPost(() =>
            {
                Results.IsRunning = false;
                Results.StatusText = null;
                Results.RunError = "The simulation failed unexpectedly. " +
                    "See logs/crash-*.log for details.";
                Results.HasResults = false;
                _toasts.Show("Run failed — see the results panel for details.", ToastKind.Error);
            });
        }
    }

    /// <summary>Resets config fields and the results panel (empty startup, FR-UI-21).</summary>
    [RelayCommand]
    public void ResetAll()
    {
        Config.ResetFields();
        Results.Reset();
        _toasts.Show("Configuration and results cleared.", ToastKind.Info);
    }

    /// <summary>Opens the guide, optionally at a help anchor (F1 / '?' icons).</summary>
    [RelayCommand]
    public void OpenGuide(string? anchor = null)
    {
        if (!string.IsNullOrEmpty(anchor))
        {
            Guide.TryOpenAnchor(anchor);
        }
        IsGuideOpen = true;
    }

    /// <summary>Closes the guide, returning focus to the opener (Escape).</summary>
    [RelayCommand]
    public void CloseGuide() => IsGuideOpen = false;

    /// <summary>Builds the one-line run header shown above the metrics.</summary>
    public static string BuildRunSummaryHeader(SimulationParameters p)
    {
        string horizon = p.HorizonMode == HorizonMode.Days
            ? $"{p.GeneratorDays} day(s), starting {p.StartDay}"
            : $"{p.HorizonMinutes:0.##} minute window";
        return $"Seed {p.Seed} · {horizon}" +
            (p.DailyCap is { } cap ? $" · cap {cap}/day" : string.Empty);
    }
}