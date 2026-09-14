namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;

/// <summary>
/// One per-stage metric row of the results table (FR-STAT-6/7): routing-derived
/// λᵢ, c, μ, ρ, served count, wait, queue length and utilisation, with
/// per-server utilisation as its own concise string.
/// </summary>
public sealed partial class StageResultsViewModel : ViewModelBase
{
    /// <summary>Gets the stage display name.</summary>
    public string StageName { get; }

    /// <summary>Gets the routing-derived arrival rate λᵢ (per minute).</summary>
    public string ArrivalRate { get; }

    /// <summary>Gets the server count c.</summary>
    public string ServerCount { get; }

    /// <summary>Gets the service rate μ per server (per minute).</summary>
    public string ServiceRate { get; }

    /// <summary>Gets the traffic intensity ρᵢ.</summary>
    public string Rho { get; }

    /// <summary>Gets the number of patients served at this stage.</summary>
    public string Served { get; }

    /// <summary>Gets the average waiting time (minutes).</summary>
    public string Wait { get; }

    /// <summary>Gets the time-weighted average queue length.</summary>
    public string QueueLength { get; }

    /// <summary>Gets the stage-level utilisation (mean of servers).</summary>
    public string Utilisation { get; }

    /// <summary>Gets the per-server utilisation as "0.72 / 0.68 / 0.70".</summary>
    public string PerServerUtilisation { get; }

    /// <summary>Creates the row from engine metrics.</summary>
    public StageResultsViewModel(OpdSimulator.Core.Engine.StageMetrics m)
    {
        StageName = m.StageName;
        ArrivalRate = m.ArrivalRate.ToString("0.###", CultureInfo.InvariantCulture);
        ServerCount = m.ServerCount.ToString(CultureInfo.InvariantCulture);
        ServiceRate = m.ServiceRate.ToString("0.###", CultureInfo.InvariantCulture);
        Rho = m.Rho.ToString("0.###", CultureInfo.InvariantCulture);
        Served = m.PatientsServed.ToString("N0", CultureInfo.InvariantCulture);
        Wait = m.AverageWaitMinutes.ToString("0.###", CultureInfo.InvariantCulture);
        QueueLength = m.AverageQueueLength.ToString("0.###", CultureInfo.InvariantCulture);
        Utilisation = m.StageUtilisation.ToString("0.###", CultureInfo.InvariantCulture);
        PerServerUtilisation = string.Join(" / ",
            m.PerServerUtilisation.Select(u => u.ToString("0.###", CultureInfo.InvariantCulture)));
    }
}

/// <summary>Whole-system totals row shown under the per-stage table.</summary>
public sealed partial class SystemResultsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _served = "-";

    [ObservableProperty]
    private string _avgWait = "-";

    [ObservableProperty]
    private string _avgQueue = "-";

    [ObservableProperty]
    private string _avgSystem = "-";

    [ObservableProperty]
    private string _throughput = "-";

    [ObservableProperty]
    private string _operatingTime = "-";

    /// <summary>Fills the row from an engine report.</summary>
    public void Fill(OpdSimulator.Core.Engine.SimulationResult r)
    {
        Served = r.TotalPatientsServed.ToString("N0", CultureInfo.InvariantCulture);
        AvgWait = r.AverageWaitMinutes.ToString("0.###", CultureInfo.InvariantCulture) + " min";
        AvgQueue = r.AverageQueueLength.ToString("0.###", CultureInfo.InvariantCulture);
        AvgSystem = r.AverageSystemTimeMinutes.ToString("0.###", CultureInfo.InvariantCulture) + " min";
        Throughput = r.ThroughputPerMinute.ToString("0.###", CultureInfo.InvariantCulture) + "/min";
        OperatingTime = r.OperatingTimeMinutes.ToString("0.###", CultureInfo.InvariantCulture) + " min";
    }

    /// <summary>Clears the row back to dashes.</summary>
    public void Reset()
    {
        Served = "-";
        AvgWait = "-";
        AvgQueue = "-";
        AvgSystem = "-";
        Throughput = "-";
        OperatingTime = "-";
    }
}

/// <summary>
/// Right-panel result state (M5-E): the welcome card until a run starts, then
/// the metrics table, chi-square rows, trace lines, data preview and the
/// chart data. Widget visibility is user-selectable (FR-UI-14) and persisted
/// separately from the configuration (AGENTS §16.11).
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    /// <summary>Widget keys, in canonical order — persisted and default-visible.</summary>
    public static readonly IReadOnlyList<string> WidgetKeys =
        new[] { "metrics", "chiSquare", "trace", "dataPreview", "charts", "token" };

    /// <summary>Gets the welcome card content shown before the first run.</summary>
    public WelcomeCardViewModel Welcome { get; } = new();

    /// <summary>Gets the whole-system totals row.</summary>
    public SystemResultsViewModel System { get; } = new();

    /// <summary>Gets the per-stage metric rows.</summary>
    public ObservableCollection<StageResultsViewModel> Stages { get; } = new();

    /// <summary>Gets the chi-square fit rows.</summary>
    public ObservableCollection<FitRowViewModel> Fits { get; } = new();

    /// <summary>Gets the rendered trace lines (capped by the sink).</summary>
    public ObservableCollection<string> TraceLines { get; } = new();

    /// <summary>Gets the user-toggled widget visibility (persisted).</summary>
    public ObservableCollection<string> VisibleWidgets { get; } = new();

    /// <summary>Gets the chart data built from the last run (pure numbers).</summary>
    public ChartsData? ChartsData { get; private set; }

    /// <summary>Gets the rendered chart collections for the Input analysis / Run tabs.</summary>
    public ChartsPanelViewModel Charts { get; }

    /// <summary>Gets the shared data-file preview surface.</summary>
    public DataPreviewStore Preview { get; }

    /// <summary>Creates the result state with every widget visible by default.</summary>
    public ResultsViewModel(DataPreviewStore preview)
    {
        Preview = preview;
        Charts = ChartsPanelViewModel.ForResources();
        foreach (var key in WidgetKeys)
        {
            VisibleWidgets.Add(key);
        }
    }

    /// <summary>Gets whether a run-refusal/error banner is showing.</summary>
    public bool HasError => RunError is not null;

    /// <summary>Gets whether the results panel currently shows the welcome card.</summary>
    [ObservableProperty]
    private bool _isWelcomeVisible = true;

    /// <summary>Gets whether the results content (not the welcome card) is shown.</summary>
    public bool ShowResults => !IsWelcomeVisible;

    /// <summary>Tracks <see cref="HasError"/> with the banner text.</summary>
    partial void OnRunErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>Gets whether a run is in progress (disables Start, shows progress).</summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>Gets the live progress line during a run.</summary>
    [ObservableProperty]
    private string? _statusText;

    /// <summary>Gets whether a completed run is being shown.</summary>
    [ObservableProperty]
    private bool _hasResults;

    /// <summary>Gets a run-refusal banner message, or null on a clean run.</summary>
    [ObservableProperty]
    private string? _runError;

    /// <summary>Gets the run summary header (seed, horizon, effective p_exit).</summary>
    [ObservableProperty]
    private string _runSummary = string.Empty;

    /// <summary>Gets the effective exit probability used by the run.</summary>
    [ObservableProperty]
    private string _effectivePExit = string.Empty;

    /// <summary>Gets whether a run produced a trace (level > None).</summary>
    public bool HasTrace => TraceLines.Count > 0;

    // ---- Widget visibility --------------------------------------------------

    /// <summary>Gets whether the metrics widget is visible.</summary>
    public bool ShowMetrics => VisibleWidgets.Contains("metrics");

    /// <summary>Gets whether the chi-square widget is visible.</summary>
    public bool ShowChiSquare => VisibleWidgets.Contains("chiSquare");

    /// <summary>Gets whether the trace widget is visible.</summary>
    public bool ShowTrace => VisibleWidgets.Contains("trace");

    /// <summary>Gets whether the data-preview widget is visible.</summary>
    public bool ShowDataPreview => VisibleWidgets.Contains("dataPreview");

    /// <summary>Gets whether the charts widget is visible.</summary>
    public bool ShowCharts => VisibleWidgets.Contains("charts");

    /// <summary>Gets whether the token widget is visible.</summary>
    public bool ShowToken => VisibleWidgets.Contains("token");

    /// <summary>Toggles a widget's visibility (FR-UI-14).</summary>
    [RelayCommand]
    public void ToggleWidget(string key)
    {
        if (VisibleWidgets.Contains(key))
        {
            VisibleWidgets.Remove(key);
        }
        else
        {
            VisibleWidgets.Add(key);
        }
        RaiseWidgetVisibility();
    }

    /// <summary>Gets or sets whether the welcome card is shown; flips the results visibility too.</summary>
    partial void OnIsWelcomeVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowResults));

    /// <summary>Sets the widget set wholesale (applied when a preset loads).</summary>
    public void SetWidgets(IEnumerable<string> keys)
    {
        VisibleWidgets.Clear();
        foreach (var key in WidgetKeys)
        {
            if (keys.Contains(key))
            {
                VisibleWidgets.Add(key);
            }
        }
        RaiseWidgetVisibility();
    }

    private void RaiseWidgetVisibility()
    {
        OnPropertyChanged(nameof(ShowMetrics));
        OnPropertyChanged(nameof(ShowChiSquare));
        OnPropertyChanged(nameof(ShowTrace));
        OnPropertyChanged(nameof(ShowDataPreview));
        OnPropertyChanged(nameof(ShowCharts));
        OnPropertyChanged(nameof(ShowToken));
        OnPropertyChanged(nameof(HasTrace));
    }

    // ---- Run lifecycle ------------------------------------------------------

    /// <summary>Marks a run as starting: welcome card gives way to progress.</summary>
    public void BeginRun()
    {
        IsWelcomeVisible = false;
        IsRunning = true;
        HasResults = false;
        RunError = null;
        StatusText = "Preparing…";
    }

    /// <summary>Applies the outcome of a finished (or refused) run.</summary>
    public void EndRun(RunOutcome outcome, string summaryHeader)
    {
        IsRunning = false;
        StatusText = null;
        RunSummary = summaryHeader;
        EffectivePExit = outcome.EffectiveExitProbability.ToString("0.###", CultureInfo.InvariantCulture);

        if (outcome.Error is not null)
        {
            RunError = outcome.Error;
            HasResults = false;
            return;
        }

        var result = outcome.Result!;
        System.Fill(result);
        Stages.Clear();
        foreach (var m in result.StageMetrics)
        {
            Stages.Add(new StageResultsViewModel(m));
        }

        Fits.Clear();
        foreach (var fit in outcome.Fits)
        {
            Fits.Add(FitRowViewModel.From(fit));
        }

        TraceLines.Clear();
        foreach (var line in outcome.TraceLines)
        {
            TraceLines.Add(line);
        }

        ChartsData = ChartsBuilder.Build(result, outcome.Fits);
        Charts.SetCharts(ChartsData);
        HasResults = true;
        RaiseWidgetVisibility();
    }

    /// <summary>Clears all result state back to the welcome card (FR-UI-13 reset).</summary>
    public void Reset()
    {
        IsRunning = false;
        StatusText = null;
        HasResults = false;
        RunError = null;
        IsWelcomeVisible = true;
        RunSummary = string.Empty;
        EffectivePExit = string.Empty;
        System.Reset();
        Stages.Clear();
        Fits.Clear();
        TraceLines.Clear();
        ChartsData = null;
        Charts.Clear();
        RaiseWidgetVisibility();
    }
}