namespace OpdSimulator.Core.Engine;

using System.Globalization;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Events;
using OpdSimulator.Core.Patients;
using OpdSimulator.Core.Servers;
using OpdSimulator.Core.Stages;
using OpdSimulator.Core.Trace;
using Serilog;

/// <summary>
/// Discrete-event simulation engine for the single-stage M/M/c queue and the
/// three-stage serial OPD network.
/// </summary>
/// <remarks>
/// <para>
/// Loop (CONTEXT §4.3): seed → schedule first arrival at t=0 → repeatedly pop
/// the earliest event from the <see cref="FEL"/>, advance the clock to its
/// time, and handle it. New arrivals are scheduled only while their time is
/// strictly inside the arrival window <c>[0, horizon)</c> (FR-SIM-5); services
/// already in progress continue past the horizon to completion (FR-SIM-6).
/// The run ends when the FEL drains (kickoff clarification: "stop when
/// clock ≥ horizon AND FEL empty").
/// </para>
/// <para>
/// Determinism: <see cref="IRandomSource.SetSeed"/> is called before the loop,
/// so the exact same seed reproduces the exact same event sequence (NFR-4).
/// Every event is logged at Debug level with time, type, patient id, queue
/// lengths, server status and the RNG draws it consumed (FR-VAL-4).
/// </para>
/// <para>
/// The engine is N-stage generic (D-006): <see cref="Run(NetworkTopology, int, double)"/>
/// executes any ordered <see cref="NetworkTopology"/>, where a stage at index
/// <c>i</c> completes via the <c>(i+1)</c>-th <see cref="EventType"/>
/// (ReceptionEnd = 1, ScreeningEnd = 2, DoctorEnd = 3). A service completion
/// either routes the patient to the next stage or, at the exit stage with
/// probability <c>p_exit</c>, records the patient as done (FR-SIM-3). Idle
/// servers are picked <see cref="RandomIdleSelection">at random</see> (D-017).
/// The legacy single-stage <see cref="Run()"/> is exactly a one-stage topology,
/// so Milestone-1 metrics stay numerically identical (output text excluded —
/// D-054).
/// </para>
/// </remarks>
public sealed class Engine
{
    private readonly EngineConfig? _config;
    private readonly TraceRandomSource _random;
    private readonly ExponentialSampler _interarrivalSampler;

    // One service-time sampler per stage, rebuilt at the start of every run from
    // each StageSpec's EffectiveServiceDistribution (Phase 8I). Before 8I a single
    // shared exponential sampler served every stage, so a stage could not be
    // anything but M/M/c. Inter-arrivals deliberately stay exponential: the arrival
    // process is M/M/c by definition and 8I changes only the service side.
    private IDistributionSampler[]? _serviceSamplers;
    private readonly IServerSelectionPolicy _serverSelection;
    private readonly ILogger _log;
    private ITraceSink? _traceSink;

    // Per-run chart samples (FR-UI-4 P2): waiting-time histograms and the
    // queue-length-over-time lines. Recorded only while a run is active, then
    // handed into StageMetrics and released so a later run starts clean.
    private List<double>[]? _stageWaitSamples;
    private List<QueueSample>[]? _stageQueueSeries;

    // Close-of-session bookkeeping (D-191): patients present per stage when
    // arrivals stopped, and each stage's last service end, which together give
    // the backlog and the drain time. All per-run, released like the buffers above.
    private double[]? _stageLastServiceEnd;
    private int[]? _backlogAtClose;
    private int[][]? _backlogPerSessionAtClose;
    private bool[]? _sessionBacklogCaptured;
    private bool _backlogCaptured;

    // Per-run generated-value samples (Phase 8A): the inter-arrival and service
    // times the RNG actually produced, retained so a later goodness-of-fit pass
    // can test the simulation output itself. Always recorded; released at the
    // end of the run like the chart buffers.
    private List<double>? _generatedInterArrivals;
    private List<List<double>>? _generatedServiceSamples;

    /// <summary>
    /// Creates an engine for a network run (no single-stage <see cref="EngineConfig"/>).
    /// </summary>
    /// <param name="random">The random source (must support seeding for reproducibility).</param>
    /// <param name="log">Serilog logger for the event trace.</param>
    /// <param name="serverSelection">Idle-server selection policy; defaults to
    /// <see cref="RandomIdleSelection"/> (D-017).</param>
    public Engine(IRandomSource random, ILogger log, IServerSelectionPolicy? serverSelection = null)
    {
        if (random is null)
            throw new ArgumentNullException(nameof(random));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _serverSelection = serverSelection ?? new RandomIdleSelection();

        // Every run draws through the transparent TraceRandomSource wrapper
        // (D-057): it forwards every draw unchanged and merely counts them, so
        // the engine can report what the RNG supplied without altering the
        // draw sequence that the Milestone-1 regression is pinned to.
        _random = new TraceRandomSource(random);
        _interarrivalSampler = new ExponentialSampler(_random);
    }

    /// <summary>
    /// Creates an engine configured for the legacy single-stage M/M/c run.
    /// </summary>
    /// <param name="config">The run configuration (λ, μ, c, horizon, seed).</param>
    /// <param name="random">The random source (must support seeding for reproducibility).</param>
    /// <param name="log">Serilog logger for the event trace.</param>
    public Engine(EngineConfig config, IRandomSource random, ILogger log)
        : this(random, log)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Runs the classic single-stage M/M/c configuration to completion and
    /// returns the aggregate metrics.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="Run(NetworkTopology, int, double)"/> with a
    /// one-stage topology built from the configuration. The event sequence and
    /// arithmetic are identical to the original Milestone-1 loop, so the known
    /// regression (seed 42: served = 29892, wait = 0.724) is a metric-level
    /// guard on this path (output text excluded — D-054).
    /// </remarks>
    /// <returns>The collected statistics of the run.</returns>
    public SimulationResult Run()
    {
        if (_config is null)
            throw new InvalidOperationException("No single-stage configuration was supplied; run a NetworkTopology instead.");

        // Keep Milestone-1 validation semantics (including the horizon bound)
        // before delegation.
        _config.Validate(); // throws UnstableSystemException when ρ ≥ 1 (FR-VAL-1)

        var topology = NetworkTopology.CreateSingleStage(
            _config.ArrivalRate, _config.ServiceRate, _config.ServerCount, _config.StageName);

        return Run(topology, _config.Seed, _config.HorizonMinutes, _config.TraceSink);
    }

    /// <summary>
    /// Runs a serial network topology to completion and returns the aggregate
    /// plus per-stage metrics.
    /// </summary>
    /// <param name="topology">The ordered stage configuration to simulate.</param>
    /// <param name="seed">Random seed for reproducibility (FR-VAL-3, default 42).</param>
    /// <param name="horizonMinutes">Length of the arrival-generation window in minutes (arrivals stop beyond it).</param>
    /// <param name="traceSink">Optional sink that receives the human-readable
    /// event trace (<see cref="ITraceSink"/>); null disables tracing.</param>
    /// <param name="maxCompletedPatients">Optional early stop: the run ends once
    /// this many patients have fully exited the system (used by the <c>trace</c>
    /// CLI to cap a trace at a fixed number of completions).</param>
    /// <returns>The collected statistics of the run.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="topology"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the horizon is not strictly positive or
    /// <paramref name="maxCompletedPatients"/> is less than 1.</exception>
    /// <exception cref="UnstableSystemException">If any stage has ρ ≥ 1 (FR-VAL-1).</exception>
    public SimulationResult Run(NetworkTopology topology, int seed = SeededRandomSource.DefaultSeed, double horizonMinutes = 10000,
        ITraceSink? traceSink = null, int? maxCompletedPatients = null)
    {
        if (topology is null)
            throw new ArgumentNullException(nameof(topology));
        if (horizonMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes), horizonMinutes, "Horizon must be strictly positive.");
        if (maxCompletedPatients is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCompletedPatients), maxCompletedPatients, "The completion cap must be at least 1.");

        return RunCore(topology, seed, horizonMinutes, null, generatorDays: 0, dailyCap: null, traceSink, maxCompletedPatients);
    }

    /// <summary>
    /// Runs a serial network topology over a clinic calendar and returns the
    /// aggregate plus per-stage metrics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The calendar anchors t = 0 at the day-0 arrival window (CONTEXT §5.1).
    /// Arrivals are generated as one continuous Poisson stream and admitted only
    /// on open weekdays inside the arrival window; arrivals landing elsewhere
    /// are gated out but the stream continues (the clinic's demand exists, the
    /// calendar is the admission gate). Services in progress keep running to
    /// completion, so a run drains past the last window (FR-SIM-6).
    /// </para>
    /// <para>
    /// <paramref name="generatorDays"/> counts operating sessions, not calendar
    /// days: closed weekdays between them are stepped over and not counted
    /// (FR-SIM-12, D-199). A 4-session run from Saturday spans five 1440-minute
    /// blocks — Sat, closed Sun, Mon, Tue, Wed — and its per-session series carry
    /// four entries with no row for the Sunday. The resolved list is on
    /// <see cref="SimulationResult.Sessions"/>.
    /// </para>
    /// <para>
    /// Operating time (D-018) is summed per operating session: first admitted
    /// arrival to last service end within that session, so overnight gaps never
    /// dilute utilisation. The optional <paramref name="dailyCap"/> resets each
    /// session (D-009).
    /// </para>
    /// </remarks>
    /// <param name="topology">The ordered stage configuration to simulate.</param>
    /// <param name="calendar">The clinic schedule (weekdays + arrival window).</param>
    /// <param name="generatorDays">Number of operating sessions over which arrivals are generated; closed weekdays between them are skipped and not counted (FR-SIM-12).</param>
    /// <param name="seed">Random seed for reproducibility (FR-VAL-3, default 42).</param>
    /// <param name="dailyCap">Maximum admissions per operating session; null = unlimited.</param>
    /// <param name="traceSink">Optional sink that receives the human-readable
    /// event trace (<see cref="ITraceSink"/>); null disables tracing.</param>
    /// <returns>The collected statistics of the run, including <see cref="SimulationResult.AdmittedPerSession"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="topology"/> or <paramref name="calendar"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="generatorDays"/> &lt; 1 or <paramref name="dailyCap"/> &lt; 1.</exception>
    /// <exception cref="UnstableSystemException">If any stage has ρ ≥ 1 (FR-VAL-1).</exception>
    public SimulationResult Run(NetworkTopology topology, ClinicCalendar calendar, int generatorDays,
        int seed = SeededRandomSource.DefaultSeed, int? dailyCap = null, ITraceSink? traceSink = null)
    {
        if (topology is null)
            throw new ArgumentNullException(nameof(topology));
        if (calendar is null)
            throw new ArgumentNullException(nameof(calendar));
        if (generatorDays < 1)
            throw new ArgumentOutOfRangeException(nameof(generatorDays), generatorDays, "At least one operating session must be generated.");
        if (dailyCap is < 1)
            throw new ArgumentOutOfRangeException(nameof(dailyCap), dailyCap, "The daily cap must be at least 1.");

        // The trace sink is forwarded (default null keeps calendar runs
        // trace-free exactly as before), matching the horizon overload.
        return RunCore(topology, seed, horizonMinutes: 0, calendar, generatorDays, dailyCap, traceSink, maxCompletedPatients: null);
    }

    private SimulationResult RunCore(
        NetworkTopology topology,
        int seed,
        double horizonMinutes,
        ClinicCalendar? calendar,
        int generatorDays,
        int? dailyCap,
        ITraceSink? traceSink,
        int? maxCompletedPatients)
    {
        // Stability check first: refuses with every unstable stage listed
        // (FR-VAL-1). The RNG is seeded only after the check, so a refusal
        // never consumes draws.
        topology.Validate();


        // Per-run trace target; null means no tracing for this run. Seeding the
        // wrapper resets its draw counter, so draw numbers always start at #1.
        _traceSink = traceSink;
        _random.SetSeed(seed);
        if (_traceSink is not null)
            EmitTrace(TraceEventType.Rng, 0, patientId: null, stageName: null, serverId: null, queueLength: null, details: $"seed={seed}");
        if (calendar is null)
        {
            _log.Information("Simulation start: stages={Stages} λ0={ArrivalRate} seed={Seed} horizon={Horizon} ρs=[{Rhos}]",
                string.Join(" → ", topology.StageSpecs.Select(s => $"{s.Name}(c={s.ServerCount}, μ={s.ServiceRate:F3})")),
                topology.ArrivalRate, seed, horizonMinutes,
                string.Join(",", Enumerable.Range(0, topology.StageSpecs.Count).Select(i => topology.RhoFor(i).ToString("0.##"))));
        }
        else
        {
            _log.Information("Simulation start: stages={Stages} λ0={ArrivalRate} seed={Seed} sessions={Sessions} window={Window} cap={Cap} start-day={StartDay} ρs=[{Rhos}]",
                string.Join(" → ", topology.StageSpecs.Select(s => $"{s.Name}(c={s.ServerCount}, μ={s.ServiceRate:F3})")),
                topology.ArrivalRate, seed, generatorDays,
                $"{calendar.FormatClock(0)}–{calendar.FormatClock(calendar.OpenDurationMinutes)}",
                dailyCap?.ToString() ?? "∞", calendar.StartDayOfWeek,
                string.Join(",", Enumerable.Range(0, topology.StageSpecs.Count).Select(i => topology.RhoFor(i).ToString("0.##"))));
        }

        // Fresh per-run runtime state: queues and servers never leak between runs.
        var gate = calendar is null ? null : new CalendarGate(calendar, generatorDays, dailyCap, topology.StageSpecs.Count);

        var stages = Enumerable.Range(0, topology.StageSpecs.Count)
            .Select(i => topology.CreateStage(i))
            .ToArray();

        // Per-stage service samplers (Phase 8I). Each stage dispatches on its own
        // family, so M/D/c and M/M/c and M/E2/c can sit in the same network.
        //
        // Building the samplers consumes no draws — the factory only validates the
        // spec — so this does not perturb the RNG stream. For a stage with no
        // explicit distribution the spec resolves to Exponential with mean 1/μ, and
        // ExponentialDistributionSampler draws exactly one uniform and applies the
        // same -ln(U)/λ inverse CDF as the legacy ExponentialSampler did. The
        // exponential draw sequence is therefore unchanged, which is what keeps the
        // D-054 regression (seed 42: served = 29892, wait = 0.724) passing.
        _serviceSamplers = topology.StageSpecs
            .Select(spec => DistributionSamplerFactory.Create(spec.EffectiveServiceDistribution, _random))
            .ToArray();

        var fel = new FEL();

        var inService = new Dictionary<int, Patient>();          // patientId -> patient being served
        var patientServer = new Dictionary<int, Server>();       // patientId -> server serving them

        double clock = 0;
        var areaUnderQueue = new double[stages.Length];          // time-integrated queue length per stage (§4 statistics)
        var lastMetricTime = new double[stages.Length];
        var stageWaitMinutes = new double[stages.Length];        // total wait accumulated at each stage
        double totalSystemMinutes = 0;                           // whole-journey times of completed patients
        int completed = 0;
        int nextPatientId = 1;

        // Chart-sample buffers, released into StageMetrics at the end.
        _stageWaitSamples = Enumerable.Range(0, stages.Length).Select(_ => new List<double>()).ToArray();
        _stageQueueSeries = Enumerable.Range(0, stages.Length).Select(_ => new List<QueueSample>()).ToArray();

        // Close-of-session buffers (D-191). The per-day arrays exist only for a
        // calendar run; a horizon run has a single close.
        _stageLastServiceEnd = new double[stages.Length];
        _backlogAtClose = new int[stages.Length];
        _backlogPerSessionAtClose = gate is null
            ? null
            : Enumerable.Range(0, gate.SessionCount).Select(_ => new int[stages.Length]).ToArray();
        _sessionBacklogCaptured = gate is null ? null : new bool[gate.SessionCount];
        _backlogCaptured = false;

        // Generated-value buffers (Phase 8A): one inter-arrival list and one
        // service-time list per stage. Always retained, released after the run.
        _generatedInterArrivals = new List<double>();
        _generatedServiceSamples = Enumerable.Range(0, stages.Length).Select(_ => new List<double>()).ToList();

        // First arrival at t = 0 (CONTEXT §4.3).
        fel.Enqueue(new Event(0, EventType.Arrival, nextPatientId));

        // Arrivals stop at this instant, and the run then drains whatever is still
        // in the system (FR-SIM-6). It is the "close" the backlog is measured at:
        // the admitted load that arrived inside the session and had not yet been
        // served when the session ended (D-191).
        double closeTime = gate is not null ? gate.StopTime : horizonMinutes;

        while (fel.Count > 0)
        {
            Event evt = fel.Dequeue();
            clock = evt.Time;

            // Calendar bookkeeping must be current before any admission or
            // service-accounting decision in this event.
            gate?.AdvanceTo(clock);

            // Capture the backlog at the close of arrivals (D-191). Events are
            // processed in time order and the run always has one at or after the
            // close whenever anyone was still in the system, so this lands exactly
            // on the close and never twice. A run that ends before the close never
            // had a backlog to clear, so it stays at zero.
            if (gate is not null)
            {
                // Calendar run: captured at the close of EACH operating session, so
                // a multi-day run can report a per-session average rather than one
                // number for the whole horizon. The gate attributes trailing drain
                // to the session that produced it, so work spilling past midnight —
                // or across a closed day — stays on the session it belongs to.
                int session = gate.CurrentSessionIndex;
                if (!_sessionBacklogCaptured![session] && clock >= gate.CloseTimeForSession(session))
                {
                    for (int i = 0; i < stages.Length; i++)
                        _backlogPerSessionAtClose![session][i] = stages[i].Queue.Count + stages[i].Servers.Count(s => s.IsBusy);
                    _sessionBacklogCaptured![session] = true;
                }
            }
            else if (!_backlogCaptured && clock >= closeTime)
            {
                // Horizon run: there is one close, at the end of the arrival window.
                for (int i = 0; i < stages.Length; i++)
                    _backlogAtClose![i] = stages[i].Queue.Count + stages[i].Servers.Count(s => s.IsBusy);
                _backlogCaptured = true;
            }

            // Time-weighted queue length: each stage's queue sampled throughout
            // the interval [lastMetricTime, clock] held its previous length.
            for (int i = 0; i < stages.Length; i++)
            {
                areaUnderQueue[i] += stages[i].Queue.Count * (clock - lastMetricTime[i]);
                lastMetricTime[i] = clock;

                // One (time, length) point per event per stage — the P2 line series.
                _stageQueueSeries![i].Add(new QueueSample(clock, stages[i].Queue.Count));
            }

            _log.Debug("t={Clock:0.###} {Type} patient={PatientId} queueLen={QueueLen} stageQueues=[{StageQueueLens}] servers=[{ServerStates}]",
                clock, evt.Type, evt.PatientId, stages[0].Queue.Count,
                string.Join(",", stages.Select(s => s.Queue.Count)), ServerStateString(stages));

            switch (evt.Type)
            {
                case EventType.Arrival:
                    HandleArrival(evt, clock, topology, stages, topology.ArrivalRate, fel,
                        inService, patientServer, ref nextPatientId, stageWaitMinutes,
                        gate, horizonMinutes);
                    break;

                case EventType.ReceptionEnd:
                case EventType.ScreeningEnd:
                case EventType.DoctorEnd:
                    HandleServiceEnd(evt, clock, stages, topology, fel, inService, patientServer,
                        stageWaitMinutes, ref totalSystemMinutes, ref completed, gate);
                    break;

                default:
                    // The five event types above are the only ones the engine can
                    // schedule; anything else indicates an internal bug.
                    throw new InvalidOperationException($"Event type {evt.Type} is not supported by the engine.");
            }

            // Optional early stop (trace runs): once the requested number of
            // patients have fully left the system, stop exactly at the next
            // event boundary so the partial metric state stays consistent.
            if (maxCompletedPatients is { } cap && completed >= cap)
                break;
        }

        // Operating time = time from first arrival (t=0) to last service end
        // (D-018). In a horizon run the FEL drains at the last service end, so
        // `clock` is exactly that (FR-SIM-6). In a calendar run the denominator
        // is summed per operating session so overnight gaps and closed days do not
        // dilute utilisation.
        double operatingTime = gate is not null ? gate.OperatingTimeMinutes : clock;

        var stageMetrics = stages.Select((stage, i) =>
        {
            var perServerUtil = stage.Servers
                .Select(s => s.Utilisation(operatingTime)) // asserts 0 ≤ util ≤ 1 (FR-VAL-2)
                .ToArray();
            int servedHere = stage.Servers.Sum(s => s.PatientsServed);

            // Drain is measured from the close of arrivals to the stage's last
            // service end. A stage that finished serving before the close — or that
            // never saw a patient — has nothing to drain, hence the clamp (D-191).
            double drain = Math.Max(0.0, _stageLastServiceEnd![i] - closeTime);

            return new StageMetrics
            {
                StageName = stage.Name,
                ArrivalRate = stage.EffectiveArrivalRate,
                ServerCount = stage.ServerCount,
                ServiceRate = stage.ServiceRate,
                Rho = stage.Rho,
                PatientsServed = servedHere,
                AverageWaitMinutes = servedHere > 0 ? stageWaitMinutes[i] / servedHere : 0,
                AverageQueueLength = operatingTime > 0 ? areaUnderQueue[i] / operatingTime : 0,
                StageUtilisation = perServerUtil.Length > 0 ? perServerUtil.Average() : 0,
                PerServerUtilisation = perServerUtil,
                ThroughputPerMinute = operatingTime > 0 ? servedHere / operatingTime : 0,
                WaitingTimeSamples = _stageWaitSamples![i],
                QueueLengthSeries = _stageQueueSeries![i],
                // The scalars keep their original meaning: the state at the FINAL
                // close of the run (D-193). The series carries one entry per
                // operating session, so a multi-day average can be computed from
                // real per-stage numbers instead of one day's figure reused.
                BacklogAtClose = gate is not null
                    ? _backlogPerSessionAtClose![gate.SessionCount - 1][i]
                    : _backlogAtClose![i],
                DrainMinutes = drain,
                BacklogAtCloseBySession = gate is null
                    ? new[] { _backlogAtClose![i] }
                    : Enumerable.Range(0, gate.SessionCount)
                        .Select(s => _backlogPerSessionAtClose![s][i]).ToArray(),
                DrainMinutesBySession = gate is null
                    ? new[] { drain }
                    : Enumerable.Range(0, gate.SessionCount)
                        .Select(s => gate.DrainMinutesForSession(s, i)).ToArray(),
            };
        }).ToArray();

        // Per-session close-of-session figures for a multi-day average (D-191). The
        // backlog is the whole system's count at that session's close; the drain is
        // that session's close to its last service end, reusing the same service-end
        // bookkeeping the operating-time denominator is built from. One entry per
        // operating session — a closed day has no session and so contributes no row
        // (FR-SIM-12).
        int[] backlogPerSession = gate is null
            ? Array.Empty<int>()
            : Enumerable.Range(0, gate.SessionCount)
                .Select(s => _backlogPerSessionAtClose![s].Sum())
                .ToArray();
        double[] drainPerSession = gate is null
            ? Array.Empty<double>()
            : Enumerable.Range(0, gate.SessionCount)
                .Select(s => gate.DrainMinutesForSession(s))
                .ToArray();

        // Release the per-run buffers; a later run allocates fresh ones.
        var generatedInterArrivals = _generatedInterArrivals!;
        var generatedServiceSamplesByStage = _generatedServiceSamples!
            .Select(samples => (IReadOnlyList<double>)samples)
            .ToList();
        _stageWaitSamples = null;
        _stageQueueSeries = null;
        _stageLastServiceEnd = null;
        _backlogAtClose = null;
        _backlogPerSessionAtClose = null;
        _sessionBacklogCaptured = null;
        _generatedInterArrivals = null;
        _generatedServiceSamples = null;
        _serviceSamplers = null;

        double[] perServerUtilisation = stageMetrics
            .SelectMany(m => m.PerServerUtilisation)
            .ToArray();

        return new SimulationResult
        {
            TotalPatientsServed = completed,
            AverageWaitMinutes = completed > 0 ? stageWaitMinutes.Sum() / completed : 0,
            AverageSystemTimeMinutes = completed > 0 ? totalSystemMinutes / completed : 0,
            AverageQueueLength = stageMetrics.Length > 0 ? stageMetrics.Average(m => m.AverageQueueLength) : 0,
            StageUtilisation = stageMetrics.Length > 0 ? stageMetrics.Average(m => m.StageUtilisation) : 0,
            PerServerUtilisation = perServerUtilisation,
            ThroughputPerMinute = operatingTime > 0 ? completed / operatingTime : 0,
            OperatingTimeMinutes = operatingTime,
            StageMetrics = stageMetrics,
            AdmittedPerSession = gate?.AdmittedPerSession ?? Array.Empty<int>(),
            ScreeningAdmittedPerSession = gate?.ScreeningAdmittedPerSession ?? Array.Empty<int>(),
            BacklogPerSession = backlogPerSession,
            DrainPerSession = drainPerSession,
            Sessions = gate?.Sessions ?? Array.Empty<ClinicSession>(),
            GeneratorDays = gate?.SessionCount ?? 0,
            DailyCap = gate?.DailyCap,
            GeneratedInterArrivalSamples = generatedInterArrivals,
            GeneratedServiceSamplesByStage = generatedServiceSamplesByStage,
        };
    }

    private void HandleArrival(
        Event evt,
        double clock,
        NetworkTopology topology,
        IReadOnlyList<Stage> stages,
        double arrivalRate,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        ref int nextPatientId,
        double[] stageWaitMinutes,
        CalendarGate? gate,
        double horizonMinutes)
    {
        // A calendar run admits only arrivals that land inside an open-day
        // window; everything else is gated out but the Poisson stream still
        // advances (the demand exists, the calendar is the admission gate).
        //
        // The window is checked FIRST and on its own, before any routing draw. A
        // patient who cannot be admitted never reaches a routing decision, and a
        // bypass arrival is subject to the opening hours exactly like a
        // Screening-bound one (D-190).
        bool inWindow = gate is null || gate.IsInWindow(clock);

        // When the skipped stage is the first one, the bypass draw happens HERE at
        // the arrival event rather than at a service completion (D-189). This is the
        // 2-stage clinic topology: a bypassed patient never enters Screening at all,
        // which is what makes λ_screening = λ₀(1 − p_bypass) in the first place.
        bool bypassed = false;
        if (inWindow && topology.BypassAtArrival)
        {
            double u = _random.NextDouble();
            bypassed = u < topology.BypassProbability;
            string outcome = bypassed
                ? $"skip stage {topology.BypassStageIndex} ({stages[topology.BypassStageIndex].Name})"
                : $"enter stage {topology.BypassStageIndex} ({stages[topology.BypassStageIndex].Name})";
            EmitRngDraw(clock, FormattableString.Invariant(
                $"arrival-time bypass draw U={u:0.####} vs p_bypass={topology.BypassProbability:0.####} → {outcome}"));
            _log.Debug("    -> arrival bypass draw={Draw:0.####} p_bypass={PBypass:0.####}", u, topology.BypassProbability);
        }

        int destinationIndex = bypassed ? topology.BypassDestinationIndex : 0;
        var stage = stages[destinationIndex];

        // Only a Screening-bound arrival consumes a place under the daily cap.
        if (!inWindow || !(gate is null || gate.TryAdmit(clock, screeningBound: destinationIndex == 0)))
        {
            _log.Debug("    -> arrival not admitted (outside the window, or over the daily screening cap): patient={PatientId}", evt.PatientId);
        }
        else
        {
            var patient = new Patient(evt.PatientId, clock, stageIndex: destinationIndex);

            // Arrival row: the arriving patient is present in the stage whether
            // they are queued or served at once, so q = queue + 1.
            EmitTrace(TraceEventType.Arrival, clock, patient.Id, stage.Name, serverId: null, stage.Queue.Count + 1, details: null);

            if (stage.HasIdleServer)
            {
                int drawsBefore = _random.DrawCount;
                var server = _serverSelection.SelectIdleServer(stage.Servers, _random);
                EmitRngDrawIfDrawn(clock, drawsBefore, $"select idle server at {stage.Name} → server {server.Id}");
                StartService(patient, server, stage, clock, fel, inService, patientServer, stageWaitMinutes);
            }
            else
            {
                stage.Queue.Enqueue(patient);
                _log.Debug("    -> queued patient={PatientId}, queueLen={QueueLen}", patient.Id, stage.Queue.Count);
            }
        }

        // Schedule the next arrival only inside the arrival window — horizon
        // mode stops at [0, horizon) (FR-SIM-5), calendar mode stops at the
        // end of the last generated day's window. The inter-arrival draw is
        // logged (FR-VAL-4).
        double interArrival = _interarrivalSampler.Sample(arrivalRate);
        double nextArrivalTime = clock + interArrival;
        _log.Debug("    -> inter-arrival draw={Draw:0.####} min, next arrival t={Time:0.###}",
            interArrival, nextArrivalTime);
        EmitRngDraw(clock, FormattableString.Invariant(
            $"inter-arrival {interArrival:0.000} min (next arrival at t={nextArrivalTime:0.000})"));

        double stopTime = gate is not null ? gate.StopTime : horizonMinutes;
        if (nextArrivalTime < stopTime)
        {
            // Retain the draw that actually produced the next arrival (Phase 8A);
            // a draw beyond the window is discarded exactly as before.
            _generatedInterArrivals!.Add(interArrival);
            nextPatientId++;
            fel.Enqueue(new Event(nextArrivalTime, EventType.Arrival, nextPatientId));
        }
    }

    private void StartService(
        Patient patient,
        Server server,
        Stage stage,
        double clock,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        double[] stageWaitMinutes)
    {
        patient.MarkServiceStarted(clock);
        server.StartService(clock);
        inService[patient.Id] = patient;
        patientServer[patient.Id] = server;

        stageWaitMinutes[patient.StageIndex] += clock - patient.ArrivalTime; // wait = start - stage arrival
        _stageWaitSamples![patient.StageIndex].Add(clock - patient.ArrivalTime); // P2 waiting-time histogram

        // Start row: q is the stage queue after any dequeue (the caller already
        // dequeued the patient that starts here), i.e. how many are left behind.
        EmitTrace(TraceEventType.StartService, clock, patient.Id, stage.Name, server.Id, stage.Queue.Count, details: null);

        // Phase 8I: the stage's own sampler, not a shared exponential. Index by the
        // patient's stage rather than the local `stage` so this stays correct for the
        // routing path too.
        double serviceTime = _serviceSamplers![patient.StageIndex].NextSample();
        _generatedServiceSamples![patient.StageIndex].Add(serviceTime); // Phase 8A: retain the drawn service time per stage
        EmitRngDraw(clock, FormattableString.Invariant(
            $"service time {serviceTime:0.000} min at {stage.Name} s{server.Id} (end at t={clock + serviceTime:0.000})"));
        _log.Debug("    -> service start server={ServerId}, service-time draw={Draw:0.####} min, end t={Time:0.###}",
            server.Id, serviceTime, clock + serviceTime);

        fel.Enqueue(new Event(clock + serviceTime, EndEventTypeForStage(patient.StageIndex), patient.Id));
    }

    private void HandleServiceEnd(
        Event evt,
        double clock,
        Stage[] stages,
        NetworkTopology topology,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        double[] stageWaitMinutes,
        ref double totalSystemMinutes,
        ref int completed,
        CalendarGate? gate)
    {
        var patient = inService[evt.PatientId];
        var server = patientServer[evt.PatientId];
        var stage = stages[patient.StageIndex];

        // The per-stage drain is recorded here, not at the event switch above,
        // because this is the first point where the completing patient's stage is
        // known (D-193). Recording it in the switch would have meant guessing the
        // stage index from the event type and keeping that mapping in step with
        // HandleServiceEnd's own.
        gate?.NoteServiceEnd(clock, patient.StageIndex);

        patient.MarkServiceCompleted(clock);
        server.EndService(clock);
        inService.Remove(evt.PatientId);
        patientServer.Remove(evt.PatientId);

        // Last service end per stage: the backlog present when arrivals stopped is
        // drained once every patient present at that moment has completed, so the
        // stage's drain time is measured from the close of arrivals to here (D-191).
        var lastEnd = _stageLastServiceEnd!;
        if (clock > lastEnd[patient.StageIndex])
            lastEnd[patient.StageIndex] = clock;

        // Default flow: the next stage, and the last stage exits.
        int nextIndex = patient.StageIndex + 1;
        bool exits = nextIndex >= stages.Length;

        // Probabilistic exit after the exit stage (FR-SIM-3): draw U against p_exit.
        if (!exits && patient.StageIndex == topology.ExitStageIndex)
        {
            double u = _random.NextDouble();
            bool leaves = u < topology.ExitProbability;
            EmitRngDraw(clock, FormattableString.Invariant(
                $"routing draw U={u:0.####} vs p_exit={topology.ExitProbability:0.####} → {(leaves ? "exit" : "continue to " + stages[nextIndex].Name)}"));
            _log.Debug("    -> routing draw={Draw:0.####} p_exit={PExit:0.####}", u, topology.ExitProbability);
            exits = leaves;
        }

        // Bypass (D-179, reshaped by D-189): the same shape as the exit draw, but
        // instead of leaving the system the patient jumps over the stage in
        // between. S is the stage being SKIPPED, so the draw rides on the
        // completion of stage S − 1 — the completion on which the patient would
        // otherwise have been routed into S. S == 0 has no such completion and is
        // drawn at the arrival event instead (HandleArrival).
        //
        // Because S is the skipped stage, drawing at S (the old behaviour) would
        // mean the patient was served at S before jumping over it — the coin would
        // fire once per stage rather than once per patient.
        //
        // The constructor refuses BypassTriggerStageIndex == ExitStageIndex, so the
        // two draws can never both fire on one completion and the order between
        // them cannot matter.
        if (!exits && topology.BypassEnabled && patient.StageIndex == topology.BypassTriggerStageIndex)
        {
            double u = _random.NextDouble();
            bool bypasses = u < topology.BypassProbability;
            string outcome = bypasses
                ? "bypass to " + stages[topology.BypassDestinationIndex].Name
                : "continue to " + stages[nextIndex].Name;
            EmitRngDraw(clock, FormattableString.Invariant(
                $"bypass draw U={u:0.####} vs p_bypass={topology.BypassProbability:0.####} → {outcome}"));
            _log.Debug("    -> bypass draw={Draw:0.####} p_bypass={PBypass:0.####}", u, topology.BypassProbability);

            if (bypasses)
            {
                nextIndex = topology.BypassDestinationIndex;
                exits = nextIndex >= stages.Length; // defensive: the destination is validated inside the topology
            }
        }

        // End row is emitted AFTER both decisions (D-179). It used to be emitted
        // before the exit draw, which meant a patient who left at Screening was
        // recorded as "→ Doctor" and then immediately recorded as an exit — the
        // trace contradicted itself on the same event, and a reader replaying it
        // could not tell which line was true. q is still the queue the freed
        // server is about to draw from, because routing only ever touches the
        // destination's queue, never this one.
        EmitTrace(TraceEventType.EndService, clock, patient.Id, stage.Name, serverId: null,
            stage.Queue.Count, exits ? "→ exit" : $"→ {stages[nextIndex].Name}");

        if (exits)
        {
            totalSystemMinutes += clock - patient.SystemArrivalTime;
            completed++;
            // Exit row: the patient has left the system, so no stage queue applies.
            EmitTrace(TraceEventType.Exit, clock, patient.Id, stageName: null, serverId: null, queueLength: null, details: null);
            _log.Debug("    -> patient done at stage='{Stage}', served={Served}", stage.Name, completed);
        }
        else
        {
            RouteTo(patient, nextIndex, clock, stages, fel, inService, patientServer, stageWaitMinutes);
        }

        // The freed server immediately pulls the next patient (FIFO queue).
        if (!stage.Queue.IsEmpty)
        {
            var next = stage.Queue.Dequeue();
            _log.Debug("    -> dequeue patient={PatientId}, queueLen={QueueLen}", next.Id, stage.Queue.Count);
            StartService(next, server, stage, clock, fel, inService, patientServer, stageWaitMinutes);
        }
    }

    /// <summary>
    /// Routes a patient to an arbitrary stage index: the next stage normally, or
    /// the bypass destination when the patient skipped the stages in between.
    /// </summary>
    /// <remarks>
    /// Renamed from <c>RouteToNextStage</c> in D-179. The name was wrong once
    /// bypass existed: "next" implied index + 1, which is precisely what a
    /// bypassed patient does <i>not</i> do. The body already accepted any index,
    /// so this is a rename only — no behavioural change.
    /// </remarks>
    /// <param name="patient">The patient that has just finished service.</param>
    /// <param name="nextStageIndex">Destination stage index.</param>
    /// <param name="clock">Current simulation time in minutes.</param>
    /// <param name="stages">All stages in the network.</param>
    /// <param name="fel">Future event list.</param>
    /// <param name="inService">Patient id → server, for patients currently in service.</param>
    /// <param name="patientServer">Patient id → stage, for patients currently in service.</param>
    /// <param name="stageWaitMinutes">Accumulator of wait minutes per stage; credited to the destination.</param>
    private void RouteTo(
        Patient patient,
        int nextStageIndex,
        double clock,
        Stage[] stages,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        double[] stageWaitMinutes)
    {
        var nextStage = stages[nextStageIndex];
        patient.AdvanceToStage(nextStageIndex, clock);
        _log.Debug("    -> routed patient={PatientId} to stage='{Stage}'", patient.Id, nextStage.Name);

        int queueAfter;
        if (nextStage.HasIdleServer)
        {
            int drawsBefore = _random.DrawCount;
            var server = _serverSelection.SelectIdleServer(nextStage.Servers, _random);
            EmitRngDrawIfDrawn(clock, drawsBefore, $"select idle server at {nextStage.Name} → server {server.Id}");
            StartService(patient, server, nextStage, clock, fel, inService, patientServer, stageWaitMinutes);
            queueAfter = nextStage.Queue.Count;
        }
        else
        {
            nextStage.Queue.Enqueue(patient);
            queueAfter = nextStage.Queue.Count;
        }

        // Route row: q is the destination queue length after the patient was placed.
        EmitTrace(TraceEventType.Route, clock, patient.Id, nextStage.Name, serverId: null, queueAfter, details: null);
    }

    private static EventType EndEventTypeForStage(int stageIndex)
    {
        // Stage i completes via the (i+1)-th event type (ReceptionEnd=1, ...).
        return (EventType)(stageIndex + 1);
    }

    private static string ServerStateString(IEnumerable<Stage> stages)
        => string.Join("|", stages.Select(s => string.Join(",", s.Servers.Select(srv => srv.IsBusy ? "busy" : "idle"))));

    /// <summary>
    /// Appends one row to the per-run trace, if a sink is configured.
    /// </summary>
    private void EmitTrace(TraceEventType type, double time, int? patientId, string? stageName, int? serverId, int? queueLength, string? details)
        => _traceSink?.Write(new TraceEvent(time, type, patientId, stageName, serverId, queueLength, details));

    /// <summary>
    /// Appends an RNG row naming the uniform deviate just drawn and what the
    /// engine did with it. Called immediately after the draw, so
    /// <see cref="TraceRandomSource.LastDraw"/>/<see cref="TraceRandomSource.DrawCount"/>
    /// still describe exactly that draw.
    /// </summary>
    /// <param name="clock">Simulation time of the draw.</param>
    /// <param name="description">What the draw produced, e.g. <c>inter-arrival 0.369 min</c>.</param>
    private void EmitRngDraw(double clock, string description)
        => EmitTrace(TraceEventType.Rng, clock, patientId: null, stageName: null, serverId: null, queueLength: null,
            $"draw#{_random.DrawCount} U={_random.LastDraw.ToString("0.####", CultureInfo.InvariantCulture)} → {description}");

    /// <summary>
    /// Appends an RNG row only when a draw was actually consumed, since the
    /// server-selection policy draws just once when several servers are idle and
    /// not at all for the single-idle case (D-017).
    /// </summary>
    /// <param name="clock">Simulation time of the draw.</param>
    /// <param name="drawsBefore">The wrapper's draw counter before the selection call.</param>
    /// <param name="description">What the draw produced, e.g. <c>select idle server at Reception → server 1</c>.</param>
    private void EmitRngDrawIfDrawn(double clock, int drawsBefore, string description)
    {
        if (_traceSink is not null && _random.DrawCount > drawsBefore)
            EmitRngDraw(clock, description);
    }

    /// <summary>
    /// Per-run bookkeeping for a calendar-aware simulation (see
    /// <see cref="Run(NetworkTopology, ClinicCalendar, int, int, int?)"/>).
    /// </summary>
    /// <remarks>
    /// Day blocks are <see cref="ClinicCalendar.MinutesPerDay"/> long and start
    /// at each day's arrival window; the admission window is the first
    /// <see cref="ClinicCalendar.OpenDurationMinutes"/> minutes of an open day's
    /// block. The gate deliberately tracks no patient-level state — it only
    /// answers "may this arrival enter?" and "what is the operating-time
    /// denominator so far?", keeping the engine loop free of calendar logic.
    /// </remarks>
    private sealed class CalendarGate
    {
        private readonly ClinicCalendar _calendar;
        private readonly IReadOnlyList<ClinicSession> _sessions;

        private readonly int[] _admittedPerSession;
        private readonly int[] _screeningAdmittedPerSession;
        private readonly double?[] _sessionFirstArrival;
        private readonly double[] _sessionLastServiceEnd;

        /// <summary>
        /// Per-stage last service end per operating session (D-193). Separate from
        /// <see cref="_sessionLastServiceEnd"/>, which stays system-wide because the
        /// operating-time denominator is defined on the whole system's last service
        /// end — repurpose it and every recorded utilisation baseline moves.
        /// </summary>
        private readonly double[][] _sessionStageLastServiceEnd;

        /// <summary>
        /// Creates the gate for a run of <paramref name="sessionCount"/>
        /// operating sessions.
        /// </summary>
        /// <param name="calendar">The clinic schedule.</param>
        /// <param name="sessionCount">Number of operating sessions to generate arrivals for.
        /// Closed weekdays between them are stepped over and not counted (FR-SIM-12).</param>
        /// <param name="dailyCap">Maximum <b>Screening-bound</b> admissions per session;
        /// null = unlimited. A patient routed past Screening by the arrival-time bypass
        /// does not consume a place (D-190).</param>
        public CalendarGate(ClinicCalendar calendar, int sessionCount, int? dailyCap, int stageCount)
        {
            _calendar = calendar;
            _sessions = calendar.EnumerateSessions(sessionCount);
            SessionCount = _sessions.Count;
            DailyCap = dailyCap;

            // Arrivals stop at the arrival-window end of the LAST operating session,
            // which is not the last block: a 4-session run from Saturday spans five
            // blocks and ends in Wednesday's block (D-199). The run then drains
            // service events only (FR-SIM-6).
            StopTime = CloseTimeForSession(SessionCount - 1);

            _admittedPerSession = new int[SessionCount];
            _screeningAdmittedPerSession = new int[SessionCount];
            _sessionFirstArrival = new double?[SessionCount];
            _sessionLastServiceEnd = new double[SessionCount];
            _sessionStageLastServiceEnd = new double[SessionCount][];
            for (var s = 0; s < SessionCount; s++)
            {
                _sessionStageLastServiceEnd[s] = new double[stageCount];
            }

            CurrentSessionIndex = 0;
        }

        /// <summary>Number of operating sessions the run covers.</summary>
        public int SessionCount { get; }

        /// <summary>The resolved operating sessions, in order.</summary>
        public IReadOnlyList<ClinicSession> Sessions => _sessions;

        /// <summary>
        /// Maximum Screening-bound admissions per operating session, or null when
        /// unlimited.
        /// </summary>
        public int? DailyCap { get; }

        /// <summary>Clock time after which no further arrivals are scheduled.</summary>
        public double StopTime { get; }

        /// <summary>
        /// The operating session the clock is currently inside. Work that runs past
        /// midnight, or across a closed day, stays attributed to the session that
        /// produced it rather than to a session that never opened.
        /// </summary>
        public int CurrentSessionIndex { get; private set; }

        /// <summary>
        /// Every admitted arrival so far in the current operating session, including
        /// arrivals routed past Screening by the bypass.
        /// </summary>
        /// <remarks>
        /// This is the counter the admission metrics report, and it deliberately
        /// still counts <i>all</i> arrivals. The daily cap reads a different
        /// counter — <see cref="ScreeningAdmittedThisSession"/> — so that turning the cap
        /// on cannot silently change what "admitted" means in the results (D-190).
        /// </remarks>
        public int AdmittedThisSession { get; private set; }

        /// <summary>
        /// Admissions so far in the current operating session that are bound for
        /// Screening — the only arrivals the daily cap constrains (D-190).
        /// </summary>
        public int ScreeningAdmittedThisSession { get; private set; }

        /// <summary>
        /// The instant arrivals close for the given operating session: the end of
        /// that day's arrival window (D-191).
        /// </summary>
        /// <param name="sessionIndex">Zero-based session index — the position in
        /// <see cref="Sessions"/>, <i>not</i> a block index.</param>
        /// <returns>Clock time at which that session's arrivals stop.</returns>
        public double CloseTimeForSession(int sessionIndex)
            => _sessions[sessionIndex].BlockIndex * ClinicCalendar.MinutesPerDay + _calendar.OpenDurationMinutes;

        /// <summary>
        /// Minutes from a session's close of arrivals to that session's last service
        /// end — the time taken to clear the backlog left at the close (D-191).
        /// </summary>
        /// <param name="sessionIndex">Zero-based session index.</param>
        /// <returns>Drain time in minutes; 0 when the session had nothing to drain.</returns>
        public double DrainMinutesForSession(int sessionIndex)
            => Math.Max(0.0, _sessionLastServiceEnd[sessionIndex] - CloseTimeForSession(sessionIndex));

        /// <summary>Admitted arrivals per operating session over the whole run.</summary>
        public int[] AdmittedPerSession => _admittedPerSession;

        /// <summary>Screening-bound admissions per operating session over the whole run.</summary>
        public int[] ScreeningAdmittedPerSession => _screeningAdmittedPerSession;

        /// <summary>
        /// Switches the session bookkeeping to the last session whose block the
        /// clock has reached. The clock is monotonic, so the cursor only ever
        /// moves forward, and a closed block never advances it — that is what
        /// keeps a session's counters (and its drain) intact across the overnight
        /// and closed-day gap instead of attributing them to a session that
        /// never opened.
        /// </summary>
        /// <param name="clock">The current simulation clock.</param>
        public void AdvanceTo(double clock)
        {
            int absoluteBlock = (int)(clock / ClinicCalendar.MinutesPerDay);
            int previous = CurrentSessionIndex;
            while (CurrentSessionIndex + 1 < SessionCount
                   && _sessions[CurrentSessionIndex + 1].BlockIndex <= absoluteBlock)
            {
                CurrentSessionIndex++;
            }

            if (CurrentSessionIndex != previous)
            {
                AdmittedThisSession = 0; // the cap resets each operating session (D-009)
                ScreeningAdmittedThisSession = 0; // and so does the counter it actually constrains (D-190)
            }
        }

        /// <summary>
        /// Whether <paramref name="clock"/> falls inside an open-day arrival
        /// window.
        /// </summary>
        /// <remarks>
        /// Checked before any routing draw, because a patient who cannot be
        /// admitted at all never reaches a routing decision — and because the
        /// window applies to bypass arrivals too: nobody walks in when the clinic
        /// is shut, whether or not they are going to be screened (D-190).
        /// </remarks>
        /// <param name="clock">The arrival's simulation time.</param>
        /// <returns>True when the clinic is taking arrivals at that time.</returns>
        public bool IsInWindow(double clock) => _calendar.IsInArrivalWindow(clock);

        /// <summary>
        /// Attempts to admit an arrival at <paramref name="clock"/>.
        /// </summary>
        /// <param name="clock">The arrival's simulation time.</param>
        /// <param name="screeningBound">Whether the patient is routed into Screening.
        /// Only these arrivals consume a place under the daily cap: a patient who
        /// jumps straight to a later stage is admitted regardless of how full the
        /// Screening list is, because the cap models the screening session's
        /// admitted load, not the building's head count (D-190).</param>
        /// <returns>True if the arrival is inside an open-day window and, when it is
        /// Screening-bound, under the daily cap; false (gated out) otherwise.</returns>
        public bool TryAdmit(double clock, bool screeningBound = true)
        {
            if (!_calendar.IsInArrivalWindow(clock))
                return false;
            if (screeningBound && DailyCap is { } cap && ScreeningAdmittedThisSession >= cap)
                return false;

            AdmittedThisSession++;
            _admittedPerSession[CurrentSessionIndex]++;
            if (screeningBound)
            {
                ScreeningAdmittedThisSession++;
                _screeningAdmittedPerSession[CurrentSessionIndex]++;
            }
            _sessionFirstArrival[CurrentSessionIndex] ??= clock;
            return true;
        }

        /// <summary>
        /// Records a service-completion time for the current operating session,
        /// keeping that session's last service end monotonic.
        /// </summary>
        /// <param name="clock">The completion's simulation time.</param>
        public void NoteServiceEnd(double clock, int stageIndex)
        {
            var session = CurrentSessionIndex;
            _sessionLastServiceEnd[session] = Math.Max(_sessionLastServiceEnd[session], clock);
            var perStage = _sessionStageLastServiceEnd[session];
            perStage[stageIndex] = Math.Max(perStage[stageIndex], clock);
        }

        /// <summary>
        /// Drain for ONE stage in ONE session: that stage's last service end minus
        /// that session's close, clamped at zero (D-193). The system-wide
        /// <see cref="DrainMinutesForSession(int)"/> is the max across stages, which
        /// is the right figure for "how long until the clinic is empty" and the wrong
        /// one for "how long was this stage still working".
        /// </summary>
        public double DrainMinutesForSession(int sessionIndex, int stageIndex)
            => Math.Max(0.0, _sessionStageLastServiceEnd[sessionIndex][stageIndex] - CloseTimeForSession(sessionIndex));

        /// <summary>
        /// Operating-time denominator: summed per operating session (first admitted
        /// arrival to last service end), never diluted by overnight gaps or by
        /// closed days that no session ever opened (D-018).
        /// </summary>
        public double OperatingTimeMinutes
        {
            get
            {
                double sum = 0;
                for (int s = 0; s < SessionCount; s++)
                    if (_sessionFirstArrival[s] is { } firstArrival)
                        sum += _sessionLastServiceEnd[s] - firstArrival;
                return sum;
            }
        }
    }
}