namespace OpdSimulator.App.ViewModels;

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using Serilog;

/// <summary>
/// Drives the Input Analysis tab (Phase 6C). The tab is an empty state until a
/// data file is uploaded and analysed; when one is, it hosts the P1 input
/// charts — histogram + fitted PDF overlay (6c.2), chi-square observed-vs-
/// expected bars (6c.3), and the per-server utilisation chart (6c.4) — each
/// in its own <see cref="OpdSimulator.App.Controls.ChartCard"/>. Chart data is
/// prepared on the background thread; the chart controls themselves are created
/// on the UI thread (G5).
/// </summary>
/// <remarks>
/// A generation counter guards against a stale background apply overwriting a
/// newer one: every <see cref="ApplyAsync"/> bumps the generation and only the
/// apply posted by the most recent generation is applied. The synchronous
/// <see cref="Apply"/> path exists for wiring/tests; chart controls are tiny
/// numbers of small LiveCharts controls, so building them on the caller's
/// thread is safe.
/// </remarks>
public partial class InputAnalysisViewModel : ObservableObject
{
    /// <summary>
    /// The exact wording of the tab's empty state (shown until a data file is
    /// loaded); asserted verbatim by the UI test.
    /// </summary>
    public string EmptyMessage { get; } = "Load a data file to see fit analysis.";

    /// <summary>
    /// True while no analysed data exists yet; false once a usable data file
    /// is loaded and the chart cards are populated.
    /// </summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>
    /// Inverse of <see cref="IsEmpty"/>, provided so the view can bind the
    /// chart stack without a negating converter.
    /// </summary>
    public bool HasData => !IsEmpty;

    /// <summary>The chart cards shown in the "has data" state, in fit order.</summary>
    public ObservableCollection<InputAnalysisChartViewModel> Charts { get; } = new();

    private int _generation;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasData));

    /// <summary>
    /// Refreshes the charts from the current binding and distribution choices
    /// on the background thread (G5): number crunching happens off the UI
    /// thread, then the card view models — including their LiveCharts controls —
    /// are built on the UI thread. Thread-safe: call from the UI thread.
    /// </summary>
    public void ApplyAsync(
        DataBindingResult? binding,
        string interArrivalFamily,
        string serviceFamily,
        double alpha)
    {
        int generation = ++_generation;
        Task.Run(() =>
        {
            var prepared = Prepare(binding, interArrivalFamily, serviceFamily, alpha);
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation)
                {
                    ApplyPrepared(prepared);
                }
                else
                {
                    Log.Warning("Input Analysis refresh superseded by a newer apply; dropping stale chart data");
                }
            });
        });
    }

    /// <summary>
    /// Synchronous refresh used by tests and for immediate updates: computes the
    /// chart data and builds the cards on the caller's thread (the UI thread in
    /// production wiring paths).
    /// </summary>
    public void Apply(
        DataBindingResult? binding,
        string interArrivalFamily,
        string serviceFamily,
        double alpha)
    {
        ++_generation;
        ApplyPrepared(Prepare(binding, interArrivalFamily, serviceFamily, alpha));
    }

    /// <summary>
    /// Applies already-prepared chart data onto the cards (UI thread). Also
    /// supersedes any in-flight background apply so the sync path wins.
    /// </summary>
    public void ApplyPrepared(IReadOnlyList<HistogramChartData> charts)
    {
        ++_generation;
        Charts.Clear();
        foreach (var data in charts)
        {
            var chart = ChartControlBuilder.Build(data);
            Charts.Add(new InputAnalysisChartViewModel(data.Title, data.Caption, data.HasSeries, chart));
        }
        IsEmpty = Charts.Count == 0;
    }

    /// <summary>Pure, backgroundable projection: fits + histogram data, no UI types.</summary>
    private static IReadOnlyList<HistogramChartData> Prepare(
        DataBindingResult? binding,
        string interArrivalFamily,
        string serviceFamily,
        double alpha)
    {
        var charts = new List<HistogramChartData>();
        foreach (var fit in InputAnalysisService.FitAll(binding, interArrivalFamily, serviceFamily, alpha))
        {
            charts.Add(InputAnalysisService.BuildHistogram(fit));
        }
        return charts;
    }
}