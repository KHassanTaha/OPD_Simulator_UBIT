namespace OpdSimulator.Core.Engine;

using OpdSimulator.Core.Patients;

/// <summary>
/// Per-stage metrics produced by the engine for the final run report.
/// </summary>
/// <remarks>
/// One instance per stage in the serial network. Provides the per-stage
/// breakdown the viva needs: arrival rates derived from routing (D-007),
/// per-stage ρ (FR-STAT-6), and per-server utilisation (FR-STAT-7) that
/// makes imbalance immediately visible in the results panel.
/// </remarks>
public sealed record StageMetrics
{
    /// <summary>Human-readable stage name.</summary>
    public string StageName { get; init; } = string.Empty;

    /// <summary>Routing-derived arrival rate λᵢ at this stage (patients per minute, D-007).</summary>
    public double ArrivalRate { get; init; }

    /// <summary>Number of parallel servers c at this stage.</summary>
    public int ServerCount { get; init; }

    /// <summary>Service rate per server μ at this stage (patients per minute).</summary>
    public double ServiceRate { get; init; }

    /// <summary>Traffic intensity ρᵢ = λᵢ/(cᵢ·μᵢ).</summary>
    public double Rho { get; init; }

    /// <summary>Total number of patients that completed service at this stage.</summary>
    public int PatientsServed { get; init; }

    /// <summary>Average time patients waited at this stage's queue (minutes).</summary>
    public double AverageWaitMinutes { get; init; }

    /// <summary>Time-weighted average queue length at this stage.</summary>
    public double AverageQueueLength { get; init; }

    /// <summary>Stage-level utilisation: mean of per-server utilisations at this stage.</summary>
    public double StageUtilisation { get; init; }

    /// <summary>Per-server utilisation at this stage, one value per server in index order.</summary>
    public IReadOnlyList<double> PerServerUtilisation { get; init; } = Array.Empty<double>();

    /// <summary>Throughput at this stage = patients completed / operating time (patients per minute).</summary>
    public double ThroughputPerMinute { get; init; }

    /// <summary>Per-patient waiting times at this stage's queue, in completion order (FR-UI-4 P2 chart).</summary>
    /// <remarks>Populated only when the engine records samples; empty for single-stage M/M/1 runs that opt out.</remarks>
    public IReadOnlyList<double> WaitingTimeSamples { get; init; } = Array.Empty<double>();

    /// <summary>Queue length sampled once per processed event — the P2 queue-length-over-time line series.</summary>
    public IReadOnlyList<QueueSample> QueueLengthSeries { get; init; } = Array.Empty<QueueSample>();

    /// <summary>
    /// Patients present in this stage (waiting or in service) when arrivals closed
    /// — the part of the admitted load the session did not get to (D-191).
    /// </summary>
    /// <remarks>
    /// For a multi-day run this is the final day's figure; the per-day series behind
    /// the average lives on <see cref="SimulationResult.BacklogPerDay"/>. 0 when the
    /// run ended before the close, because then nothing was left over.
    /// </remarks>
    public int BacklogAtClose { get; init; }

    /// <summary>
    /// Minutes from the close of arrivals to this stage's last service completion —
    /// how long the stage took to clear what was left at the close (D-191).
    /// </summary>
    /// <remarks>
    /// 0 for a stage that had already finished serving before the close. A
    /// multi-day run reports the final day's drain here; the per-day series is on
    /// <see cref="SimulationResult.DrainPerDay"/>.
    /// </remarks>
    public double DrainMinutes { get; init; }
}