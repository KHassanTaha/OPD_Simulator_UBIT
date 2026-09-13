namespace OpdSimulator.Core.Stages;

using OpdSimulator.Core.Queues;
using OpdSimulator.Core.Servers;

/// <summary>
/// One stage of the serial network at run time: its configuration plus the
/// mutable FIFO queue and parallel servers the engine drives during a run.
/// </summary>
/// <remarks>
/// <para>
/// Realisation of the "single queue per stage" structure in CONTEXT §1.2 and of
/// the N-stage design decision D-006: the engine treats every stage identically,
/// so the 3-stage clinic network is just a longer <see cref="NetworkTopology"/>
/// list rather than special-case code.
/// </para>
/// <para>
/// <see cref="EffectiveArrivalRate"/> is the routing-derived arrival rate λᵢ
/// (D-007): the external arrival rate λ₀ scaled by the continuation
/// probabilities of all earlier stages. It feeds ρᵢ = λᵢ/(cᵢ·μᵢ), the per-stage
/// stability and utilisation measure there is no single whole-network ρ.
/// </para>
/// </remarks>
public sealed class Stage
{
    private readonly StageSpec _spec;

    /// <summary>
    /// Creates the runtime stage state.
    /// </summary>
    /// <param name="spec">The stage configuration.</param>
    /// <param name="effectiveArrivalRate">Routing-derived arrival rate λᵢ (patients per minute).</param>
    public Stage(StageSpec spec, double effectiveArrivalRate)
    {
        _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        if (effectiveArrivalRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(effectiveArrivalRate), effectiveArrivalRate, "Arrival rate λ must be strictly positive.");

        EffectiveArrivalRate = effectiveArrivalRate;
        Servers = Enumerable.Range(0, spec.ServerCount)
            .Select(i => new Server(i))
            .ToArray();
    }

    /// <summary>Human-readable stage name.</summary>
    public string Name => _spec.Name;

    /// <summary>Number of parallel servers c at the stage.</summary>
    public int ServerCount => _spec.ServerCount;

    /// <summary>Service rate per server μ (patients per minute).</summary>
    public double ServiceRate => _spec.ServiceRate;

    /// <summary>Routing-derived arrival rate λᵢ (patients per minute), D-007.</summary>
    public double EffectiveArrivalRate { get; }

    /// <summary>Traffic intensity ρᵢ = λᵢ/(cᵢ·μᵢ); the stage is stable only while ρᵢ &lt; 1.</summary>
    public double Rho => EffectiveArrivalRate / (ServerCount * ServiceRate);

    /// <summary>FIFO queue of patients waiting at this stage.</summary>
    public Queue Queue { get; } = new();

    /// <summary>The stage's parallel servers, indexed 0..c−1.</summary>
    public Server[] Servers { get; }

    /// <summary>Whether at least one server at this stage is currently idle.</summary>
    public bool HasIdleServer => Servers.Any(s => !s.IsBusy);
}