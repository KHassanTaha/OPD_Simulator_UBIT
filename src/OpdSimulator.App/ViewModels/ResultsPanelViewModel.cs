namespace OpdSimulator.App.ViewModels;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;

/// <summary>One metrics-table label/value pair (FR-STAT-6).</summary>
/// <param name="Label">Human metric name.</param>
/// <param name="Value">Formatted metric value.</param>
public sealed record MetricRow(string Label, string Value);



/// <summary>One per-stage row of the results metrics table (FR-STAT-6/7).</summary>
/// <param name="SerialNumber">
/// The stage's 1-based position in the run's stage list, shown as its own
/// column. Added in Phase 8Q.3 under the 8Q.5 rule that every listing table
/// carries one, so a row can be cited unambiguously in the viva ("stage 2")
/// without the reader counting columns or rows.
/// </param>
public sealed record StageMetricRow(
    string SerialNumber,
    string StageName,
    string ArrivalRate,
    string Servers,
    string ServiceRate,
    string Rho,
    string Served,
    string Wait,
    string Queue,
    string Utilisation,
    string BacklogAtClose,
    string DrainMinutes);

/// <summary>
/// One stage's close-of-session row: what was left waiting when arrivals stopped,
/// and how long that stage took to clear it (D-191).
/// </summary>
/// <param name="SerialNumber">1-based stage position, so a row can be cited in the viva.</param>
/// <param name="StageName">The stage the row describes.</param>
/// <param name="BacklogAtClose">Patients still waiting or in service at the close of arrivals.</param>
/// <param name="DrainMinutes">Minutes from that close until this stage's last service completion.</param>
/// <param name="Basis">
/// "single session" or "average of N sessions" — which figure the two columns hold,
/// so a multi-day average is never mistaken for one day's backlog.
/// </param>
public sealed record BacklogDrainRow(
    string SerialNumber,
    string StageName,
    string BacklogAtClose,
    string DrainMinutes,
    string Basis);

/// <summary>
/// One stage's stability verdict (Phase 8Q.3, D-183).
/// </summary>
/// <param name="StageName">The stage the verdict describes.</param>
/// <param name="Rho">Traffic intensity as displayed, so the row reads against the table above it.</param>
/// <param name="Verdict">Plain-language band name — the cue that carries the meaning.</param>
/// <param name="BandBrush">
/// Theme brush for the band, from <see cref="StabilityBandPalette"/>. Carried on
/// the row rather than bound through three booleans and a style block, matching
/// how <see cref="StageLegendItem"/> already carries its swatch, and so the
/// colour is assertable in a test instead of only visible on screen.
/// </param>
/// <remarks>
/// The verdict text is rendered alongside the colour because colour alone is not
/// an accessible signal (AGENTS §16.9).
/// </remarks>
public sealed record StabilityRow(
    string StageName,
    string Rho,
    string Verdict,
    IBrush BandBrush);

/// <summary>
/// One stage-colour legend entry (Phase 8M, D-163, FR-UI-27). The legend is
/// generated from the run's stages, so it cannot drift from the charts the way a
/// hand-written list would.
/// </summary>
/// <param name="StageName">Stage label, as the run reported it.</param>
/// <param name="Swatch">The stage's shared palette brush.</param>
/// <param name="StageIndex">The stage's position, matching the palette index.</param>
public sealed record StageLegendItem(string StageName, IBrush Swatch, int StageIndex);

/// <summary>One chi-square goodness-of-fit verdict row (FR-STAT-8).</summary>
/// <param name="SerialNumber">
/// The verdict's 1-based position, shown as its own column (FR-UI-36) so a row
/// can be cited unambiguously ("verdict 2") without the reader counting rows.
/// </param>
public sealed record ChiSquareRow(
    string SerialNumber,
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
/// (Phase 6c.5), chi-square table and the scrollable event trace — plus the
/// run-refusal banner with its clean message (G3/G4, 5-F). Widget visibility
/// follows <see cref="WidgetPreferences"/> (FR-UI-14), seeded to all-on.
/// Phase 7D removed the data-preview widget: the preview now lives on the
/// Input tab. The Simulation verification (Phase 8B) and Analytical validation
/// (Phase 8C) widgets bind shared view models owned by the window.
/// </summary>
public partial class ResultsPanelViewModel : ObservableObject
{
    private const string Unavailable = "—";

    private readonly WidgetPreferences? _preferences;

    /// <summary>Parameters of the most recent run, for the calculations dialog (D-164).</summary>
    private SimulationParameters? _lastParameters;

    /// <summary>Human description of where those parameters came from (D-164).</summary>
    private string? _parameterSource;

    /// <summary>
    /// The data binding behind the most recent run, so the calculations dialog can
    /// print both arrival-rate estimates and the one the run used (D-173).
    /// </summary>
    private DataBindingResult? _lastBinding;

    /// <summary>Direct-to-Doctor probability the last run actually used (D-179).</summary>
    private double _effectiveBypassProbability;

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
            || !visible.Contains("waitHistogram")
            || !visible.Contains("simulationVerification")
            || !visible.Contains("analyticalValidation"))
        {
            // Widget migrations: a ui.json written before the utilisation (6c.4),
            // queue-length / waiting-time (6c.5), simulation-verification
            // (Phase 8B) and analytical-validation (Phase 8C) widgets existed
            // must not hide them forever. Add each missing key once and persist,
            // so every new widget starts visible like the others. A deliberate
            // later-off is re-enabled once — same accepted behaviour as the 6c.4
            // migration.
            if (!visible.Contains("utilisation")) { visible.Add("utilisation"); migrated = true; }
            if (!visible.Contains("queueLength")) { visible.Add("queueLength"); migrated = true; }
            if (!visible.Contains("waitHistogram")) { visible.Add("waitHistogram"); migrated = true; }
            if (!visible.Contains("simulationVerification")) { visible.Add("simulationVerification"); migrated = true; }
            if (!visible.Contains("analyticalValidation")) { visible.Add("analyticalValidation"); migrated = true; }
            if (migrated) { _preferences.Save(); }
        }

        ShowMetrics = visible.Contains("metrics");
        ShowChiSquare = visible.Contains("chiSquare");

        // "trace" is read but deliberately NOT honoured. An existing ui.json still
        // carries the key, and FR-UI-35 makes the trace permanently visible, so a
        // user who once switched it off must still get it back. The key is not
        // dropped from the loaded list here either — it is filtered out on write,
        // so the file heals itself the next time any widget toggles.
        ShowTrace = true;
        ShowUtilisation = visible.Contains("utilisation");
        ShowQueueLength = visible.Contains("queueLength");
        ShowWaitHistogram = visible.Contains("waitHistogram");
        ShowSimulationVerification = visible.Contains("simulationVerification");
        ShowAnalyticalValidation = visible.Contains("analyticalValidation");
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
    /// <param name="outcome">The coordinator's outcome for this run.</param>
    /// <param name="parameters">
    /// The parameters the run was launched with. Optional so the refusal path and
    /// existing callers stay source-compatible; the calculations dialog (D-164)
    /// needs them to write down λ, μ and p_exit, and a run without them still
    /// shows every result-derived figure.
    /// </param>
    /// <param name="sourceDescription">
    /// Where the parameters came from, in the user's terms (fitted from a file vs
    /// entered manually), so the calculations dialog can name its own provenance.
    /// </param>
    public void CompleteRun(
        RunOutcome outcome,
        SimulationParameters? parameters = null,
        string? sourceDescription = null,
        DataBindingResult? binding = null)
    {
        _lastParameters = parameters;
        _parameterSource = sourceDescription;

        // D-173: the calculations dialog prints BOTH λ estimates and names the one
        // the run used. It needs the binding that produced them, so the binding is
        // captured with the parameters — the run's own inputs, not a later state
        // the user may have changed since.
        _lastBinding = binding;
        OnPropertyChanged(nameof(CalculationsText));
        OnPropertyChanged(nameof(CalculationsRows));
        IsBusy = false;
        HasRun = true;
        IsWelcomeVisible = false;
        StatusText = string.Empty;

        RunError = outcome.Error;
        RunSummary = outcome.Error is null
            ? "Run complete"
            : "The run was refused before it started";
        SessionSummaryText = outcome.Result is null
            ? string.Empty
            : BuildSessionSummary(outcome.Result.Sessions);
        OnPropertyChanged(nameof(HasSessionSummary));
        EffectiveExitText = $"Effective exit probability (after Screening): {outcome.EffectiveExitProbability:0.###}";
        _effectiveBypassProbability = outcome.EffectiveBypassProbability;
        OnPropertyChanged(nameof(EffectiveBypassText));
        OnPropertyChanged(nameof(CalculationsText));
        OnPropertyChanged(nameof(CalculationsRows));

        ChiSquareRows.Clear();
        for (var fitIndex = 0; fitIndex < outcome.Fits.Count; fitIndex++)
        {
            var fit = outcome.Fits[fitIndex];
            // The serial numbers the displayed rows, so they stay contiguous even
            // when a fit is absent and renders as "fit unavailable".
            var serial = (fitIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ChiSquareRows.Add(fit.ChiSquare is { } cs && fit.Fitted is not null
                ? new ChiSquareRow(serial, fit.Label, fit.Fitted.Name, $"{cs.Statistic:0.###}", $"{cs.DegreesOfFreedom}",
                    $"{cs.PValue:0.###}", cs.Decision)
                : new ChiSquareRow(serial, fit.Label, "fit unavailable", Unavailable, Unavailable, Unavailable, Unavailable));
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
        SessionSummaryText = string.Empty;
        OnPropertyChanged(nameof(HasSessionSummary));
        EffectiveExitText = string.Empty;
        _effectiveBypassProbability = 0.0;
        OnPropertyChanged(nameof(EffectiveBypassText));
        RunError = null;
        TraceText = string.Empty;
        ChiSquareCaption = DefaultChiSquareCaption;
        SystemMetrics.Clear();
        StageRows.Clear();
        BacklogDrainRows.Clear();
        TotalDrainText = string.Empty;
        StabilityRows.Clear();
        BottleneckText = string.Empty;
        ChiSquareRows.Clear();
        UtilisationChart = null;
        QueueLengthChart = null;
        WaitHistogram = null;
        WaitHistogramChart = null;
        WaitStageNames = null;
        SelectedWaitStage = null;
        IsWaitLogScale = false;
        _lastResult = null;
        _lastBinding = null;
        ApplyPreferences();
    }

    private void SetMetrics(SimulationResult? result)
    {
        SystemMetrics.Clear();
        StageRows.Clear();
        BacklogDrainRows.Clear();
        TotalDrainText = string.Empty;
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

        StabilityRows.Clear();
        BottleneckText = string.Empty;

        var serial = 1;
        foreach (var stage in result.StageMetrics)
        {
            StageRows.Add(new StageMetricRow(
                $"{serial++}",
                stage.StageName,
                N0(stage.ArrivalRate),
                $"{stage.ServerCount}",
                N0(stage.ServiceRate),
                N0(stage.Rho),
                $"{stage.PatientsServed}",
                N0(stage.AverageWaitMinutes),
                N0(stage.AverageQueueLength),
                $"{stage.StageUtilisation:0.##}",
                $"{stage.BacklogAtClose}",
                N0(stage.DrainMinutes)));
        }

        SetBacklogAndDrain(result);
        SetStability(result);
    }

    /// <summary>
    /// Builds the close-of-session rows and the total-drain summary (D-191).
    /// </summary>
    /// <remarks>
    /// A multi-day run's per-stage figures are for its final day, which on their own
    /// would understate a week. So when the engine reported a per-day series, the
    /// table switches to the average across sessions and says so in the Basis
    /// column — a number whose basis is unstated is a number a reader cannot trust.
    /// The total drain is always the slowest stage, because the system is only empty
    /// once its slowest stage is empty.
    /// </remarks>
    private void SetBacklogAndDrain(SimulationResult result)
    {
        BacklogDrainRows.Clear();

        // Every stage is averaged over ITS OWN sessions (D-193). Averaging the
        // system-wide series and printing it on each row — which is what 8R.0 did —
        // made every row identical in exactly the multi-day runs where a per-stage
        // backlog is the figure worth reading.
        int sessionCount = result.StageMetrics.Count == 0
            ? 0
            : result.StageMetrics.Max(st => Math.Max(
                st.BacklogAtCloseBySession.Count, st.DrainMinutesBySession.Count));

        bool multiDay = sessionCount > 1;
        string basis = multiDay
            ? $"average of {sessionCount} sessions"
            : "single session";

        var serial = 1;
        foreach (var stage in result.StageMetrics)
        {
            // A stage with no series (an older result, or a horizon run) falls back
            // to its scalar, which is that run's single session.
            string backlog = multiDay
                ? $"{stage.BacklogAtCloseBySession.DefaultIfEmpty(stage.BacklogAtClose).Average():0.#}"
                : $"{stage.BacklogAtClose}";
            string drain = multiDay
                ? $"{stage.DrainMinutesBySession.DefaultIfEmpty(stage.DrainMinutes).Average():0.#}"
                : N0(stage.DrainMinutes);
            BacklogDrainRows.Add(new BacklogDrainRow($"{serial++}", stage.StageName, backlog, drain, basis));
        }

        // The whole system is empty when its slowest stage is empty, so the total
        // drain is the largest per-stage drain — max(), never a sum.
        double totalDrain = result.StageMetrics.Count == 0
            ? 0
            : result.StageMetrics.Max(s => s.DrainMinutes);
        TotalDrainText = multiDay
            ? $"{totalDrain:0.#} min to clear the last patient after a session closed (slowest stage, final session)"
            : $"{totalDrain:0.#} min to clear the last patient after the session closed (slowest stage)";
    }

    /// <summary>
    /// Builds the stability rows and names the bottleneck (Phase 8Q.3, D-183).
    /// </summary>
    /// <remarks>
    /// The verdict itself comes from <see cref="StabilityClassifier"/>, a pure
    /// function in Core, so the thresholds are stated in exactly one place and
    /// are testable without a display. This method only renders what it returns.
    /// </remarks>
    private void SetStability(SimulationResult result)
    {
        var rhos = result.StageMetrics
            .Select(m => (m.StageName, m.Rho))
            .ToList();

        if (rhos.Count == 0)
        {
            return;
        }

        foreach (var (stageName, rho) in rhos)
        {
            var band = StabilityClassifier.Classify(rho);
            StabilityRows.Add(new StabilityRow(
                stageName,
                N0(rho),
                Describe(band),
                StabilityBandPalette.BrushFor(band)));
        }

        var (worstBand, bottleneck) = StabilityClassifier.ClassifySet(rhos);

        // The bottleneck is named with its ρ, not alone: "Screening" is only
        // half an answer, and the number beside it is what a reader checks
        // against the table above (D-176).
        BottleneckText = $"{bottleneck} — ρ {N0(rhos.First(r => r.StageName == bottleneck).Rho)} ({Describe(worstBand)})";
    }

    /// <summary>
    /// Plain-language name for a band. Chosen over the bare band name because
    /// "Amber" on its own says nothing to a reader who has not memorised the
    /// thresholds, whereas the sentence states the consequence.
    /// </summary>
    private static string Describe(StabilityBand band) => band switch
    {
        StabilityBand.Green => "Stable",
        StabilityBand.Amber => "Near capacity",
        _ => "Unstable",
    };

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
            BuildStageLegend(null);
            return;
        }

        var data = UtilisationChartService.Build(result);
        BuildStageLegend(result);
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
        var previous = _lastResult;
        _lastResult = result;
        if (!ReferenceEquals(previous, result))
        {
            // The calculations body is derived from the result, so the button's
            // enabled state and its text both move with a new run.
            OnPropertyChanged(nameof(CalculationsText));
        OnPropertyChanged(nameof(CalculationsRows));
            OnPropertyChanged(nameof(HasCalculations));
        }

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

    /// <summary>
    /// Caption naming the operating sessions the run actually resolved, e.g.
    /// "4 operating sessions: Day 1 (Sat) · Day 2 (Mon) · Day 3 (Tue) · Day 4 (Wed)".
    /// Empty for a horizon run, which has no sessions.
    /// </summary>
    /// <remarks>
    /// A user who asks for 4 days and gets four sessions on four different weekdays
    /// has no other way to confirm which four — the run summary used to say only
    /// "Run complete", which is precisely the ambiguity FR-SIM-12 exists to remove.
    /// Long horizons are abbreviated after five sessions so the header cannot wrap
    /// into a paragraph.
    /// </remarks>
    [ObservableProperty]
    private string _sessionSummaryText = string.Empty;

    /// <summary>True when <see cref="SessionSummaryText"/> has something to say.</summary>
    public bool HasSessionSummary => !string.IsNullOrEmpty(SessionSummaryText);

    /// <summary>
    /// Builds the session caption from the resolved session list: the count, then
    /// each session as "Day n (Weekday)" — the same label the per-session totals
    /// table uses (FR-UI-38), abbreviated past five sessions.
    /// </summary>
    /// <param name="sessions">The run's resolved operating sessions; empty for a
    /// horizon run.</param>
    /// <returns>The caption text, or an empty string when there are no sessions.</returns>
    public static string BuildSessionSummary(IReadOnlyList<ClinicSession> sessions)
    {
        if (sessions.Count == 0)
            return string.Empty;

        const int named = 5;
        string label(ClinicSession session) => $"Day {session.Ordinal} ({session.DayOfWeek.ToString()[..3]})";

        var shown = sessions.Take(named).Select(label);
        string list = string.Join(" · ", shown);
        if (sessions.Count > named)
            list += $" · … Day {sessions[^1].Ordinal} ({sessions[^1].DayOfWeek.ToString()[..3]})";

        string unit = sessions.Count == 1 ? "1 operating session" : $"{sessions.Count} operating sessions";
        return $"{unit}: {list}";
    }

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

    /// <summary>
    /// Per-stage close-of-session rows: backlog left when arrivals stopped, and how
    /// long the stage took to clear it (D-191).
    /// </summary>
    public ObservableCollection<BacklogDrainRow> BacklogDrainRows { get; } = new();

    /// <summary>
    /// One-line total drain — the slowest stage's drain, because the system is only
    /// empty once its slowest stage is empty. Empty before a run (D-191).
    /// </summary>
    public string TotalDrainText { get; private set; } = string.Empty;

    /// <summary>Per-stage stability verdicts, shown once a run exists (Phase 8Q.3, D-183).</summary>
    public ObservableCollection<StabilityRow> StabilityRows { get; } = new();

    /// <summary>
    /// The stage closest to saturation, named with its ρ. Empty until a run
    /// finishes, which is also what hides the whole stability block.
    /// </summary>
    public string BottleneckText { get; private set; } = string.Empty;

    /// <summary>True once a run has produced at least one stage to judge.</summary>
    public bool HasStability => StabilityRows.Count > 0;

    /// <summary>Label plus value for the bottleneck line, or empty before a run.</summary>
    public string BottleneckCaption =>
        string.IsNullOrEmpty(BottleneckText) ? string.Empty : $"Bottleneck: {BottleneckText}";

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
    /// <remarks>
    /// Paired with <see cref="OnTraceTextChanged"/>, which is not optional. The view
    /// binds <see cref="TraceBody"/>, not this property, and <c>TraceBody</c> is
    /// computed from this one — so raising only <c>TraceText</c> leaves the binding
    /// holding the value it read at launch (D-194).
    /// </remarks>
    [ObservableProperty]
    private string _traceText = string.Empty;

    /// <summary>
    /// Re-raises <see cref="TraceBody"/> whenever <see cref="TraceText"/> changes.
    /// </summary>
    /// <remarks>
    /// <c>TraceBody</c> is a computed get-only property, so the
    /// <c>[ObservableProperty]</c> generator has no field to attach to and no way to
    /// know a binding depends on it. Without this the trace widget keeps rendering
    /// "No trace was recorded for this run." after a run that recorded 9,000+
    /// characters — the recorder worked, the screen never updated.
    /// </remarks>
    partial void OnTraceTextChanged(string value) =>
        OnPropertyChanged(nameof(TraceBody));

    /// <summary>Body of the pinned trace panel, or the reason it is empty.</summary>
    /// <remarks>
    /// A permanently visible box that says nothing reads as a broken panel, and a
    /// refused run legitimately produces no trace lines. Rather than leave 240 px
    /// of blank space, the empty case states why — AGENTS §16.1: the user always
    /// knows why something is blank.
    /// </remarks>
    public string TraceBody
        => string.IsNullOrWhiteSpace(TraceText)
            ? "No trace was recorded for this run."
            : TraceText;

    /// <summary>
    /// Fixed caption under the "Event Trace" heading (FR-UI-35). Fixed, not
    /// derived from the run, because the panel itself never varies — only its
    /// contents do, and <see cref="TraceBody"/> is where that is said.
    /// </summary>
    public const string TracePanelCaption =
        "Every event of the last run, in order. Scrolls within this box.";

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

    /// <summary>
    /// Formats a fraction as a percentage with two decimals in the invariant
    /// culture, matching the rest of the results panel.
    /// </summary>
    private static string FormatPercent(double fraction) =>
        fraction.ToString("P2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Rebuilds the stage legend from a run's own stage list, each stage painted
    /// with the colour every chart gave it. A null result clears it.
    /// </summary>
    private void BuildStageLegend(OpdSimulator.Core.Engine.SimulationResult? result)
    {
        if (result is null)
        {
            StageLegend = Array.Empty<StageLegendItem>();
            return;
        }

        var items = new List<StageLegendItem>();
        int index = 0;
        foreach (var stage in result.StageMetrics)
        {
            items.Add(new StageLegendItem(
                stage.StageName,
                StageColourPalette.BrushForStageIndex(index),
                index));
            index++;
        }

        StageLegend = items;
    }



    /// <summary>
    /// The stage-colour legend (Phase 8M, D-163, FR-UI-27), generated from the
    /// run's own stage list so it always matches the charts beside it.
    /// </summary>
    public IReadOnlyList<StageLegendItem> StageLegend { get; private set; } =
        Array.Empty<StageLegendItem>();

    /// <summary>
    /// The full "View calculations" body (Phase 8M, D-164, FR-UI-29), rendered by
    /// the pure <see cref="CalculationsTextBuilder"/> from the finished result and
    /// the parameters that produced it.
    /// </summary>
    public string CalculationsText => CalculationsTextBuilder.Build(
        _lastResult, _lastParameters, _parameterSource, _lastBinding, _effectiveBypassProbability);

    /// <summary>
    /// The same calculations as <see cref="CalculationsText"/>, structured for the
    /// dialog's two-column render (Phase 8N, D-165). Both properties read the same
    /// three fields, so the on-screen rows and the copied text cannot disagree.
    /// </summary>
    public IReadOnlyList<CalculationRow> CalculationsRows =>
        CalculationsTextBuilder.BuildRows(_lastResult, _lastParameters, _parameterSource, _lastBinding, _effectiveBypassProbability);

    /// <summary>True once a run has produced a result, so the button has something to show.</summary>
    public bool HasCalculations => _lastResult is not null;

    /// <summary>
    /// The direct-to-Doctor probability the finished run actually used (D-179).
    /// </summary>
    /// <remarks>
    /// Read back from the outcome rather than recomputed, because the coordinator
    /// can normalise it away: a network with fewer than three stages has no stage
    /// to skip, so the topology runs with bypass off whatever the file measured.
    /// Showing the measured value here would tell the user their data was applied
    /// when it was not.
    /// </remarks>
    public string EffectiveBypassText =>
        $"Effective bypass probability (Reception → Doctor): {_effectiveBypassProbability:0.###}";

    /// <summary>
    /// Raised when the user asks to see the calculations. The VIEW opens the
    /// themed dialog, matching how every other dialog in this app is opened
    /// (a view model announces intent; it never builds a window itself).
    /// </summary>
    public event EventHandler? CalculationsRequested;

    /// <summary>Button: "View Calculations" in the Overview heading row (D-164).</summary>
    [RelayCommand]
    private void ShowCalculations() => CalculationsRequested?.Invoke(this, EventArgs.Empty);

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
    private bool _showUtilisation = true;

    [ObservableProperty]
    private bool _showQueueLength = true;

    [ObservableProperty]
    private bool _showWaitHistogram = true;

    /// <summary>
    /// The Simulation verification widget state (Phase 8B): the engine-output
    /// chi-square cards. Shared with the window-level view model — MainViewModel
    /// owns the single instance and hands it here so the Results XAML can bind
    /// it under this panel's DataContext. A refused run leaves it empty.
    /// </summary>
    public SimulationVerificationViewModel SimulationVerification { get; set; } = new();

    /// <summary>True when the Simulation verification widget card is visible (FR-UI-14).</summary>
    [ObservableProperty]
    private bool _showSimulationVerification = true;

    /// <summary>
    /// The Analytical validation widget state (Phase 8C): the simulated-vs-M/M/c
    /// comparison table. Shared with the window-level view model — MainViewModel
    /// owns the single instance and hands it here so the Results XAML can bind
    /// it under this panel's DataContext. A refused or non-comparable run leaves
    /// it empty.
    /// </summary>
    public AnalyticalValidationViewModel AnalyticalValidation { get; set; } = new();

    /// <summary>True when the Analytical validation widget card is visible (FR-UI-14).</summary>
    [ObservableProperty]
    private bool _showAnalyticalValidation = true;

    partial void OnShowMetricsChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowChiSquareChanged(bool value) => OnWidgetVisibilityChanged();

    // ShowTrace is retained only so persisted data naming "trace" still deserialises
    // (see the constructor). It is forced true and drives no visibility, so its
    // change hook must NOT write preferences — doing so would re-add the key this
    // phase is removing.

    partial void OnShowUtilisationChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowQueueLengthChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowWaitHistogramChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowSimulationVerificationChanged(bool value) => OnWidgetVisibilityChanged();

    partial void OnShowAnalyticalValidationChanged(bool value) => OnWidgetVisibilityChanged();

    /// <summary>Programmatic toggle used by tests and presets (the strip uses TwoWay binds).</summary>
    /// <param name="key">The widget key ("metrics", "chiSquare", "utilisation", "queueLength", "waitHistogram", "simulationVerification", "analyticalValidation"). "trace" is not a key: the trace is pinned (FR-UI-35).</param>
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
            case "utilisation":
                ShowUtilisation = !ShowUtilisation;
                break;
            case "queueLength":
                ShowQueueLength = !ShowQueueLength;
                break;
            case "waitHistogram":
                ShowWaitHistogram = !ShowWaitHistogram;
                break;
            case "simulationVerification":
                ShowSimulationVerification = !ShowSimulationVerification;
                break;
            case "analyticalValidation":
                ShowAnalyticalValidation = !ShowAnalyticalValidation;
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
        if (ShowUtilisation) { visible.Add("utilisation"); }
        if (ShowQueueLength) { visible.Add("queueLength"); }
        if (ShowWaitHistogram) { visible.Add("waitHistogram"); }
        if (ShowSimulationVerification) { visible.Add("simulationVerification"); }
        if (ShowAnalyticalValidation) { visible.Add("analyticalValidation"); }

        VisibleWidgets = visible;
        WidgetVisibilityChanged?.Invoke(this, EventArgs.Empty);
        if (_preferences is not null)
        {
            _preferences.VisibleWidgets = visible;
            _preferences.Save();
        }
    }

    /// <summary>
    /// Widget keys currently visible (mirrors the Show flags). Seven, not eight:
    /// the trace is pinned and always on (FR-UI-35), so it is not a toggleable
    /// widget and does not appear here.
    /// </summary>
    public IReadOnlyList<string> VisibleWidgets { get; private set; } =
        new[] { "metrics", "chiSquare", "utilisation", "queueLength", "waitHistogram", "simulationVerification", "analyticalValidation" };
}