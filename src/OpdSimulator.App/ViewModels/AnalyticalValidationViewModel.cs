using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    /// The exact wording of the widget's empty state before a run has been
    /// applied. It names every condition so the user understands what the
    /// widget needs.
    /// </summary>
    public const string DefaultEmptyMessage =
        "Analytical comparison requires a steady-state run — ρ < 1 at every stage, "
        + "and a horizon of at least 100,000 simulated minutes (~70 operating days). "
        + "Clinic-day runs are transient and will not match closed-form formulas. "
        + "M/M/c requires exponential arrivals and service; M/G/c requires "
        + "exponential arrivals; G/G/c accepts any arrival and service variability.";

    /// <summary>
    /// The empty-state text currently shown. It is the default message until a
    /// run is applied, then the refusal that run produced — so the widget says
    /// WHY it is empty (FR-UI-9) instead of always printing the same paragraph.
    /// </summary>
    [ObservableProperty]
    private string _emptyMessage = DefaultEmptyMessage;

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

    /// <summary>
    /// Raised when the user asks for a dedicated steady-state run (D-137).
    /// </summary>
    /// <remarks>
    /// The widget cannot run the engine itself: the configuration and the engine
    /// belong to <see cref="MainViewModel"/>. The event keeps the dependency one
    /// way — the widget says WHAT it needs, the shell decides how to produce it.
    /// </remarks>
    public event EventHandler? SteadyStateValidationRequested;

    /// <summary>True while a steady-state validation run is in flight.</summary>
    [ObservableProperty]
    private bool _isSteadyStateRunning;

    /// <summary>Keeps the button disabled during a validation run.</summary>
    /// <returns><c>true</c> unless a run is already in flight.</returns>
    private bool CanRunSteadyStateValidation() => !IsSteadyStateRunning;

    partial void OnIsSteadyStateRunningChanged(bool value)
        => RunSteadyStateValidationCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Asks the shell to run the current configuration for 100,000 minutes and
    /// feed the result to this widget.
    /// </summary>
    /// <remarks>
    /// A dedicated run rather than a longer clinic day: the closed forms describe
    /// steady state, and stretching one 165-minute session would change the
    /// question the run answers. The shell builds a DiagnosticTrace copy of the
    /// current parameters, so the results panel keeps showing the run the user
    /// actually asked for.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanRunSteadyStateValidation))]
    private void RunSteadyStateValidation()
        => SteadyStateValidationRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The simulated-vs-analytical rows, one per stage, in stage order.</summary>
    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    private int _generation;

    private SimulationResult? _lastResult;
    private string _lastArrivalFamily = "Exponential";
    private IReadOnlyList<string> _lastServiceFamilies = Array.Empty<string>();
    private IReadOnlyList<(double Lambda, double Mu, int Servers)> _lastStageInputs =
        Array.Empty<(double, double, int)>();
    private IReadOnlyList<DistributionSpec>? _lastServiceSpecs;
    private DistributionSpec? _lastArrivalSpec;

    /// <summary>One comparison attempt carried off the UI thread.</summary>
    /// <param name="Rows">The rows, empty when the model does not apply.</param>
    /// <param name="Refusal">Why the table is empty, or null when it is not.</param>
    private sealed record ComparisonOutcome(IReadOnlyList<ComparisonRow> Rows, string? Refusal);

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
            Apply(
                _lastResult,
                _lastArrivalFamily,
                _lastServiceFamilies,
                _lastStageInputs,
                _lastServiceSpecs,
                _lastArrivalSpec);
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
    /// <param name="arrivalSpec">The configured arrival distribution, used for Ca² (D-211); null means Poisson when the family says so.</param>
    public void ApplyAsync(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs = null,
        DistributionSpec? arrivalSpec = null)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        Retain(result, arrivalFamily, serviceFamilies, stageInputs, serviceSpecs, arrivalSpec);

        int generation = ++_generation;
        Task.Run(() =>
        {
            ComparisonOutcome outcome = Compare(result);
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation)
                {
                    ApplyRows(outcome.Rows, outcome.Refusal);
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
    /// <param name="arrivalSpec">The configured arrival distribution, used for Ca² (D-211); null means Poisson when the family says so.</param>
    public void Apply(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs = null,
        DistributionSpec? arrivalSpec = null)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        Retain(result, arrivalFamily, serviceFamilies, stageInputs, serviceSpecs, arrivalSpec);
        ++_generation;
        ComparisonOutcome outcome = Compare(result);
        ApplyRows(outcome.Rows, outcome.Refusal);
    }

    /// <summary>Replaces the rows with <paramref name="rows"/> (UI thread).</summary>
    /// <param name="rows">The comparison rows; empty means the model did not apply.</param>
    /// <param name="refusal">Why the table is empty; null restores the default empty-state message.</param>
    public void ApplyRows(IReadOnlyList<ComparisonRow> rows, string? refusal = null)
    {
        ++_generation;
        Rows.Clear();
        foreach (ComparisonRow row in rows)
        {
            Rows.Add(row);
        }

        IsEmpty = Rows.Count == 0;
        EmptyMessage = IsEmpty && !string.IsNullOrWhiteSpace(refusal)
            ? refusal!
            : DefaultEmptyMessage;
    }

    /// <summary>Returns the widget to its empty state (Clear All, or a refused run).</summary>
    public void Clear()
    {
        ++_generation;
        _lastResult = null;
        Rows.Clear();
        EmptyMessage = DefaultEmptyMessage;
        IsEmpty = true;
    }

    private ComparisonOutcome Compare(SimulationResult result)
    {
        AnalyticalValidationService.TryCompare(
            result,
            _lastArrivalFamily,
            _lastServiceFamilies,
            _lastStageInputs,
            SelectedModelFamily,
            _lastServiceSpecs,
            _lastArrivalSpec,
            out IReadOnlyList<ComparisonRow> rows,
            out string? refusal);

        return new ComparisonOutcome(rows, refusal);
    }

    private void Retain(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        IReadOnlyList<DistributionSpec>? serviceSpecs,
        DistributionSpec? arrivalSpec)
    {
        _lastResult = result;
        _lastArrivalFamily = arrivalFamily;
        _lastServiceFamilies = serviceFamilies;
        _lastStageInputs = stageInputs;
        _lastServiceSpecs = serviceSpecs;
        _lastArrivalSpec = arrivalSpec;
    }
}
