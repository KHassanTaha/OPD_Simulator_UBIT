namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;

/// <summary>One metrics-table label/value pair (FR-STAT-6).</summary>
/// <param name="Label">Human metric name.</param>
/// <param name="Value">Formatted metric value.</param>
public sealed record MetricRow(string Label, string Value);

/// <summary>One per-stage row of the results metrics table (FR-STAT-6/7).</summary>
public sealed record StageMetricRow(
    string StageName,
    string ArrivalRate,
    string Servers,
    string ServiceRate,
    string Rho,
    string Served,
    string Wait,
    string Queue,
    string Utilisation);

/// <summary>One chi-square goodness-of-fit verdict row (FR-STAT-8).</summary>
public sealed record ChiSquareRow(
    string Series,
    string Distribution,
    string Chi2,
    string DegreesOfFreedom,
    string PValue,
    string Decision);

/// <summary>
/// Drives the Simulation-tab results panel (Phase 5): the welcome card until a
/// run starts, then the chosen widgets — metrics table, per-server utilisation
/// chart (Phase 6c.4), queue-length-over-time chart and waiting-time histogram
/// (Phase 6c.5), chi-square table, the scrollable event trace, the data
/// preview — plus the run-refusal banner with its clean message (G3/G4, 5-F).
/// Widget visibility follows <see cref="WidgetPreferences"/> (FR-UI-14),
/// seeded to all-on.
/// </summary>
public partial class ResultsPanelViewModel : ObservableObject
{
    private const string Unavailable = "—";

    private readonly WidgetPreferences? _preferences;

    /// <summary>Welcome-card content (FR-UI-5).</summary>
    public WelcomeCardViewModel Welcome { get; } = new();

    /// <summary>Creates the panel. Pass the per-user preferences store to persist widget visibility (FR-UI-14); null keeps tests hermetic.</summary>
    public ResultsPanelViewModel(WidgetPreferences? preferences = null)
    {
        _preferences = preferences;
        ApplyPreferences();
    }

    private void ApplyPreferences()
    {
        if (_preferences is null)
        {
            return;
        }

        var visible = _preferences.VisibleWidgets;
        bool migrated = false;
        if (!visible.Contains("utilisation")
            || !visible.Contains("queueLength")
            || !visible.Contains("waitHistogram"))
        {
            // Widget migrations: a ui.json written before the utilisation (6c.4)
            // and queue-length / waiting-time (6c.5) widgets existed must not
            // hide them forever. Add each missing key once and persist, so every
            // new widget starts visible like the others. A deliberate later-off
            // is re-enabled once — same accepted behaviour as the 6c.4 migration.
            if (!visible.Contains("utilisation")) { visible.Add("utilisation"); migrated = true; }
            if (!visible.Contains("queueLength")) { visible.Add("queueLength"); migrated = true; }
            if (!visible.Contains("waitHistogram")) { visible.Add("waitHistogram"); migrated = true; }
            if (migrated) { _preferences.Save(); }
        }

        ShowMetrics = visible.Contains("metrics");
        ShowChiSquare = visible.Contains("chiSquare");
        ShowTrace = visible.Contains("trace");
        ShowDataPreview = visible.Contains("dataPreview");
        ShowUtilisation = visible.Contains("utilisation");
        ShowQueueLength = visible.Contains("queueLength");
        ShowWaitHistogram = visible.Contains("waitHistogram");
    }

    /// <summary>Called at the start of every run attempt so config proof is a one-time concern.</summary>
    public void StartRun()
    {
        IsWelcomeVisible = false;
        HasRun = true;
        IsBusy = true;
        StatusText = "Running simulation…";
        RunError = null;
    }

    /// <summary>Finishes a run with its outcome (worker thread result, UI thread call).</summary>
    /// <param name="outcome">Engine run outcome (result, fits, trace, refusal message).</param>
    public void CompleteRun(RunOutcome outcome)
    {
        IsBusy = false;
        HasRun = true;
        IsWelcomeVisible = false;
        StatusText = string.Empty;

        RunError = outcome.Error;
        RunSummary = outcome.Error is null
            ? "Run complete"
            : "The run was refused before it started";
        EffectiveExitText = $"Effective exit probability (after Screening): {outcome.EffectiveExitProbability:0.###}";

        ChiSquareRows.Clear();
        foreach (var fit in outcome.Fits)
        {
            ChiSquareRows.Add(fit.ChiSquare is { } cs && fit.Fitted is not null
                ? new ChiSquareRow(fit.Label, fit.Fitted.Name, $"{cs.Statistic:0.###}", $"{cs.DegreesOfFreedom}",
                    $"{cs.PValue:0.###}", cs.Decision)
                : new ChiSquareRow(fit.Label, "fit unavailable", Unavailable, Unavailable, Unavailable, Unavailable));
        }

        TraceText = outcome.TraceLines.Count > 0
            ? string.Join(Environment.NewLine, outcome.TraceLines)
            : string.Empty;

        SetMetrics(outcome.Result);
        SetUtilisation(outcome.Result);
        SetQueueLength(outcome.Result);
        SetWaitHistogram(outcome.Result);
    }

    private static string N0(double v) => $"{v:0.###}";

    /// <summary>
    /// Returns the panel to its fresh-launch state (FR-UI-5/21, Phase 5c.4):
    /// the welcome card shows again, every widget, the banner and the status
    /// texts are cleared, and widget visibility is re-applied from
    /// preferences. Called by <see cref="MainViewModel.ResetAll"/> after the
    /// user confirms "Clear All".
    /// </summary>
    public void Reset()
    {
        IsWelcomeVisible = true;
        HasRun = false;
        IsBusy = false;
        StatusText = string.Empty;
        RunSummary = string.Empty;
        EffectiveExitText = string.Empty;
        RunError = null;
        TraceText = string.Empty;
        ChiSquareCaption = DefaultChiSquareCaption;
        SystemMetrics.Clear();
        StageRows.Clear();
        ChiSquareRows.Clear();
        PreviewColumnTitles = null;
        PreviewRows = null;
        PreviewInvalidRows = null;
        PreviewError = null;
        UtilisationChart = null;
        QueueLengthChart = null;
        WaitHistogram = null;
        WaitHistogramChart = null;
        WaitStageNames = null;
        SelectedWaitStage = null;
        IsWaitLogScale = false;
        _lastResult = null;
        ApplyPreferences();
    }

    private void SetMetrics(OpdSimulator.Core.Engine.SimulationResult? result)
    {
        SystemMetrics.Clear();
        StageRows.Clear();
        if (result is null)
        {
            return;
        }

        SystemMetrics.Add(new MetricRow("Patients served", $"{result.TotalPatientsServed}"));
        SystemMetrics.Add(new MetricRow("Average wait (min)", N0(result.AverageWaitMinutes)));
        SystemMetrics.Add(new MetricRow("Average queue length", N0(result.AverageQueueLength)));
        SystemMetrics.Add(new MetricRow("Average system time (min)", N0(result.AverageSystemTimeMinutes)));
        SystemMetrics.Add(new MetricRow("Throughput (per min)", N0(result.ThroughputPerMinute)));
        SystemMetrics.Add(new MetricRow("Operating time (min)", N0(result.OperatingTimeMinutes)));

        foreach (var stage in result.StageMetrics)
        {
            StageRows.Add(new StageMetricRow(
                stage.StageName,
                N0(stage.ArrivalRate),
                $"{stage.ServerCount}",
                N0(stage.ServiceRate),
                N0(stage.Rho),
                $"{stage.PatientsServed}",
                N0(stage.AverageWaitMinutes),
                N0(stage.AverageQueueLength),
                $"{stage.StageUtilisation:0.##}"));
        }
    }

    /// <summary>
    /// Feeds the per-server utilisation widget (FR-STAT-7, Phase 6c.4). The
    /// chart control is built by <see cref="ChartControlBuilder"/> on the UI
    /// thread; a refused run (or none) leaves an empty card and the widget shows
    /// its "run a simulation to see utilisation" empty state instead.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    private void SetUtilisation(OpdSimulator.Core.Engine.SimulationResult? result)
    {
        if (result is null)
        {
            UtilisationChart = null;
            return;
        }

        var data = UtilisationChartService.Build(result);
        try
        {
            UtilisationChart = ChartControlBuilder.BuildUtilisationChart(data);
        }
        catch (Exception ex)
        {
            // G5: LiveCharts controls only construct inside a fully-initialised
            // Avalonia app (with a rendering platform). Headless unit tests that
            // assert metrics/trace have none, so the widget degrades to its
            // empty state instead of failing the whole results panel. The real
            // path is proven by the AvaloniaFact screenshot test.
            Serilog.Log.Warning(ex, "Utilisation chart could not be built in this context");
            UtilisationChart = null;
        }
    }

    /// <summary>
    /// Feeds the queue-length-over-time widget (FR-UI-4 P2, Phase 6c.5). The
    /// chart control is pre-decimated by <see cref="QueueLengthChartService"/>
    /// and built on the UI thread; a refused run (or none) leaves an empty card
    /// and the widget shows its "run a simulation to see queue length" empty
    /// state instead.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    private void SetQueueLength(OpdSimulator.Core.Engine.SimulationResult? result)
    {
        if (result is null)
        {
            QueueLengthChart = null;
            return;
        }

        var data = QueueLengthChartService.Build(result);
        try
        {
            QueueLengthChart = ChartControlBuilder.BuildQueueChart(data);
        }
        catch (Exception ex)
        {
            // Same G5 degradation rationale as the utilisation widget.
            Serilog.Log.Warning(ex, "Queue-length chart could not be built in this context");
            QueueLengthChart = null;
        }
    }

    /// <summary>
    /// Feeds the waiting-time histogram widget (FR-UI-4 P2, Phase 6c.5): stores
    /// the finished result, resets the stage selector to the first stage (every
    /// new run starts from a deterministic selection), and rebuilds the chart
    /// for that stage. A refused run (or none) leaves no stage names, no
    /// selection and no chart — the widget's empty state shows.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    private void SetWaitHistogram(OpdSimulator.Core.Engine.SimulationResult? result)
    {
        _lastResult = result;
        WaitStageNames = result is null ? null : WaitHistogramService.StageNames(result);
        SelectedWaitStage = WaitStageNames is { Count: > 0 } ? WaitStageNames[0] : null;
        RebuildWaitHistogram();
    }

    /// <summary>
    /// Rebuilds the waiting-time histogram for the currently selected stage.
    /// Triggered by the stage selector, the log-scale toggle and every new run;
    /// the pure data record is always refreshed so tests can assert the data
    /// follows the selection even in contexts where the LiveCharts control
    /// cannot be constructed (G5 degradation).
    /// </summary>
    private void RebuildWaitHistogram()
    {
        if (_lastResult is null || SelectedWaitStage is null)
        {
            WaitHistogram = null;
            WaitHistogramChart = null;
            return;
        }

        var data = WaitHistogramService.Build(_lastResult, SelectedWaitStage);
        WaitHistogram = data;
        try
        {
            WaitHistogramChart = ChartControlBuilder.BuildWaitHistogramChart(data, IsWaitLogScale);
        }
        catch (Exception ex)
        {
            // Same G5 degradation rationale as the utilisation widget.
            Serilog.Log.Warning(ex, "Waiting-time histogram could not be built in this context");
            WaitHistogramChart = null;
        }
    }

    /// <summary>Feeds the data-preview widget from the current data binding.</summary>
    /// <param name="binding">Binding result of the last loaded file, or null.</param>
    public void SetPreview(DataBindingResult? binding)
    {
        if (binding?.DataSet is not { } dataSet)
        {
            PreviewColumnTitles = null;
            PreviewRows = null;
            PreviewInvalidRows = null;
            PreviewError = binding?.ErrorMessage;
            return;
        }

        PreviewColumnTitles = dataSet.Columns.ToList();
        PreviewRows = dataSet.Rows
            .Select(row => (IReadOnlyList<string?>)row.Values.Select(v => (string?)v).ToList())
            .ToList();

        // RowNumber is 1-based data-row number (row 1 = first data row below
        // the header); the preview widget uses 0-based indices.
        PreviewInvalidRows = binding.Issues
            .Where(i => i.RowNumber > 0)
            .GroupBy(i => i.RowNumber - 1)
            .ToDictionary(g => g.Key, g => (string?)g.First().Reason);
        PreviewError = binding.ErrorMessage;
        ShowDataPreview = binding.IsUsable;
    }

    // ── Header / banner ───────────────────────────────────────────────────

    /// <summary>True while the welcome card is shown (before any run attempt).</summary>
    [ObservableProperty]
    private bool _isWelcomeVisible = true;

    /// <summary>True once a run was attempted (the welcome card is replaced then).</summary>
    [ObservableProperty]
    private bool _hasRun;

    /// <summary>True while the engine runs on the background thread.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Live progress text shown under the busy indicator.</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Header line of the results area ("Run complete" or "The run was refused…").</summary>
    [ObservableProperty]
    private string _runSummary = string.Empty;

    /// <summary>Effective exit probability actually used by the last run.</summary>
    [ObservableProperty]
    private string _effectiveExitText = string.Empty;

    /// <summary>Refusal/error message for the banner, or null/blank when clean.</summary>
    [ObservableProperty]
    private string? _runError;

    /// <summary>True while a refusal/error banner should be shown.</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(RunError);

    partial void OnRunErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    // ── Widgets ───────────────────────────────────────────────────────────

    /// <summary>System-totals rows of the metrics widget.</summary>
    public ObservableCollection<MetricRow> SystemMetrics { get; } = new();

    /// <summary>Per-stage rows of the metrics widget.</summary>
    public ObservableCollection<StageMetricRow> StageRows { get; } = new();

    /// <summary>Chi-square verdict rows.</summary>
    public ObservableCollection<ChiSquareRow> ChiSquareRows { get; } = new();

    /// <summary>Widget header, e.g. "Chi-square goodness-of-fit (α = 0.05)" — α flows in at run start (5d.2, D-113).</summary>
    [ObservableProperty]
    private string _chiSquareCaption = DefaultChiSquareCaption;

    private const string DefaultChiSquareCaption = "Chi-square goodness-of-fit (α = 0.05)";

    /// <summary>Records the significance level the next run's verdicts will be decided at.</summary>
    /// <param name="alpha">Significance level from the Model section.</param>
    public void SetChiSquareAlpha(double alpha) =>
        ChiSquareCaption = $"Chi-square goodness-of-fit (α = {alpha:0.###})";

    /// <summary>Rendered trace lines of the diagnostic run, joined for the log widget.</summary>
    [ObservableProperty]
    private string _traceText = string.Empty;

    /// <summary>Data-preview column titles from the loaded file.</summary>
    [ObservableProperty]
    private IEnumerable<string>? _previewColumnTitles;

    /// <summary>Data-preview raw rows (each an ordered list of cell strings or nulls).</summary>
    [ObservableProperty]
    private IEnumerable<IReadOnlyList<string?>>? _previewRows;

    /// <summary>Preview row index → validator reason for invalid rows.</summary>
    [ObservableProperty]
    private IReadOnlyDictionary<int, string?>? _previewInvalidRows;

    /// <summary>Load-failure summary that replaces the preview table (FR-UI-9).</summary>
    [ObservableProperty]
    private string? _previewError;

    /// <summary>Built per-server utilisation chart (Phase 6c.4), or null before the first run.</summary>
    [ObservableProperty]
    private Control? _utilisationChart;

    /// <summary>True once a utilisation chart was built from a finished run.</summary>
    public bool HasUtilisationChart => UtilisationChart is not null;

    /// <summary>True before the first run — the widget shows its empty state then.</summary>
    public bool ShowUtilisationEmptyState => !HasUtilisationChart;

    partial void OnUtilisationChartChanged(Control? value)
    {
        OnPropertyChanged(nameof(HasUtilisationChart));
        OnPropertyChanged(nameof(ShowUtilisationEmptyState));
    }

    /// <summary>Widget caption (FR-STAT-7): names the imbalance threshold and rule.</summary>
    public string UtilisationCaption => UtilisationChartService.Caption;

    // ── Queue-length-over-time widget (FR-UI-4 P2, Phase 6c.5) ────────────

    /// <summary>Built queue-length-over-time chart, or null before the first run.</summary>
    [ObservableProperty]
    private Control? _queueLengthChart;

    /// <summary>True once a queue-length chart was built from a finished run.</summary>
    public bool HasQueueLengthChart => QueueLengthChart is not null;

    /// <summary>True before the first run — the widget shows its empty state then.</summary>
    public bool ShowQueueLengthEmptyState => !HasQueueLengthChart;

    partial void OnQueueLengthChartChanged(Control? value)
    {
        OnPropertyChanged(nameof(HasQueueLengthChart));
        OnPropertyChanged(nameof(ShowQueueLengthEmptyState));
    }

    /// <summary>Widget caption: names the sampling and downsampling policy.</summary>
    public string QueueLengthCaption => QueueLengthChartService.Caption;

    // ── Waiting-time histogram widget (FR-UI-4 P2, Phase 6c.5) ────────────

    /// <summary>The last finished run the histogram rebuilds from (selector/scale changes).</summary>
    private OpdSimulator.Core.Engine.SimulationResult? _lastResult;

    /// <summary>Pure data record for the selected stage (refreshed on every rebuild).</summary>
    [ObservableProperty]
    private WaitHistogramData? _waitHistogram;

    /// <summary>Built waiting-time histogram chart, or null before the first run.</summary>
    [ObservableProperty]
    private Control? _waitHistogramChart;

    /// <summary>True once a waiting-time histogram was built from a finished run.</summary>
    public bool HasWaitHistogramChart => WaitHistogramChart is not null;

    /// <summary>True before the first run — the widget shows its empty state then.</summary>
    public bool ShowWaitHistogramEmptyState => !HasWaitHistogramChart;

    partial void OnWaitHistogramChartChanged(Control? value)
    {
        OnPropertyChanged(nameof(HasWaitHistogramChart));
        OnPropertyChanged(nameof(ShowWaitHistogramEmptyState));
    }

    /// <summary>Stage names the selector offers, in run order (null before a run).</summary>
    [ObservableProperty]
    private IReadOnlyList<string>? _waitStageNames;

    /// <summary>The stage whose histogram is shown; resets to the first on every new run.</summary>
    [ObservableProperty]
    private string? _selectedWaitStage;

    partial void OnSelectedWaitStageChanged(string? value) => RebuildWaitHistogram();

    /// <summary>Log-scale toggle for long tails: switches the Y axis to base-10 logarithmic.</summary>
    [ObservableProperty]
    private bool _isWaitLogScale;

    partial void OnIsWaitLogScaleChanged(bool value) => RebuildWaitHistogram();

    /// <summary>Widget caption: names the binning policy.</summary>
    public string WaitHistogramCaption => WaitHistogramService.Caption;

    // ── Widget visibility (FR-UI-14, persisted via WidgetPreferences) ─────

    [ObservableProperty]
    private bool _showMetrics = true;

    [ObservableProperty]
    private bool _showChiSquare = true;

    [ObservableProperty]
    private bool _showTrace = true;

    [ObservableProperty]
    private bool _showDataPreview;

    [ObservableProperty]
    private bool _showUtilisation = true;

    [ObservableProperty]
    private bool _showQueueLength = true;

    [ObservableProperty]
    private bool _showWaitHistogram = true;

    partial void OnShowMetricsChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowChiSquareChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowTraceChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowDataPreviewChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowUtilisationChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowQueueLengthChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowWaitHistogramChanged(bool value) => OnWidgetVisibilityChanged();

    /// <summary>Programmatic toggle used by tests and presets (the strip uses TwoWay binds).</summary>
    /// <param name="key">The widget key ("metrics", "chiSquare", "trace", "dataPreview", "utilisation", "queueLength", "waitHistogram").</param>
    public void ToggleWidget(string key)
    {
        switch (key)
        {
            case "metrics":
                ShowMetrics = !ShowMetrics;
                break;
            case "chiSquare":
                ShowChiSquare = !ShowChiSquare;
                break;
            case "trace":
                ShowTrace = !ShowTrace;
                break;
            case "dataPreview":
                ShowDataPreview = !ShowDataPreview;
                break;
            case "utilisation":
                ShowUtilisation = !ShowUtilisation;
                break;
            case "queueLength":
                ShowQueueLength = !ShowQueueLength;
                break;
            case "waitHistogram":
                ShowWaitHistogram = !ShowWaitHistogram;
                break;
        }
    }

    /// <summary>Raised so the parent can persist widget visibility when it changes (FR-UI-14).</summary>
    public event EventHandler? WidgetVisibilityChanged;

    private void OnWidgetVisibilityChanged()
    {
        var visible = new List<string>();
        if (ShowMetrics) { visible.Add("metrics"); }
        if (ShowChiSquare) { visible.Add("chiSquare"); }
        if (ShowTrace) { visible.Add("trace"); }
        if (ShowDataPreview) { visible.Add("dataPreview"); }
        if (ShowUtilisation) { visible.Add("utilisation"); }
        if (ShowQueueLength) { visible.Add("queueLength"); }
        if (ShowWaitHistogram) { visible.Add("waitHistogram"); }

        VisibleWidgets = visible;
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
        if (_preferences is not null)
        {
            _preferences.VisibleWidgets = visible;
            _preferences.Save();
        }
    }

    /// <summary>Widget keys currently visible (mirrors the Show flags).</summary>
    public IReadOnlyList<string> VisibleWidgets { get; private set; } =
        new[] { "metrics", "chiSquare", "trace", "utilisation", "queueLength", "waitHistogram" };
}