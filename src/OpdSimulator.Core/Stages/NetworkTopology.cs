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
    /// Effective per-stage arrival rates are the sum of every route that reaches
    /// the stage (D-007, as amended by D-179 when bypass was added): λ_reception
    /// = λ₀, and each downstream stage receives its share of whatever flowed into
    /// its predecessors. With no bypass configured this is the original product
    /// rule, so λ_screening = λ₀ and λ_doctor = λ₀·(1 − p_exit).
    /// <see cref="Validate"/> refuses the whole run if any stage has ρᵢ ≥ 1
    /// (FR-VAL-1).
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
    /// <param name="bypassProbability">Probability p_bypass that a patient skips a stage and is
    /// routed straight past it, in [0, 1). 0 disables bypass entirely (D-179).</param>
    /// <param name="bypassStageIndex">Zero-based index of the stage whose completion triggers the
    /// bypass draw (Reception = 0); −1 for no bypass.</param>
    /// <param name="bypassDestinationIndex">Zero-based index of the stage a bypassed patient lands
    /// in; −1 for no bypass. Must be strictly after <paramref name="bypassStageIndex"/>, since
    /// routing backwards would be a cycle rather than a skip.</param>
    public NetworkTopology(
        double arrivalRate,
        IReadOnlyList<StageSpec> stageSpecs,
        int exitStageIndex = -1,
        double exitProbability = 0.0,
        double bypassProbability = 0.0,
        int bypassStageIndex = -1,
        int bypassDestinationIndex = -1)
    {
        if (arrivalRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(arrivalRate), arrivalRate, "Arrival rate λ must be strictly positive.");
        if (stageSpecs is null || stageSpecs.Count == 0)
            throw new ArgumentException("At least one stage is required.", nameof(stageSpecs));
        if (exitProbability < 0.0 || exitProbability >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(exitProbability), exitProbability, "Exit probability must be in [0, 1).");
        if (exitProbability > 0.0 && (exitStageIndex < 0 || exitStageIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(nameof(exitStageIndex), exitStageIndex, "The exit stage must exist inside the stage list.");

        // Bypass mirrors ExitProbability's shape exactly (D-179): same [0, 1)
        // domain, same "a probability of zero means the mechanism is off" reading.
        if (bypassProbability < 0.0 || bypassProbability >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(bypassProbability), bypassProbability, "Bypass probability must be in [0, 1).");
        if (bypassProbability > 0.0 && (bypassStageIndex < 0 || bypassStageIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(nameof(bypassStageIndex), bypassStageIndex, "The bypass stage must exist inside the stage list.");
        if (bypassProbability > 0.0 && (bypassDestinationIndex <= bypassStageIndex || bypassDestinationIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(
                nameof(bypassDestinationIndex), bypassDestinationIndex,
                $"The bypass destination must come after the bypass stage ({bypassStageIndex}) and inside the stage list ({stageSpecs.Count - 1}).");
        if (bypassProbability > 0.0 && bypassStageIndex == exitStageIndex)
            throw new ArgumentException(
                $"The bypass stage ({bypassStageIndex}) cannot also be the exit stage: both draws would fire on the same service completion and the destination would depend on which was consulted first.",
                nameof(bypassStageIndex));

        ArrivalRate = arrivalRate;
        _stageSpecs = stageSpecs.ToArray();
        ExitStageIndex = exitProbability > 0.0 ? exitStageIndex : -1;
        ExitProbability = exitProbability;

        // Same normalisation the exit stage gets on line 51: a probability of zero
        // takes the mechanism out of play *and* discards its indices, so a caller
        // reading BypassStageIndex back cannot be misled into thinking a bypass
        // stage is configured when p_bypass is 0.
        BypassProbability = bypassProbability;
        BypassStageIndex = bypassProbability > 0.0 ? bypassStageIndex : -1;
        BypassDestinationIndex = bypassProbability > 0.0 ? bypassDestinationIndex : -1;
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
    /// Probability p_bypass that a patient completing the bypass stage is routed
    /// straight to <see cref="BypassDestinationIndex"/>, skipping the stages in
    /// between. 0 means bypass is off and both indices are −1 (D-179).
    /// </summary>
    public double BypassProbability { get; }

    /// <summary>
    /// Index of the stage whose service completion triggers the bypass draw, or
    /// −1 when bypass is disabled.
    /// </summary>
    public int BypassStageIndex { get; }

    /// <summary>
    /// Index of the stage a bypassed patient lands in, or −1 when bypass is
    /// disabled.
    /// </summary>
    public int BypassDestinationIndex { get; }

    /// <summary>Whether a bypass draw can fire at all.</summary>
    public bool BypassEnabled => BypassStageIndex >= 0;

    /// <summary>
    /// Inflow rate λᵢ at the given stage, as the <b>sum over every route that
    /// reaches it</b> (D-007 as amended by D-179).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original D-007 rule was a product over a single exit stage:
    /// λᵢ = λ₀ × Π<sub>j&lt;i</sub> (1 − p_exit<sub>j</sub>). That form cannot
    /// express bypass, because a bypassed patient arrives at a downstream stage
    /// <i>without</i> passing through the stages in between, and the single-exit
    /// product has no way to add that second inflow back in.
    /// </para>
    /// <para>
    /// So the rate is now propagated as a probability mass over stages. Starting
    /// with all of the external inflow at stage 0, each stage sends its share
    /// forward: normally all of it to the next stage; at the exit stage,
    /// (1 − p_exit) of it; at the bypass stage, (1 − p_bypass) of it to the next
    /// stage and p_bypass of it to the destination. The rate at a stage is λ₀
    /// times the mass that reaches it.
    /// </para>
    /// <para>
    /// For the three-stage clinic with bypass at Reception this reproduces the
    /// owner's formula exactly: λ_screening = λ₀(1 − p_bypass) and
    /// λ_doctor = λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit). With bypass
    /// disabled it collapses back to the original D-007 product, so an
    /// unmodified caller sees the same numbers it always did.
    /// </para>
    /// </remarks>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>λᵢ in patients per minute.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the index is outside the topology.</exception>
    public double EffectiveArrivalRate(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= _stageSpecs.Length)
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "Stage index outside the topology.");

        double[] mass = new double[_stageSpecs.Length];
        mass[0] = 1.0;

        for (int i = 0; i <= stageIndex; i++)
        {
            // Read the incoming mass without mutating mass[i]: the rate we return
            // is the flow INTO stage i, so i's own outgoing decision must not
            // reduce it. That is the whole difference between λᵢ and λᵢ₊₁.
            double incoming = mass[i];

            if (i == ExitStageIndex)
            {
                // The exit share leaves the system and contributes nothing downstream.
                double forwarded = incoming * (1.0 - ExitProbability);
                if (i + 1 < mass.Length)
                    mass[i + 1] += forwarded;
            }
            else if (i == BypassStageIndex)
            {
                if (i + 1 < mass.Length)
                    mass[i + 1] += incoming * (1.0 - BypassProbability);
                mass[BypassDestinationIndex] += incoming * BypassProbability;
            }
            else if (i + 1 < mass.Length)
            {
                mass[i + 1] += incoming;
            }
        }

        return ArrivalRate * mass[stageIndex];
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