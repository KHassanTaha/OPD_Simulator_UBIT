using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Engine;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Drives the Analytical validation (M/M/c) widget on the Results panel
/// (Phase 8C, FR-STAT-11): an empty state until a run finishes, then a table
/// comparing the simulated per-stage wait and queue length against the
/// closed-form M/M/c values. When the M/M/c assumptions are not met
/// (non-exponential arrivals or service, an unstable stage, or a transient run
/// shorter than <see cref="AnalyticalValidationService.MinimumSteadyStateMinutes"/>)
/// the table stays empty and explains why, rather than showing an invalid
/// comparison.
/// </summary>
/// <remarks>
/// The closed-form numbers are computed off the UI thread by
/// <see cref="ApplyAsync"/> (same background-prep pattern as Phase 8B), with a
/// generation guard so a stale apply cannot overwrite a newer one. The
/// synchronous <see cref="Apply"/> path exists for tests and immediate wiring.
/// </remarks>
public partial class AnalyticalValidationViewModel : ObservableObject
{
    /// <summary>
    /// The exact wording of the widget's empty state (shown until a run yields a
    /// valid steady-state comparison); asserted verbatim by the UI test. It names
    /// all three conditions so the user understands why the widget is hidden.
    /// </summary>
    public string EmptyMessage { get; } =
        "Analytical comparison requires a steady-state run — exponential service, "
        + "ρ < 1 at every stage, and a horizon of at least 100,000 simulated minutes "
        + "(~70 operating days). Clinic-day runs are transient and will not match "
        + "M/M/c formulas.";

    /// <summary>True while no comparable run has been applied; false once rows exist.</summary>
    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Inverse of <see cref="IsEmpty"/>, so the view can bind without a negating converter.</summary>
    public bool HasRows => !IsEmpty;

    /// <summary>The simulated-vs-analytical rows, one per stage, in stage order.</summary>
    public ObservableCollection<ComparisonRow> Rows { get; } = new();

    private int _generation;

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasRows));

    /// <summary>
    /// Validates a finished run on the background thread (G5): the closed-form
    /// numbers are computed off the UI thread, then the rows are applied on the
    /// UI thread. Thread-safe: call from the UI thread.
    /// </summary>
    /// <param name="result">The completed engine result, or null when the run was refused.</param>
    /// <param name="arrivalFamily">Configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">Configured service family per stage.</param>
    /// <param name="stageInputs">Per-stage analytical inputs (λ, μ, c), in stage order.</param>
    public void ApplyAsync(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        int generation = ++_generation;
        Task.Run(() =>
        {
            var rows = AnalyticalValidationService.Compare(result, arrivalFamily, serviceFamilies, stageInputs);
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
    public void Apply(
        SimulationResult? result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs)
    {
        if (result is null)
        {
            Clear();
            return;
        }

        ++_generation;
        ApplyRows(AnalyticalValidationService.Compare(result, arrivalFamily, serviceFamilies, stageInputs));
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
        Rows.Clear();
        IsEmpty = true;
    }
}
