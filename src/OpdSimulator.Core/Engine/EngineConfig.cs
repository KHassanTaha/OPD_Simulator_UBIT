namespace OpdSimulator.Core.Engine;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Immutable configuration for a single-stage M/M/c run.
/// </summary>
/// <remarks>
/// Holds the traffic-intensity inputs (per-stage λ, μ, c) that define the
/// queueing model. <see cref="Validate"/> refuses unstable configurations
/// (ρ ≥ 1, FR-VAL-1) before any simulation work is done.
/// </remarks>
public sealed class EngineConfig
{
    /// <summary>
    /// Creates the configuration.
    /// </summary>
    /// <param name="arrivalRate">Arrival rate λ (patients per minute).</param>
    /// <param name="serviceRate">Service rate per server μ (patients per minute).</param>
    /// <param name="serverCount">Number of parallel servers c at the stage.</param>
    /// <param name="horizonMinutes">Length of the arrival-generation window in minutes (arrivals stop beyond it).</param>
    /// <param name="seed">Random seed for reproducibility (FR-VAL-3, default 42).</param>
    /// <param name="stageName">Human-readable stage name used in logs and stability messages.</param>
    public EngineConfig(
        double arrivalRate,
        double serviceRate,
        int serverCount = 1,
        double horizonMinutes = 10000,
        int seed = SeededRandomSource.DefaultSeed,
        string stageName = "Stage 0 (single-stage)")
    {
        ArrivalRate = arrivalRate;
        ServiceRate = serviceRate;
        ServerCount = serverCount;
        HorizonMinutes = horizonMinutes;
        Seed = seed;
        StageName = stageName;
    }

    /// <summary>Arrival rate λ (patients per minute).</summary>
    public double ArrivalRate { get; }

    /// <summary>Service rate per server μ (patients per minute).</summary>
    public double ServiceRate { get; }

    /// <summary>Number of parallel servers c at the stage.</summary>
    public int ServerCount { get; }

    /// <summary>Length of the arrival-generation window in minutes.</summary>
    public double HorizonMinutes { get; }

    /// <summary>Random seed for reproducibility.</summary>
    public int Seed { get; }

    /// <summary>Human-readable stage name for logs and stability messages.</summary>
    public string StageName { get; }

    /// <summary>
    /// Traffic intensity ρ = λ / (c·μ). The M/M/c system is only stable when ρ &lt; 1.
    /// </summary>
    public double Rho => ArrivalRate / (ServerCount * ServiceRate);

    /// <summary>
    /// Validates the configuration, refusing to run when ρ ≥ 1 (FR-VAL-1).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If any rate/server/horizon parameter is invalid.</exception>
    /// <exception cref="UnstableSystemException">If ρ ≥ 1 — the queue would grow without bound.</exception>
    public void Validate()
    {
        if (ArrivalRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(ArrivalRate), ArrivalRate, "Arrival rate λ must be strictly positive.");
        if (ServiceRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(ServiceRate), ServiceRate, "Service rate μ must be strictly positive.");
        if (ServerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(ServerCount), ServerCount, "At least one server is required.");
        if (HorizonMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(HorizonMinutes), HorizonMinutes, "Horizon must be strictly positive.");

        if (Rho >= 1.0)
            throw new UnstableSystemException(this);
    }
}