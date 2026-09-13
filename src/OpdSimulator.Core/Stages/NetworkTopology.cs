namespace OpdSimulator.Core.Stages;

using OpdSimulator.Core.Engine;

/// <summary>
/// Configuration of the serial multi-stage network the engine simulates.
/// </summary>
/// <remarks>
/// <para>
/// A topology is an ordered list of <see cref="StageSpec"/>s plus the external
/// arrival rate λ₀ and one optional probabilistic exit. For the real clinic
/// (CONTEXT §1.2/there) the stages are Reception → Screening → Doctor, with a
/// probability <c>p_exit</c> of leaving the system after Screening
/// (FR-SIM-3). Any other stage always forwards to the next stage, and the last
/// stage always exits.
/// </para>
/// <para>
/// Effective per-stage arrival rates follow the product rule (D-007):
/// λᵢ = λ₀ × Π_{j&lt;i} (1 − exitProbability_j), where only the exit stage has
/// a non-zero exit probability. This yields λ_screening = λ₀ and
/// λ_doctor = λ₀·(1 − p_exit). <see cref="Validate"/> refuses the whole run if
/// any stage has ρᵢ ≥ 1 (FR-VAL-1).
/// </para>
/// </remarks>
public sealed class NetworkTopology
{
    private readonly StageSpec[] _stageSpecs;

    /// <summary>
    /// Creates a serial network configuration.
    /// </summary>
    /// <param name="arrivalRate">External arrival rate λ₀ at the first stage (patients per minute).</param>
    /// <param name="stageSpecs">Ordered stage descriptions, first stage first.</param>
    /// <param name="exitStageIndex">Zero-based index of the stage after which patients may exit
    /// probabilistically (Screening = 1); −1 for no probabilistic exit.</param>
    /// <param name="exitProbability">Probability p_exit that a patient leaves after the exit stage,
    /// in [0, 1). Ignored when <paramref name="exitStageIndex"/> is −1.</param>
    public NetworkTopology(double arrivalRate, IReadOnlyList<StageSpec> stageSpecs, int exitStageIndex = -1, double exitProbability = 0.0)
    {
        if (arrivalRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(arrivalRate), arrivalRate, "Arrival rate λ must be strictly positive.");
        if (stageSpecs is null || stageSpecs.Count == 0)
            throw new ArgumentException("At least one stage is required.", nameof(stageSpecs));
        if (exitProbability < 0.0 || exitProbability >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(exitProbability), exitProbability, "Exit probability must be in [0, 1).");
        if (exitProbability > 0.0 && (exitStageIndex < 0 || exitStageIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(nameof(exitStageIndex), exitStageIndex, "The exit stage must exist inside the stage list.");

        ArrivalRate = arrivalRate;
        _stageSpecs = stageSpecs.ToArray();
        ExitStageIndex = exitProbability > 0.0 ? exitStageIndex : -1;
        ExitProbability = exitProbability;
    }

    /// <summary>External arrival rate λ₀ at the first stage (patients per minute).</summary>
    public double ArrivalRate { get; }

    /// <summary>Ordered stage descriptions, first stage first.</summary>
    public IReadOnlyList<StageSpec> StageSpecs => _stageSpecs;

    /// <summary>Index of the stage after which patients may exit, or −1 for none.</summary>
    public int ExitStageIndex { get; }

    /// <summary>Probability p_exit of leaving after the exit stage.</summary>
    public double ExitProbability { get; }

    /// <summary>
    /// Routing-derived arrival rate λᵢ at the given stage (D-007).
    /// </summary>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>λᵢ = λ₀ × Π_{j&lt;i} (1 − exitProbability_j) patients per minute.</returns>
    public double EffectiveArrivalRate(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= _stageSpecs.Length)
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "Stage index outside the topology.");

        double lambda = ArrivalRate;
        for (int j = 0; j < stageIndex; j++)
            lambda *= (j == ExitStageIndex) ? (1.0 - ExitProbability) : 1.0;
        return lambda;
    }

    /// <summary>
    /// Traffic intensity ρᵢ = λᵢ/(cᵢ·μᵢ) at the given stage.
    /// </summary>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>The stage's per-stage ρ.</returns>
    public double RhoFor(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= _stageSpecs.Length)
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "Stage index outside the topology.");

        var spec = _stageSpecs[stageIndex];
        return EffectiveArrivalRate(stageIndex) / (spec.ServerCount * spec.ServiceRate);
    }

    /// <summary>
    /// Builds a fresh runtime <see cref="Stage"/> for the given index. Called
    /// once per run so per-run queue/server state never leaks across runs.
    /// </summary>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>Fresh runtime stage state carrying the derived λᵢ.</returns>
    public Stage CreateStage(int stageIndex)
        => new(_stageSpecs[stageIndex], EffectiveArrivalRate(stageIndex));

    /// <summary>
    /// Validates the topology, refusing to run when any stage has ρᵢ ≥ 1.
    /// </summary>
    /// <exception cref="UnstableSystemException">
    /// If at least one stage is unstable — the message lists every unstable stage
    /// with its λᵢ, cᵢ, μᵢ and ρᵢ so the caller can report exactly what overloads
    /// (FR-VAL-1).
    /// </exception>
    public void Validate()
    {
        var unstable = new List<UnstableStage>();
        for (int i = 0; i < _stageSpecs.Length; i++)
        {
            double rho = RhoFor(i);
            if (rho >= 1.0)
                unstable.Add(new UnstableStage(_stageSpecs[i].Name, EffectiveArrivalRate(i), _stageSpecs[i].ServerCount, _stageSpecs[i].ServiceRate, rho));
        }

        if (unstable.Count > 0)
            throw new UnstableSystemException(unstable);
    }

    /// <summary>
    /// Convenience factory for the classic single-stage model (the Milestone-1
    /// configuration): one stage, no probabilistic exit.
    /// </summary>
    /// <param name="arrivalRate">Arrival rate λ (patients per minute).</param>
    /// <param name="serviceRate">Service rate per server μ (patients per minute).</param>
    /// <param name="serverCount">Number of parallel servers c at the stage.</param>
    /// <param name="stageName">Human-readable stage name.</param>
    /// <returns>A single-stage topology with the given parameters.</returns>
    public static NetworkTopology CreateSingleStage(double arrivalRate, double serviceRate, int serverCount, string stageName)
        => new(arrivalRate, new[] { new StageSpec(stageName, serverCount, serviceRate) });
}