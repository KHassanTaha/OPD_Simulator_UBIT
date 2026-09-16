namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
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
/// run starts, then the chosen widgets — metrics table, chi-square table, the
/// scrollable event trace, the data preview — plus the run-refusal banner with
/// its clean message (G3/G4, 5-F). Widget visibility follows
/// <see cref="WidgetPreferences"/> (FR-UI-14), seeded to all-on.
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
        ShowMetrics = visible.Contains("metrics");
        ShowChiSquare = visible.Contains("chiSquare");
        ShowTrace = visible.Contains("trace");
        ShowDataPreview = visible.Contains("dataPreview");
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
        SystemMetrics.Clear();
        StageRows.Clear();
        ChiSquareRows.Clear();
        PreviewColumnTitles = null;
        PreviewRows = null;
        PreviewInvalidRows = null;
        PreviewError = null;
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

    // ── Widget visibility (FR-UI-14, persisted via WidgetPreferences) ─────

    [ObservableProperty]
    private bool _showMetrics = true;

    [ObservableProperty]
    private bool _showChiSquare = true;

    [ObservableProperty]
    private bool _showTrace = true;

    [ObservableProperty]
    private bool _showDataPreview;

    partial void OnShowMetricsChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowChiSquareChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowTraceChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowDataPreviewChanged(bool value) => OnWidgetVisibilityChanged();

    /// <summary>Programmatic toggle used by tests and presets (the strip uses TwoWay binds).</summary>
    /// <param name="key">The widget key ("metrics", "chiSquare", "trace", "dataPreview").</param>
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

        VisibleWidgets = visible;
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
        if (_preferences is not null)
        {
            _preferences.VisibleWidgets = visible;
            _preferences.Save();
        }
    }

    /// <summary>Widget keys currently visible (mirrors the four Show flags).</summary>
    public IReadOnlyList<string> VisibleWidgets { get; private set; } =
        new[] { "metrics", "chiSquare", "trace" };
}