using OpdSimulator.Core.Calendar;

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
    /// Patients admitted per operating session. Only populated by a
    /// calendar-aware run (<see cref="Engine.Run(NetworkTopology, ClinicCalendar, int, int, int?)"/>).
    /// One entry per session, so a closed day contributes no entry at all
    /// (FR-SIM-12). Empty for a plain horizon run.
    /// </summary>
    public IReadOnlyList<int> AdmittedPerSession { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Screening-bound admissions per operating session — the subset the daily cap
    /// actually constrains. A session can hold more entries here than
    /// <see cref="AdmittedPerSession"/> minus nothing: bypass arrivals land in
    /// <see cref="AdmittedPerSession"/> but not here, because they skip the screening
    /// session the cap models (D-190).
    /// </summary>
    /// <remarks>Empty for a plain horizon run.</remarks>
    public IReadOnlyList<int> ScreeningAdmittedPerSession { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Patients still in the system when each operating session's arrivals closed —
    /// the admitted load left waiting at the close (D-191). One entry per session;
    /// a closed day contributes no entry (FR-SIM-12).
    /// </summary>
    /// <remarks>Empty for a plain horizon run.</remarks>
    public IReadOnlyList<int> BacklogPerSession { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Minutes from each operating session's close of arrivals to that session's
    /// last service end — the time taken to clear that session's backlog (D-191).
    /// </summary>
    /// <remarks>Empty for a plain horizon run.</remarks>
    public IReadOnlyList<double> DrainPerSession { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Number of operating sessions the run covered — the count the user asked
    /// for, where closed weekdays between them are stepped over and NOT counted
    /// (FR-SIM-12, D-199); 0 for a plain horizon run.
    /// </summary>
    /// <remarks>
    /// Named for the field the config panel binds to rather than for the unit: the
    /// value is identical to the requested Days setting. Use
    /// <see cref="Sessions"/> when the weekdays or block indices are needed, and
    /// never read this as a count of 1440-minute blocks — from a Saturday start,
    /// 4 sessions span 5.
    /// </remarks>
    public int GeneratorDays { get; init; }

    /// <summary>
    /// The operating sessions this run covered, in order: each one's 1-based
    /// ordinal, the 1440-minute block it occupies, and its weekday (FR-SIM-12).
    /// Empty for a plain horizon run.
    /// </summary>
    /// <remarks>
    /// Every per-session series on this result is indexed by the position in this
    /// list, so index <c>i</c> is <c>Sessions[i]</c> — which is what lets a label
    /// read "Day 3 (Tue)" instead of guessing from a clock time.
    /// </remarks>
    public IReadOnlyList<ClinicSession> Sessions { get; init; } = Array.Empty<ClinicSession>();

    /// <summary>
    /// If this run was calendar-aware and capped, the daily admission cap;
    /// null otherwise.
    /// </summary>
    public int? DailyCap { get; init; }

    /// <summary>
    /// Inter-arrival times actually generated by the engine during
    /// the run, in minutes. Empty when the run scheduled no
    /// arrivals.
    /// </summary>
    public IReadOnlyList<double> GeneratedInterArrivalSamples { get; init; }
        = Array.Empty<double>();

    /// <summary>
    /// Service times actually generated by the engine during the
    /// run, in minutes. One list per stage; index matches
    /// NetworkTopology.Stages order.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<double>> GeneratedServiceSamplesByStage
        { get; init; } = Array.Empty<IReadOnlyList<double>>();
}