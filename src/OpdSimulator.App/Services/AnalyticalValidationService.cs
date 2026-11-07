namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
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
    /// <summary>
    /// The stage's 1-based position, shown as its own column (FR-UI-36) so a
    /// comparison can be cited unambiguously ("stage 2's Δ%") without the
    /// reader counting rows.
    /// </summary>
    string SerialNumber,
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
/// Each model states its own assumptions (D-212): M/M/c needs exponential
/// arrivals and exponential service, M/G/c needs exponential arrivals, and
/// G/G/c accepts any arrival and service variability through Ca² and Cs². All
/// three are steady-state results, so a transient run or an unstable stage
/// (ρ ≥ 1) is refused. <see cref="TryCompare"/> returns the named reason;
/// <see cref="Compare"/> returns an empty table, because a comparison made
/// under violated assumptions is worse than no comparison.
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
    /// <param name="model">The closed-form model to compare against.</param>
    /// <param name="serviceSpecs">Per-stage configured service specs, used for Cs²; null means exponential.</param>
    /// <param name="arrivalSpec">
    /// The configured arrival distribution, used for Ca². Null is the classic
    /// exponential process when <paramref name="arrivalFamily"/> names it, and an
    /// unknown Ca² otherwise — never silently read as 1 (D-211).
    /// </param>
    /// <returns>
    /// One row per stage when every assumption holds; an empty list otherwise.
    /// Returning empty rather than a partial comparison is deliberate — a
    /// comparison made under violated assumptions is worse than no comparison.
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
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        AnalyticalModelFamily model = AnalyticalModelFamily.Mmc,
        IReadOnlyList<DistributionSpec>? serviceSpecs = null,
        DistributionSpec? arrivalSpec = null)
        => TryCompare(
            result,
            arrivalFamily,
            serviceFamilies,
            stageInputs,
            model,
            serviceSpecs,
            arrivalSpec,
            out IReadOnlyList<ComparisonRow> rows,
            out _)
                ? rows
                : Array.Empty<ComparisonRow>();

    /// <summary>
    /// Builds the comparison table or explains, in one named sentence, why no
    /// comparison exists.
    /// </summary>
    /// <param name="result">The finished simulation result.</param>
    /// <param name="arrivalFamily">The configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">The configured service distribution family, one per stage.</param>
    /// <param name="stageInputs">Per-stage analytical inputs (λ, μ, c), in stage order.</param>
    /// <param name="model">The closed-form model to compare against.</param>
    /// <param name="serviceSpecs">Per-stage configured service specs, used for Cs².</param>
    /// <param name="arrivalSpec">The configured arrival distribution, used for Ca².</param>
    /// <param name="rows">The table when this method returns <c>true</c>; otherwise empty.</param>
    /// <param name="refusal">The reason when this method returns <c>false</c>; otherwise null.</param>
    /// <returns><c>true</c> when a comparison was produced.</returns>
    /// <remarks>
    /// <para>
    /// Applicability is decided before any arithmetic, and each refusal names the
    /// model, the offending input and the model that would accept it. That is the
    /// difference between a widget that is empty and a widget that explains itself
    /// (FR-UI-9): "M/M/c requires exponential service" is actionable; a blank
    /// table is not.
    /// </para>
    /// <para>
    /// The service families are read from the SPECS when they are available,
    /// because a spec carries both the family and the parameters the engine
    /// sampled; the string list is the fallback for callers that have no specs.
    /// Reading the family from a name while the engine sampled from a spec would
    /// let the two disagree, which is the D-147 class of defect.
    /// </para>
    /// </remarks>
    public static bool TryCompare(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<(double Lambda, double Mu, int Servers)> stageInputs,
        AnalyticalModelFamily model,
        IReadOnlyList<DistributionSpec>? serviceSpecs,
        DistributionSpec? arrivalSpec,
        out IReadOnlyList<ComparisonRow> rows,
        out string? refusal)
    {
        rows = Array.Empty<ComparisonRow>();
        refusal = null;

        // Every model here is a steady-state result: a short/transient run (e.g. one
        // 165-minute clinic day) is not comparable, regardless of model or families.
        if (result.OperatingTimeMinutes < MinimumSteadyStateMinutes)
        {
            refusal =
                $"The run lasted {result.OperatingTimeMinutes:0} simulated minutes, but " +
                $"{DisplayName(model)} values are steady-state results and need at least " +
                $"{MinimumSteadyStateMinutes:0} minutes (~70 operating days). Run a " +
                "steady-state validation from the Results panel.";
            return false;
        }

        if (stageInputs.Count == 0 || stageInputs.Count < result.StageMetrics.Count)
        {
            refusal =
                $"The comparison needs λ, μ and c for all {result.StageMetrics.Count} " +
                $"stage(s), but only {stageInputs.Count} were supplied.";
            return false;
        }

        // Ca² comes from the configured arrival distribution, never from an
        // assumption. A null spec is the classic exponential process ONLY when the
        // family says so; any other family without a spec has an unknown Ca², and
        // guessing 1 would make a G/G run look like an M/M run.
        bool arrivalIsExponential = arrivalSpec is not null
            ? arrivalSpec.Family == DistributionFamily.Exponential
            : IsExponential(arrivalFamily);

        if (arrivalSpec is null && !arrivalIsExponential)
        {
            refusal =
                $"The {arrivalFamily} arrival process was configured, but its distribution " +
                "was not carried into the comparison, so Ca² is unknown. Re-run from the " +
                "form so the arrival shape travels with the run.";
            return false;
        }

        double ca2 = arrivalSpec is not null
            ? AnalyticalModels.SquaredCoefficientOfVariation(arrivalSpec)
            : 1.0;

        // Model applicability. M/M/c assumes exponential arrivals AND service.
        // M/G/c assumes exponential arrivals only. G/G/c assumes neither, so its
        // Ca² and Cs² are the inputs rather than preconditions.
        if (model is AnalyticalModelFamily.Mmc or AnalyticalModelFamily.Mgc && !arrivalIsExponential)
        {
            refusal =
                $"{DisplayName(model)} requires exponential (Poisson) inter-arrival times, " +
                $"but this run used {arrivalFamily} (Ca² = {ca2:0.###}). Choose G/G/c, " +
                "which takes any arrival variability as an input.";
            return false;
        }

        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            if (model != AnalyticalModelFamily.Mmc || ServiceIsExponential(i, serviceFamilies, serviceSpecs))
            {
                continue;
            }

            DistributionSpec? spec = ServiceSpecAt(i, serviceSpecs);
            string family = spec?.Family.ToString()
                ?? (i < serviceFamilies.Count ? serviceFamilies[i] : "unknown");
            double cs2 = spec is null ? 1.0 : AnalyticalModels.SquaredCoefficientOfVariation(spec);

            refusal =
                $"M/M/c requires exponential service times, but stage " +
                $"'{result.StageMetrics[i].StageName}' uses {family} (Cs² = {cs2:0.###}). " +
                "Choose M/G/c or G/G/c.";
            return false;
        }

        var built = new List<ComparisonRow>(result.StageMetrics.Count);
        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            StageMetrics sim = result.StageMetrics[i];
            (double lambda, double mu, int servers) = stageInputs[i];

            DistributionSpec? serviceSpec = ServiceSpecAt(i, serviceSpecs);
            AnalyticalModelInputs modelInputs = AnalyticalModels.InputsFor(
                lambda, mu, servers, serviceSpec, arrivalCa2: ca2);

            if (!AnalyticalModels.TryCompute(model, modelInputs, out AnalyticalModelResult? analytical, out string? why))
            {
                // A single unstable / invalid stage makes the whole comparison
                // invalid — do not show rows for the stages that happened to pass.
                refusal = $"Stage '{sim.StageName}': {why}";
                return false;
            }

            double delta = Math.Abs(sim.AverageWaitMinutes - analytical!.Wq)
                / Math.Max(analytical.Wq, 1e-6)
                * 100.0;
            built.Add(new ComparisonRow(
                (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                sim.StageName,
                sim.AverageWaitMinutes,
                analytical.Wq,
                sim.AverageQueueLength,
                analytical.Lq,
                delta));
        }

        rows = built;
        return true;
    }

    /// <summary>The user-facing name of a model family.</summary>
    /// <param name="model">The family.</param>
    /// <returns>The notation shown in the widget.</returns>
    private static string DisplayName(AnalyticalModelFamily model) => model switch
    {
        AnalyticalModelFamily.Mmc => "M/M/c",
        AnalyticalModelFamily.Mgc => "M/G/c",
        AnalyticalModelFamily.Ggc => "G/G/c",
        _ => model.ToString()
    };

    private static DistributionSpec? ServiceSpecAt(int index, IReadOnlyList<DistributionSpec>? serviceSpecs)
        => serviceSpecs is not null && index < serviceSpecs.Count ? serviceSpecs[index] : null;

    /// <summary>
    /// True when the stage's service distribution is exponential, reading the
    /// spec's family when a spec exists and the name list otherwise.
    /// </summary>
    private static bool ServiceIsExponential(
        int index,
        IReadOnlyList<string> serviceFamilies,
        IReadOnlyList<DistributionSpec>? serviceSpecs)
    {
        DistributionSpec? spec = ServiceSpecAt(index, serviceSpecs);
        if (spec is not null)
        {
            return spec.Family == DistributionFamily.Exponential;
        }

        return index < serviceFamilies.Count && IsExponential(serviceFamilies[index]);
    }

    private static bool IsExponential(string? family)
        => string.Equals(family, "Exponential", StringComparison.OrdinalIgnoreCase)
            || string.Equals(family, "Poisson", StringComparison.OrdinalIgnoreCase);
}
