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
}