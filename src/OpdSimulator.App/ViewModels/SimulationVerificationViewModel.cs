using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Engine;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// One chart card of the Simulation verification widget (Phase 8B): the heading,
/// the verdict caption line and the already-built chart control — the same shape
/// as <see cref="InputAnalysisChartViewModel"/> so the widget reuses the
/// <see cref="OpdSimulator.App.Controls.ChartCard"/> contract. The control is
/// built on the UI thread by <see cref="ChartControlBuilder"/> and stored here
/// for the card to bind to (G5).
/// </summary>
/// <param name="title">Card heading, e.g. "Inter-arrival time (minutes)".</param>
/// <param name="caption">Verdict line, or a note when no verdict could be computed.</param>
/// <param name="hasSeries">False when no fit ran — the card shows its empty state.</param>
/// <param name="chartContent">The chart control, or null when <paramref name="hasSeries"/> is false.</param>
public sealed class VerificationChartViewModel(
    string title,
    string? caption,
    bool hasSeries,
    Control? chartContent)
{
    /// <summary>Card heading, e.g. "Inter-arrival time (minutes)".</summary>
    public string Title { get; } = title;

    /// <summary>Verdict caption line (χ² / p / decision), or a note when none exists.</summary>
    public string? Caption { get; } = caption;

    /// <summary>True when the card has a drawable histogram; false shows the card's empty state.</summary>
    public bool HasSeries { get; } = hasSeries;

    /// <summary>Inverse of <see cref="HasSeries"/>, bound to the card's <c>ShowEmptyState</c>.</summary>
    public bool ShowEmptyState => !HasSeries;

    /// <summary>The chart control hosted inside the card (null on the empty state).</summary>
    public Control? ChartContent { get; } = chartContent;
}

/// <summary>
/// Drives the Simulation verification widget on the Results panel (Phase 8B):
/// an empty state until a simulation run finishes, then one chart card per
/// series (inter-arrival plus per stage) chi-square testing the engine's own
/// generated output samples against the configured distribution (Banks / Law
/// &amp; Kelton model verification). Chart data is prepared on the background
/// thread; the chart controls themselves are created on the UI thread (G5) —
/// mirroring <see cref="InputAnalysisViewModel"/>.
/// </summary>
/// <remarks>
/// A generation counter guards against a stale background apply overwriting a
/// newer one: every <see cref="ApplyAsync"/> bumps the generation and only the
/// apply posted by the most recent generation is applied. The synchronous
/// <see cref="Apply"/> path exists for wiring/tests.
/// </remarks>
public partial class SimulationVerificationViewModel : ObservableObject
{
    /// <summary>
    /// The exact wording of the widget's empty state (shown until a simulation
    /// run finishes); asserted verbatim by the UI test.
    /// </summary>
    public string EmptyMessage { get; } = "Run a simulation to verify its output.";

    /// <summary>True while no run has been verified yet; false once a run's series cards are populated.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Inverse of <see cref="IsEmpty"/>, provided so the view can bind without a negating converter.</summary>
    public bool HasCharts => !IsEmpty;

    /// <summary>The chart cards shown after a verified run, in series order (inter-arrival, then stages).</summary>
    public System.Collections.ObjectModel.ObservableCollection<VerificationChartViewModel> Charts { get; } = new();

    private int _generation;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasCharts));

    /// <summary>
    /// Verifies a finished run on the background thread (G5): the chi-square
    /// number crunching happens off the UI thread, then the card view models —
    /// including their LiveCharts controls — are built on the UI thread.
    /// Thread-safe: call from the UI thread.
    /// </summary>
    /// <param name="result">The completed engine result, or null when the run was refused.</param>
    /// <param name="arrivalFamily">Configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">Configured service family per stage.</param>
    /// <param name="alpha">Significance level the verdicts are decided at.</param>
    public void ApplyAsync(
        SimulationResult? result,
        string arrivalFamily,
        System.Collections.Generic.IReadOnlyList<string> serviceFamilies,
        double alpha)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        int generation = ++_generation;
        Task.Run(() =>
        {
            var prepared = SimulationVerificationService.VerifyAll(result, arrivalFamily, serviceFamilies, alpha);
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation)
                {
                    ApplyPrepared(prepared);
                }
                else
                {
                    Serilog.Log.Warning("Simulation verification refresh superseded by a newer apply; dropping stale chart data");
                }
            });
        });
    }

    /// <summary>
    /// Synchronous verification used by tests and immediate wiring paths:
    /// computes the reports and builds the cards on the caller's thread.
    /// </summary>
    public void Apply(
        SimulationResult? result,
        string arrivalFamily,
        System.Collections.Generic.IReadOnlyList<string> serviceFamilies,
        double alpha)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        ++_generation;
        ApplyPrepared(SimulationVerificationService.VerifyAll(result, arrivalFamily, serviceFamilies, alpha));
    }

    /// <summary>
    /// Applies already-prepared reports onto the cards (UI thread). Also
    /// supersedes any in-flight background apply so the sync path wins.
    /// </summary>
    public void ApplyPrepared(System.Collections.Generic.IReadOnlyList<VerificationReport> reports)
    {
        ++_generation;
        Charts.Clear();
        foreach (var report in reports)
        {
            string? caption = report.ChiSquare is not null
                ? report.Histogram.Caption
                : report.Note is not null
                    ? $"{report.Note} ({report.SampleCount} samples)"
                    : $"{report.SampleCount} samples";

            Charts.Add(new VerificationChartViewModel(
                report.Histogram.Title,
                caption,
                report.Histogram.HasSeries,
                ChartControlBuilder.Build(report.Histogram)));
        }

        IsEmpty = Charts.Count == 0;
    }

    /// <summary>Returns the widget to its empty state (Clear All, or a refused run).</summary>
    public void Clear()
    {
        ++_generation;
        Charts.Clear();
        IsEmpty = true;
    }
}