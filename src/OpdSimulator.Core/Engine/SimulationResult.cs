namespace OpdSimulator.Core.Engine;

/// <summary>
/// One point of a stage's queue-length-over-time series (FR-UI-4 P2 chart).
/// Sampled once per processed event so the line chart and the time-weighted
/// average (area ÷ operating time) use the exact same observables.
/// </summary>
/// <param name="Time">Simulation clock minutes at the sample.</param>
/// <param name="Length">Number of patients waiting in that stage's queue.</param>
public readonly record struct QueueSample(double Time, int Length);

/// <summary>
/// Immutable report of a completed simulation run.
/// </summary>
/// <remarks>
/// Produced once by <see cref="Engine.Run"/> after the FEL drains. All times
/// are in minutes. Utilisation denominators follow the operating-time
/// definition in D-017/D-018 (time from the first arrival to the last service
/// end).
/// </remarks>
public sealed class SimulationResult
{
    /// <summary>Total number of patients fully served by the end of the run.</summary>
    public int TotalPatientsServed { get; init; }

    /// <summary>Average time patients spent waiting in queue (minutes).</summary>
    public double AverageWaitMinutes { get; init; }

    /// <summary>Time-weighted average number of patients waiting in queue.</summary>
    public double AverageQueueLength { get; init; }

    /// <summary>Average time patients spent in the system, queue included (minutes).</summary>
    public double AverageSystemTimeMinutes { get; init; }

    /// <summary>Stage-level utilisation: mean of per-server utilisations.</summary>
    public double StageUtilisation { get; init; }

    /// <summary>Per-server utilisation, one value per server in index order.</summary>
    public IReadOnlyList<double> PerServerUtilisation { get; init; } = Array.Empty<double>();

    /// <summary>Throughput = total patients served / operating time (patients per minute).</summary>
    public double ThroughputPerMinute { get; init; }

    /// <summary>Operating time (minutes): first arrival to last service end.</summary>
    public double OperatingTimeMinutes { get; init; }

    /// <summary>
    /// Per-stage metrics for the serial network. A single-element array for the
    /// legacy single-stage model; one entry per stage for the multi-stage topology.
    /// </summary>
    public IReadOnlyList<StageMetrics> StageMetrics { get; init; } = Array.Empty<StageMetrics>();

    /// <summary>
    /// Patients admitted per calendar-day block. Only populated by a
    /// calendar-aware run (<see cref="Engine.Run(NetworkTopology, ClinicCalendar, int, int, int?)"/>);
    /// closed days hold 0. Empty for a plain horizon run.
    /// </summary>
    public IReadOnlyList<int> AdmittedPerDay { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Number of calendar days over which arrivals were generated in a
    /// calendar-aware run; 0 for a plain horizon run.
    /// </summary>
    public int GeneratorDays { get; init; }

    /// <summary>
    /// If this run was calendar-aware and capped, the daily admission cap;
    /// null otherwise.
    /// </summary>
    public int? DailyCap { get; init; }
}