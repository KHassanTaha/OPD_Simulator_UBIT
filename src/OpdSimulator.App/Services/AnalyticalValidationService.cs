namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Engine;

/// <summary>
/// The closed-form M/M/c metrics for one stage, used as the analytical
/// reference the simulated numbers are compared against (FR-STAT-11).
/// </summary>
/// <param name="StageName">Stage the metrics describe.</param>
/// <param name="Lambda">Arrival rate into the stage (patients per minute).</param>
/// <param name="Mu">Per-server service rate (patients per minute).</param>
/// <param name="Servers">Number of parallel servers (c).</param>
/// <param name="Rho">Server utilisation ρ = λ / (c·μ).</param>
/// <param name="Lq">Expected number waiting in queue (Erlang-C).</param>
/// <param name="Wq">Expected waiting time in queue, minutes.</param>
/// <param name="L">Expected number in the system, queue included.</param>
/// <param name="W">Expected time in the system, queue included, minutes.</param>
/// <param name="P0">Probability the system is empty.</param>
public sealed record AnalyticalStageMetrics(
    string StageName,
    double Lambda,
    double Mu,
    int Servers,
    double Rho,
    double Lq,
    double Wq,
    double L,
    double W,
    double P0);

/// <summary>
/// One row of the simulated-vs-analytical comparison table (FR-STAT-11).
/// </summary>
/// <param name="StageName">Stage the row describes.</param>
/// <param name="SimulatedAvgWait">Simulated average wait in queue (minutes).</param>
/// <param name="AnalyticalAvgWait">Closed-form M/M/c average wait in queue (minutes).</param>
/// <param name="SimulatedQueueLength">Simulated time-weighted average queue length.</param>
/// <param name="AnalyticalQueueLength">Closed-form M/M/c expected queue length.</param>
/// <param name="DeltaPercent">Absolute percentage gap between simulated and analytical average wait.</param>
public sealed record ComparisonRow(
    string StageName,
    double SimulatedAvgWait,
    double AnalyticalAvgWait,
    double SimulatedQueueLength,
    double AnalyticalQueueLength,
    double DeltaPercent);

/// <summary>
/// Output-side analytical validation: computes the closed-form M/M/c metrics
/// (Erlang-C) for each stage and compares them with the simulated run. This is
/// a model-verification aid for the viva, not part of the engine (Phase 8C,
/// FR-STAT-11).
/// </summary>
/// <remarks>
/// The comparison is only valid where its assumptions hold: exponential
/// arrivals, exponential service and a stable stage (ρ &lt; 1). Both
/// <see cref="ComputeForStage"/> and <see cref="Compare"/> return no result
/// when those conditions fail rather than report a mathematically meaningless
/// number.
/// </remarks>
public static class AnalyticalValidationService
{
    /// <summary>
    /// Minimum operating time (simulated minutes) at which the run is treated as
    /// steady state and the closed-form M/M/c comparison is considered valid.
    /// M/M/c formulas are steady-state results, so a short clinic day (165
    /// operating minutes) is transient and must not be compared. 100,000 minutes
    /// is the conservative midpoint of the measured convergence (50,000 min →
    /// ~5% delta; 200,000 min → ~1%).
    /// </summary>
    public const double MinimumSteadyStateMinutes = 100_000.0;

    /// <summary>
    /// Computes the closed-form M/M/c metrics for one stage.
    /// </summary>
    /// <param name="stageName">Stage name carried into the result.</param>
    /// <param name="lambda">Arrival rate into the stage (patients per minute).</param>
    /// <param name="mu">Per-server service rate (patients per minute).</param>
    /// <param name="servers">Number of parallel servers (c).</param>
    /// <returns>
    /// The metrics, or <c>null</c> when the stage is not a valid stable M/M/c:
    /// <paramref name="lambda"/> or <paramref name="mu"/> is not positive,
    /// <paramref name="servers"/> is below 1, or ρ = λ / (c·μ) ≥ 1.
    /// </returns>
    public static AnalyticalStageMetrics? ComputeForStage(string stageName, double lambda, double mu, int servers)
    {
        if (lambda <= 0 || mu <= 0 || servers < 1)
        {
            return null;
        }

        double rho = lambda / (servers * mu);
        if (rho >= 1)
        {
            return null;
        }

        // Erlang-C building blocks: r = offered load λ/μ (in erlangs).
        double r = lambda / mu;

        // P0 = 1 / ( Σ_{k=0}^{c-1} r^k/k! + (r^c/c!)·(1/(1-ρ)) ).
        double sum = 0;
        double term = 1; // r^k / k!, starting at k = 0.
        for (int k = 0; k < servers; k++)
        {
            if (k > 0)
            {
                term *= r / k;
            }

            sum += term;
        }

        double lastTerm = term * r / servers; // r^c / c!.
        double p0 = 1.0 / (sum + lastTerm / (1 - rho));

        double lq = p0 * lastTerm * rho / ((1 - rho) * (1 - rho));
        double wq = lq / lambda;
        double w = wq + (1.0 / mu);
        double l = lambda * w;

        return new AnalyticalStageMetrics(stageName, lambda, mu, servers, rho, lq, wq, l, w, p0);
    }

    /// <summary>
    /// Builds the simulated-vs-analytical comparison table for a finished run.
    /// </summary>
    /// <param name="result">The finished simulation result.</param>
    /// <param name="arrivalFamily">The configured inter-arrival distribution family (e.g. "Exponential").</param>
    /// <param name="serviceFamilies">The configured service distribution family, one per stage.</param>
    /// <param name="stageInputs">
    /// The per-stage analytical inputs (λ, μ, c), in stage order. Must line up
    /// with <see cref="SimulationResult.StageMetrics"/>; a short or empty list
    /// yields no rows rather than a partial, misleading table.
    /// </param>
    /// <returns>
    /// One row per stage when every assumption holds (exponential arrivals,
    /// exponential service, ρ &lt; 1, and a steady-state run of at least
    /// <see cref="MinimumSteadyStateMinutes"/> operating minutes); an empty list
    /// otherwise. Returning empty rather than a partial comparison is deliberate
    /// — a comparison made under violated assumptions is worse than no
    /// comparison.
    /// </returns>
    /// <remarks>
    /// λ and μ come from the caller's <paramref name="stageInputs"/> (built by
    /// the view model from <see cref="StageMetrics.ArrivalRate"/>,
    /// <see cref="StageMetrics.ServiceRate"/> and
    /// <see cref="StageMetrics.ServerCount"/>), not from this service reading
    /// the result — this keeps the analytical formula independent of however
    /// the engine happens to report its own rates. The steady-state guard reads
    /// <see cref="SimulationResult.OperatingTimeMinutes"/>: for a horizon run
    /// this is the elapsed simulated time, and for a calendar run it is the
    /// summed open-day block time, which stays far below the threshold.
    /// </remarks>
    public static IReadOnlyList<ComparisonRow> Compare(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs)
    {
        // M/M/c is a steady-state result: a short/transient run (e.g. one
        // 165-minute clinic day) is not comparable, regardless of families.
        if (result.OperatingTimeMinutes < MinimumSteadyStateMinutes)
        {
            return Array.Empty<ComparisonRow>();
        }

        if (!IsExponential(arrivalFamily)
            || stageInputs.Count == 0
            || stageInputs.Count < result.StageMetrics.Count)
        {
            return Array.Empty<ComparisonRow>();
        }

        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            string family = i < serviceFamilies.Count ? serviceFamilies[i] : string.Empty;
            if (!IsExponential(family))
            {
                return Array.Empty<ComparisonRow>();
            }
        }

        var rows = new List<ComparisonRow>(result.StageMetrics.Count);
        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            StageMetrics sim = result.StageMetrics[i];
            (double lambda, double mu, int servers) = stageInputs[i];
            AnalyticalStageMetrics? analytical = ComputeForStage(sim.StageName, lambda, mu, servers);
            if (analytical is null)
            {
                // A single unstable / invalid stage makes the whole comparison
                // invalid — do not show rows for the stages that happened to pass.
                return Array.Empty<ComparisonRow>();
            }

            double delta = Math.Abs(sim.AverageWaitMinutes - analytical.Wq)
                / Math.Max(analytical.Wq, 1e-6)
                * 100.0;
            rows.Add(new ComparisonRow(
                sim.StageName,
                sim.AverageWaitMinutes,
                analytical.Wq,
                sim.AverageQueueLength,
                analytical.Lq,
                delta));
        }

        return rows;
    }

    private static bool IsExponential(string? family)
        => string.Equals(family, "Exponential", StringComparison.OrdinalIgnoreCase);
}
