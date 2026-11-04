using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Drives the Analytical validation widget on the Results panel (Phase 8C,
/// FR-STAT-5; model selection Phase 8U, D-208): an empty state until a run
/// finishes, then a table comparing the simulated per-stage wait and queue
/// length against a closed-form steady-state model — M/M/c, M/G/c or G/G/c.
/// When the chosen model's inputs do not hold (an unstable stage, or a transient
/// run shorter than <see cref="AnalyticalValidationService.MinimumSteadyStateMinutes"/>)
/// the table stays empty and explains why, rather than showing an invalid
/// comparison.
/// </summary>
/// <remarks>
/// The closed-form numbers are computed off the UI thread by
/// <see cref="ApplyAsync"/> (same background-prep pattern as Phase 8B), with a
/// generation guard so a stale apply cannot overwrite a newer one. The
/// synchronous <see cref="Apply"/> path exists for tests and immediate wiring.
/// Changing <see cref="SelectedModel"/> re-evaluates the retained run without
/// asking the engine to run again.
/// </remarks>
public partial class AnalyticalValidationViewModel : ObservableObject
{
    /// <summary>
    /// The exact wording of the widget's empty state (shown until a run yields a
    /// valid steady-state comparison). It names every condition so the user
    /// understands why the widget is empty.
    /// </summary>
    public string EmptyMessage { get; } =
        "Analytical comparison requires a steady-state run — ρ < 1 at every stage, "
        + "and a horizon of at least 100,000 simulated minutes (~70 operating days). "
        + "Clinic-day runs are transient and will not match closed-form formulas. "
        + "M/M/c additionally requires exponential service; choose M/G/c or G/G/c "
        + "for any other service distribution.";

    /// <summary>The models the widget can compare against, in display order.</summary>
    public IReadOnlyList<string> ModelOptions { get; } = new[] { "M/M/c", "M/G/c", "G/G/c" };

    /// <summary>
    /// The model the widget is currently showing. Changing it re-evaluates the
    /// retained run; it never asks the engine to run again.
    /// </summary>
    [ObservableProperty]
    private string _selectedModel = "M/M/c";

    /// <summary>True while no comparable run has been applied; false once rows exist.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Inverse of <see cref="IsEmpty"/>, so the view can bind without a negating converter.</summary>
    public bool HasRows => !IsEmpty;

    /// <summary>The simulated-vs-analytical rows, one per stage, in stage order.</summary>
    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    private int _generation;

    private SimulationResult? _lastResult;
    private string _lastArrivalFamily = "Exponential";
    private IReadOnlyList<string> _lastServiceFamilies = Array.Empty<string>();
    private IReadOnlyList<(double Lambda, double Mu, int Servers)> _lastStageInputs =
        Array.Empty<(double, double, int)>();
    private IReadOnlyList<DistributionSpec>? _lastServiceSpecs;

    /// <summary>The <see cref="AnalyticalModelFamily"/> matching <see cref="SelectedModel"/>.</summary>
    public AnalyticalModelFamily SelectedModelFamily => SelectedModel switch
    {
        "M/G/c" => AnalyticalModelFamily.Mgc,
        "G/G/c" => AnalyticalModelFamily.Ggc,
        _ => AnalyticalModelFamily.Mmc
    };

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasRows));

    partial void OnSelectedModelChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedModelFamily));
        if (_lastResult is not null)
        {
            Apply(_lastResult, _lastArrivalFamily, _lastServiceFamilies, _lastStageInputs, _lastServiceSpecs);
        }
    }

    /// <summary>
    /// Validates a finished run on the background thread (G5): the closed-form
    /// numbers are computed off the UI thread, then the rows are applied on the
    /// UI thread. Thread-safe: call from the UI thread.
    /// </summary>
    /// <param name="result">The completed engine result, or null when the run was refused.</param>
    /// <param name="arrivalFamily">Configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">Configured service family per stage.</param>
    /// <param name="stageInputs">Per-stage analytical inputs (λ, μ, c), in stage order.</param>
    /// <param name="serviceSpecs">Per-stage configured service specs, used for Cs² (D-157); null means exponential.</param>
    public void ApplyAsync(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs = null)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        Retain(result, arrivalFamily, serviceFamilies, stageInputs, serviceSpecs);

        int generation = ++_generation;
        Task.Run(() =>
        {
            var rows = Compare(result);
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation)
                {
                    ApplyRows(rows);
                }
                else
                {
                    Serilog.Log.Warning("Analytical validation refresh superseded by a newer apply; dropping stale rows");
                }
            });
        });
    }

    /// <summary>
    /// Synchronous comparison used by tests and immediate wiring paths: computes
    /// the rows and applies them on the caller's thread.
    /// </summary>
    /// <param name="result">The completed engine result, or null when the run was refused.</param>
    /// <param name="arrivalFamily">Configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">Configured service family per stage.</param>
    /// <param name="stageInputs">Per-stage analytical inputs (λ, μ, c), in stage order.</param>
    /// <param name="serviceSpecs">Per-stage configured service specs, used for Cs² (D-157); null means exponential.</param>
    public void Apply(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs = null)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        Retain(result, arrivalFamily, serviceFamilies, stageInputs, serviceSpecs);
        ++_generation;
        ApplyRows(Compare(result));
    }

    /// <summary>Replaces the rows with <paramref name="rows"/> (UI thread).</summary>
    public void ApplyRows(IReadOnlyList<ComparisonRow> rows)
    {
        ++_generation;
        Rows.Clear();
        foreach (ComparisonRow row in rows)
        {
            Rows.Add(row);
        }

        IsEmpty = Rows.Count == 0;
    }

    /// <summary>Returns the widget to its empty state (Clear All, or a refused run).</summary>
    public void Clear()
    {
        ++_generation;
        _lastResult = null;
        Rows.Clear();
        IsEmpty = true;
    }

    private IReadOnlyList<ComparisonRow> Compare(SimulationResult result) =>
        AnalyticalValidationService.Compare(
            result,
            _lastArrivalFamily,
            _lastServiceFamilies,
            _lastStageInputs,
            SelectedModelFamily,
            _lastServiceSpecs);

    private void Retain(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs)
    {
        _lastResult = result;
        _lastArrivalFamily = arrivalFamily;
        _lastServiceFamilies = serviceFamilies;
        _lastStageInputs = stageInputs;
        _lastServiceSpecs = serviceSpecs;
    }
}
