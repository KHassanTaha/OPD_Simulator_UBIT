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

    /// <summary>Per-patient queue waits at this stage, in the order observed (FR-UI-4 P2 chart).</summary>
    /// <remarks>
    /// Each sample carries the simulation time the wait was observed at, so a wait
    /// can be attributed to an operating session without inferring it from sample
    /// order (D-201, FR-STAT-9). Empty for single-stage M/M/1 runs that opt out.
    /// Consumers that only need the wait read <see cref="WaitSample.Minutes"/>, so
    /// the histogram is unchanged by the timestamp.
    /// </remarks>
    public IReadOnlyList<WaitSample> WaitingTimeSamples { get; init; } = Array.Empty<WaitSample>();

    /// <summary>Queue length sampled once per processed event — the P2 queue-length-over-time line series.</summary>
    public IReadOnlyList<QueueSample> QueueLengthSeries { get; init; } = Array.Empty<QueueSample>();

    /// <summary>
    /// Patients present in this stage (waiting or in service) when arrivals closed
    /// — the part of the admitted load the session did not get to (D-191).
    /// </summary>
    /// <remarks>
    /// For a multi-day run this is the final day's figure; the per-day series behind
    /// the average lives on <see cref="SimulationResult.BacklogPerSession"/>. 0 when the
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
    /// <see cref="SimulationResult.DrainPerSession"/>.
    /// </remarks>
    public double DrainMinutes { get; init; }

    /// <summary>
    /// Patients still queued or in service at the close of each operating session,
    /// in session order (D-193).
    /// </summary>
    /// <remarks>
    /// <see cref="BacklogAtClose"/> is the FINAL session's figure and means exactly
    /// that. This series is the per-session detail behind a multi-day average; where
    /// both are shown they are labelled "Final session" and "Mean across N sessions"
    /// so neither can be mistaken for the other.
    /// </remarks>
    public IReadOnlyList<int> BacklogAtCloseBySession { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Minutes from each session's close to this stage's last service end, in
    /// session order (D-193). <see cref="DrainMinutes"/> is the final session's
    /// figure; this series is the per-session detail.
    /// </summary>
    public IReadOnlyList<double> DrainMinutesBySession { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Patients whose service at this stage <i>completed</i> in each operating
    /// session, in session order (FR-STAT-9, D-201).
    /// </summary>
    /// <remarks>
    /// The sum over all sessions equals <see cref="PatientsServed"/>, which stays a
    /// whole-run figure — this is the per-session detail beside it, not a redefinition
    /// of it. A completion that spills past a session's close stays on the session
    /// that produced it, the same rule <see cref="DrainMinutesBySession"/> follows.
    /// </remarks>
    public IReadOnlyList<int> PatientsServedBySession { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Mean queue wait in minutes at this stage in each operating session, in session
    /// order; <see langword="null"/> where the session has no wait sample (FR-STAT-9).
    /// </summary>
    /// <remarks>
    /// The mean is taken over the wait samples whose observation time falls in that
    /// session, so a session that admitted nobody is <em>absent</em> rather than 0.0 —
    /// a mean over an empty set has no value, and printing 0.0 would read as "patients
    /// waited no time" (FR-STAT-9). The values sum to
    /// <see cref="AverageWaitMinutes"/> × <see cref="PatientsServed"/> over the sessions
    /// that have samples, which is the whole-run mean.
    /// </remarks>
    public IReadOnlyList<double?> MeanWaitMinutesBySession { get; init; } = Array.Empty<double?>();

    /// <summary>
    /// Time-weighted mean queue length at this stage in each operating session, in
    /// session order; <see langword="null"/> where the stage had no activity in that
    /// session (FR-STAT-9).
    /// </summary>
    /// <remarks>
    /// Denominator is that session's own operating minutes (first admitted arrival to
    /// last service end within the session), the per-session form of the D-018
    /// denominator, so the figures are never diluted by overnight gaps or closed days.
    /// A session in which the stage neither served nor held anyone waiting is
    /// <em>absent</em> for the same reason the wait mean is. A session where patients
    /// waited but none were served yet <i>does</i> report a queue-length mean: the
    /// queue genuinely had people in it, and zero would be a false statement.
    /// </remarks>
    public IReadOnlyList<double?> MeanQueueLengthBySession { get; init; } = Array.Empty<double?>();
}
