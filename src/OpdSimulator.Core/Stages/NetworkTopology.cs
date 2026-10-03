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
    /// <param name="bypassStageIndex">Zero-based index of the stage being <b>skipped</b> — the
    /// stage a bypassed patient never enters. The draw fires on the completion of
    /// <c>bypassStageIndex − 1</c>, or at the arrival event when this is 0 (D-189);
    /// −1 for no bypass.</param>
    /// <param name="bypassDestinationIndex">Zero-based index of the stage a bypassed patient lands
    /// in; −1 for no bypass. Must be strictly after <paramref name="bypassStageIndex"/>, since
    /// routing backwards would be a cycle rather than a skip.</param>
    /// <param name="screeningArrivalCap">Maximum admitted rate into
    /// <paramref name="screeningCapStageIndex"/> in patients per minute — the daily cap
    /// divided by the session length. null for no cap (D-190).</param>
    /// <param name="screeningCapStageIndex">Index of the stage the admitted-load cap applies
    /// to. −1 when no cap is configured.</param>
    public NetworkTopology(
        double arrivalRate,
        IReadOnlyList<StageSpec> stageSpecs,
        int exitStageIndex = -1,
        double exitProbability = 0.0,
        double bypassProbability = 0.0,
        int bypassStageIndex = -1,
        int bypassDestinationIndex = -1,
        double? screeningArrivalCap = null,
        int screeningCapStageIndex = -1)
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
        if (bypassProbability > 0.0 && stageSpecs.Count < 2)
            throw new ArgumentException(
                "Bypass needs at least two stages: a patient cannot skip a stage and land somewhere else if there is nowhere else to land.",
                nameof(bypassStageIndex));
        if (bypassProbability > 0.0 && (bypassStageIndex < 0 || bypassStageIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(nameof(bypassStageIndex), bypassStageIndex, "The bypass stage must exist inside the stage list.");
        if (bypassProbability > 0.0 && (bypassDestinationIndex <= bypassStageIndex || bypassDestinationIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(
                nameof(bypassDestinationIndex), bypassDestinationIndex,
                $"The bypass destination must come after the bypass stage ({bypassStageIndex}) and inside the stage list ({stageSpecs.Count - 1}).");

        // The collision test is OFF BY ONE relative to the exit stage, and getting
        // it wrong rejects both configurations the owner requires (D-189).
        //
        // S is the stage being SKIPPED, so the bypass draw fires when the patient
        // would be routed INTO S:
        //
        //   S == 0  → at the arrival event, before the patient enters any stage.
        //              Not a service completion at all, so it can never collide
        //              with the exit draw. This is the 2-stage clinic file
        //              ([Screening, Doctor], S = 0, D = 1, ExitStageIndex = 0).
        //
        //   S > 0   → at the completion of stage S − 1.
        //
        // The exit draw fires at the completion of ExitStageIndex, so the two can
        // compete for the same completion exactly when S − 1 == ExitStageIndex.
        // S == ExitStageIndex is NOT the test: for the 3-stage OPD (S = 1,
        // ExitStageIndex = 1) that reads as a collision while S − 1 = 0 ≠ 1 shows
        // there is none, and rejecting on it refuses a configuration that runs
        // perfectly well. The old rule (D-179) used S == ExitStageIndex and was
        // therefore wrong twice over — it rejected the 2-stage file outright and
        // rejected the 3-stage file for a collision that does not exist.
        if (bypassProbability > 0.0 && bypassStageIndex > 0 && exitProbability > 0.0 && bypassStageIndex - 1 == exitStageIndex)
            throw new ArgumentException(
                $"The bypass stage ({bypassStageIndex}) is entered on the completion of stage {bypassStageIndex - 1}, which is also the exit stage: one service completion would have to resolve to both an exit and a bypass, and the destination would depend on which was consulted first. Move the bypass source or the exit stage.",
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

        // Admitted-load cap (D-190). A cap is meaningless without a stage to apply
        // it to and without a positive rate, so both are checked together rather
        // than letting a null rate fall through and quietly disable the cap.
        if (screeningArrivalCap is { } cap && !(cap > 0))
            throw new ArgumentOutOfRangeException(nameof(screeningArrivalCap), cap, "The admitted-load cap must be strictly positive.");
        if (screeningArrivalCap is not null && (screeningCapStageIndex < 0 || screeningCapStageIndex >= stageSpecs.Count))
            throw new ArgumentOutOfRangeException(nameof(screeningCapStageIndex), screeningCapStageIndex, "The capped stage must exist inside the stage list.");
        if (screeningArrivalCap is null && screeningCapStageIndex != -1)
            throw new ArgumentException("A capped stage index was supplied without a cap rate.", nameof(screeningCapStageIndex));

        ScreeningArrivalCap = screeningArrivalCap;
        ScreeningCapStageIndex = screeningArrivalCap is not null ? screeningCapStageIndex : -1;
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

    /// <summary>Index of a bypassed patient's destination, or −1 when bypass is disabled.</summary>
    public int BypassDestinationIndex { get; }

    /// <summary>Whether a bypass draw can fire at all.</summary>
    public bool BypassEnabled => BypassStageIndex >= 0;

    /// <summary>
    /// Index of the stage whose service completion carries the bypass draw — the
    /// completion on which the patient would otherwise be routed into
    /// <see cref="BypassStageIndex"/>. Equals <c>BypassStageIndex − 1</c>.
    /// </summary>
    /// <remarks>
    /// −1 when bypass is disabled, and also when <see cref="BypassStageIndex"/> is 0:
    /// a patient who skips the very first stage is routed at the <b>arrival</b> event,
    /// which is not a service completion, so there is no stage completion that can
    /// carry the draw (D-189).
    /// </remarks>
    public int BypassTriggerStageIndex => BypassEnabled && BypassStageIndex > 0 ? BypassStageIndex - 1 : -1;

    /// <summary>
    /// Whether the bypass draw fires at the arrival event rather than at a service
    /// completion, because the skipped stage is the first one (D-189).
    /// </summary>
    public bool BypassAtArrival => BypassEnabled && BypassStageIndex == 0;

    /// <summary>
    /// Maximum admitted rate into <see cref="ScreeningCapStageIndex"/> in patients
    /// per minute, or null when no admitted-load cap is configured (D-190).
    /// </summary>
    /// <remarks>
    /// Set from the daily cap divided by the session length, so the two can never
    /// disagree. When present, <see cref="EffectiveArrivalRate"/> returns the rate a
    /// stage will actually see rather than the rate demand would place on it, which
    /// is what the stability check and the reported metrics both need.
    /// </remarks>
    public double? ScreeningArrivalCap { get; }

    /// <summary>
    /// Index of the stage the admitted-load cap applies to, or −1 when no cap is
    /// configured (D-190).
    /// </summary>
    public int ScreeningCapStageIndex { get; }

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
    /// (1 − p_exit) of it. Bypass injects p_bypass of the arriving mass straight
    /// onto the destination. With bypass disabled this collapses back to the
    /// original D-007 product, so an unmodified caller sees the same numbers it
    /// always did.
    /// </para>
    /// <para>
    /// The bypass share is applied at the <b>trigger</b> node
    /// (<see cref="BypassTriggerStageIndex"/> = S − 1), which is the completion on
    /// which the patient would be routed into the skipped stage S. Applying it at S
    /// instead would fire the coin twice for a 3-stage chain and double-count the
    /// share (D-189). When S == 0 the split happens before any stage, because the
    /// draw fires at the arrival event.
    /// </para>
    /// <para>
    /// For the 2-stage clinic file this gives λ_screening = λ₀(1 − p_bypass) and
    /// λ_doctor = λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit). For the 3-stage
    /// OPD with S = 1 it gives λ_reception = λ₀, λ_screening = λ₀(1 − p_bypass)
    /// and λ_doctor = λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit) — the same
    /// flows D-179 produced, because its S = 0 drew on the same node (stage 0)
    /// that S = 1 draws on.
    /// </para>
    /// </remarks>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>λᵢ in patients per minute.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the index is outside the topology.</exception>
    public double EffectiveArrivalRate(int stageIndex)
        => ComputeArrivalRate(stageIndex, applyCap: true);

    /// <summary>
    /// The inflow a stage would see with no admitted-load cap applied — the rate
    /// demand places on it before the cap turns patients away (D-190).
    /// </summary>
    /// <remarks>
    /// Reported alongside the capped rate when a run is refused, so the user can see
    /// how much of the overload the cap is absorbing rather than only the remainder.
    /// </remarks>
    /// <param name="stageIndex">Zero-based index of the stage.</param>
    /// <returns>Uncapped λᵢ in patients per minute.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If the index is outside the topology.</exception>
    public double OfferedArrivalRate(int stageIndex)
        => ComputeArrivalRate(stageIndex, applyCap: false);

    private double ComputeArrivalRate(int stageIndex, bool applyCap)
    {
        if (stageIndex < 0 || stageIndex >= _stageSpecs.Length)
            throw new ArgumentOutOfRangeException(nameof(stageIndex), stageIndex, "Stage index outside the topology.");

        double[] mass = new double[_stageSpecs.Length];
        double[] freeAtDestination = new double[_stageSpecs.Length]; // mass that skipped the capped stage

        if (BypassAtArrival)
        {
            // The draw happens at the arrival event (D-189), so the split happens
            // here and not at any stage: only 1 − p_bypass enters stage 0, and the
            // bypass share lands directly on the destination. This is the ONLY place
            // the skipped stage is the first one, so the case has exactly one
            // reachable configuration — the 2-stage clinic file — which is what
            // makes it cheap to test exhaustively.
            mass[0] = 1.0 - BypassProbability;
            mass[BypassDestinationIndex] += BypassProbability;
            freeAtDestination[BypassDestinationIndex] += BypassProbability;
        }
        else
        {
            mass[0] = 1.0;
        }

        // Bypass fires on the completion of stage S − 1, because that is the
        // completion on which the patient would be routed INTO the skipped stage.
        // Putting it at S instead would fire the coin twice in the 3-stage case:
        // once at Reception's completion (skipping Screening) and again at
        // Screening's completion, double-counting the bypass share (D-189).
        //
        // S == 0 leaves this at −1, so the loop below never takes the bypass branch
        // for the arrival-time case — the split was already applied above.
        int trigger = BypassTriggerStageIndex;

        for (int i = 0; i <= stageIndex; i++)
        {
            // Read the incoming mass without mutating mass[i]: the rate we return
            // is the flow INTO stage i, so i's own outgoing decision must not
            // reduce it. That is the whole difference between λᵢ and λᵢ₊₁.
            double incoming = mass[i];

            if (i == trigger)
            {
                // The bypass share jumps past the skipped stages; the remainder
                // enters the skipped stage as normal. The share is marked "free" so
                // a downstream cap does not throttle it: a patient who never queued
                // for screening never consumed a place in the screening session.
                mass[BypassDestinationIndex] += incoming * BypassProbability;
                freeAtDestination[BypassDestinationIndex] += incoming * BypassProbability;
                if (i + 1 < mass.Length)
                    mass[i + 1] += incoming * (1.0 - BypassProbability);
            }
            else if (i == ExitStageIndex)
            {
                // The exit share leaves the system and contributes nothing downstream.
                // The constructor guarantees `trigger != ExitStageIndex`, so the two
                // decisions can never both apply to one completion and their order
                // cannot matter (D-189).
                double forwarded = incoming * (1.0 - ExitProbability);
                if (i + 1 < mass.Length)
                    mass[i + 1] += forwarded;
            }
            else if (i + 1 < mass.Length)
            {
                mass[i + 1] += incoming;
            }
        }

        // Admitted-load cap (D-190). A daily cap of N over a session of T minutes
        // admits at most N/T per minute, so the Screening-bound inflow is clamped
        // here and every downstream stage inherits the reduction — the patients the
        // cap turns away never arrive, so they cannot reach the Doctor either.
        // The bypass stream is left alone, because those patients never take a place
        // in the screening session.
        if (applyCap && ScreeningArrivalCap is { } capRate && stageIndex >= ScreeningCapStageIndex)
        {
            double capMass = capRate / ArrivalRate;
            double offered = mass[ScreeningCapStageIndex];
            if (offered > capMass && offered > 0)
            {
                double admittedShare = capMass / offered;
                for (int j = ScreeningCapStageIndex + 1; j <= stageIndex; j++)
                    mass[j] = freeAtDestination[j] + (mass[j] - freeAtDestination[j]) * admittedShare;
                mass[ScreeningCapStageIndex] = capMass;
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
                unstable.Add(new UnstableStage(
                    _stageSpecs[i].Name, EffectiveArrivalRate(i), _stageSpecs[i].ServerCount, _stageSpecs[i].ServiceRate, rho,
                    ScreeningArrivalCap is null ? null : OfferedArrivalRate(i),
                    ScreeningArrivalCap));
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