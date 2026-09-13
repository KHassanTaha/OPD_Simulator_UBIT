namespace OpdSimulator.Core.Engine;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Events;
using OpdSimulator.Core.Patients;
using OpdSimulator.Core.Servers;
using OpdSimulator.Core.Stages;
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
/// so Milestone-1 output stays byte-for-byte reproducible.
/// </para>
/// </remarks>
public sealed class Engine
{
    private readonly EngineConfig? _config;
    private readonly IRandomSource _random;
    private readonly ExponentialSampler _interarrivalSampler;
    private readonly ExponentialSampler _serviceSampler;
    private readonly IServerSelectionPolicy _serverSelection;
    private readonly ILogger _log;

    /// <summary>
    /// Creates an engine for a network run (no single-stage <see cref="EngineConfig"/>).
    /// </summary>
    /// <param name="random">The random source (must support seeding for reproducibility).</param>
    /// <param name="log">Serilog logger for the event trace.</param>
    /// <param name="serverSelection">Idle-server selection policy; defaults to
    /// <see cref="RandomIdleSelection"/> (D-017).</param>
    public Engine(IRandomSource random, ILogger log, IServerSelectionPolicy? serverSelection = null)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _serverSelection = serverSelection ?? new RandomIdleSelection();

        _interarrivalSampler = new ExponentialSampler(random);
        _serviceSampler = new ExponentialSampler(random);
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
    /// regression (seed 42: served = 29892, wait = 0.724) is a byte-for-byte
    /// guard on this path.
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

        return Run(topology, _config.Seed, _config.HorizonMinutes);
    }

    /// <summary>
    /// Runs a serial network topology to completion and returns the aggregate
    /// plus per-stage metrics.
    /// </summary>
    /// <param name="topology">The ordered stage configuration to simulate.</param>
    /// <param name="seed">Random seed for reproducibility (FR-VAL-3, default 42).</param>
    /// <param name="horizonMinutes">Length of the arrival-generation window in minutes (arrivals stop beyond it).</param>
    /// <returns>The collected statistics of the run.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="topology"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the horizon is not strictly positive.</exception>
    /// <exception cref="UnstableSystemException">If any stage has ρ ≥ 1 (FR-VAL-1).</exception>
    public SimulationResult Run(NetworkTopology topology, int seed = SeededRandomSource.DefaultSeed, double horizonMinutes = 10000)
    {
        if (topology is null)
            throw new ArgumentNullException(nameof(topology));
        if (horizonMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes), horizonMinutes, "Horizon must be strictly positive.");

        topology.Validate(); // throws UnstableSystemException listing every unstable stage (FR-VAL-1)

        _random.SetSeed(seed);
        _log.Information("Simulation start: stages={Stages} λ0={ArrivalRate} seed={Seed} horizon={Horizon} ρs=[{Rhos}]",
            string.Join(" → ", topology.StageSpecs.Select(s => $"{s.Name}(c={s.ServerCount}, μ={s.ServiceRate:F3})")),
            topology.ArrivalRate, seed, horizonMinutes,
            string.Join(",", Enumerable.Range(0, topology.StageSpecs.Count).Select(i => topology.RhoFor(i).ToString("0.##"))));

        // Fresh per-run runtime state: queues and servers never leak between runs.
        var stages = Enumerable.Range(0, topology.StageSpecs.Count)
            .Select(i => topology.CreateStage(i))
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

        // First arrival at t = 0 (CONTEXT §4.3).
        fel.Enqueue(new Event(0, EventType.Arrival, nextPatientId));

        while (fel.Count > 0)
        {
            Event evt = fel.Dequeue();
            clock = evt.Time;

            // Time-weighted queue length: each stage's queue sampled throughout
            // the interval [lastMetricTime, clock] held its previous length.
            for (int i = 0; i < stages.Length; i++)
            {
                areaUnderQueue[i] += stages[i].Queue.Count * (clock - lastMetricTime[i]);
                lastMetricTime[i] = clock;
            }

            _log.Debug("t={Clock:0.###} {Type} patient={PatientId} queueLen={QueueLen} stageQueues=[{StageQueueLens}] servers=[{ServerStates}]",
                clock, evt.Type, evt.PatientId, stages[0].Queue.Count,
                string.Join(",", stages.Select(s => s.Queue.Count)), ServerStateString(stages));

            switch (evt.Type)
            {
                case EventType.Arrival:
                    HandleArrival(evt, clock, stages[0], topology.ArrivalRate, horizonMinutes, fel,
                        inService, patientServer, ref nextPatientId, stageWaitMinutes);
                    break;

                case EventType.ReceptionEnd:
                case EventType.ScreeningEnd:
                case EventType.DoctorEnd:
                    HandleServiceEnd(evt, clock, stages, topology, fel, inService, patientServer,
                        stageWaitMinutes, ref totalSystemMinutes, ref completed);
                    break;

                default:
                    // The five event types above are the only ones the engine can
                    // schedule; anything else indicates an internal bug.
                    throw new InvalidOperationException($"Event type {evt.Type} is not supported by the engine.");
            }
        }

        // Operating time = time from first arrival (t=0) to last service end
        // (D-018). The FEL is empty, so `clock` is exactly that (FR-SIM-6).
        double operatingTime = clock;

        var stageMetrics = stages.Select((stage, i) =>
        {
            var perServerUtil = stage.Servers
                .Select(s => s.Utilisation(operatingTime)) // asserts 0 ≤ util ≤ 1 (FR-VAL-2)
                .ToArray();
            int servedHere = stage.Servers.Sum(s => s.PatientsServed);

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
            };
        }).ToArray();

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
        };
    }

    private void HandleArrival(
        Event evt,
        double clock,
        Stage stage,
        double arrivalRate,
        double horizonMinutes,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        ref int nextPatientId,
        double[] stageWaitMinutes)
    {
        var patient = new Patient(evt.PatientId, clock, stageIndex: 0);

        if (stage.HasIdleServer)
        {
            var server = _serverSelection.SelectIdleServer(stage.Servers, _random);
            StartService(patient, server, stage, clock, fel, inService, patientServer, stageWaitMinutes);
        }
        else
        {
            stage.Queue.Enqueue(patient);
            _log.Debug("    -> queued patient={PatientId}, queueLen={QueueLen}", patient.Id, stage.Queue.Count);
        }

        // Schedule the next arrival only inside the arrival window (FR-SIM-5).
        // The inter-arrival draw is logged (FR-VAL-4).
        double interArrival = _interarrivalSampler.Sample(arrivalRate);
        double nextArrivalTime = clock + interArrival;
        _log.Debug("    -> inter-arrival draw={Draw:0.####} min, next arrival t={Time:0.###}",
            interArrival, nextArrivalTime);

        if (nextArrivalTime < horizonMinutes)
        {
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

        double serviceTime = _serviceSampler.Sample(stage.ServiceRate);
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
        ref int completed)
    {
        var patient = inService[evt.PatientId];
        var server = patientServer[evt.PatientId];
        var stage = stages[patient.StageIndex];

        patient.MarkServiceCompleted(clock);
        server.EndService(clock);
        inService.Remove(evt.PatientId);
        patientServer.Remove(evt.PatientId);

        // Does the finished patient continue to the next stage or leave?
        int nextIndex = patient.StageIndex + 1;
        bool exits = nextIndex >= stages.Length;
        if (!exits && patient.StageIndex == topology.ExitStageIndex)
        {
            // Probabilistic exit after the exit stage (FR-SIM-3): draw U against p_exit.
            double u = _random.NextDouble();
            _log.Debug("    -> routing draw={Draw:0.####} p_exit={PExit:0.####}", u, topology.ExitProbability);
            exits = u < topology.ExitProbability;
        }

        if (exits)
        {
            totalSystemMinutes += clock - patient.SystemArrivalTime;
            completed++;
            _log.Debug("    -> patient done at stage='{Stage}', served={Served}", stage.Name, completed);
        }
        else
        {
            RouteToNextStage(patient, nextIndex, clock, stages, fel, inService, patientServer, stageWaitMinutes);
        }

        // The freed server immediately pulls the next patient (FIFO queue).
        if (!stage.Queue.IsEmpty)
        {
            var next = stage.Queue.Dequeue();
            _log.Debug("    -> dequeue patient={PatientId}, queueLen={QueueLen}", next.Id, stage.Queue.Count);
            StartService(next, server, stage, clock, fel, inService, patientServer, stageWaitMinutes);
        }
    }

    private void RouteToNextStage(
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

        if (nextStage.HasIdleServer)
        {
            var server = _serverSelection.SelectIdleServer(nextStage.Servers, _random);
            StartService(patient, server, nextStage, clock, fel, inService, patientServer, stageWaitMinutes);
        }
        else
        {
            nextStage.Queue.Enqueue(patient);
        }
    }

    private static EventType EndEventTypeForStage(int stageIndex)
    {
        // Stage i completes via the (i+1)-th event type (ReceptionEnd=1, ...).
        return (EventType)(stageIndex + 1);
    }

    private static string ServerStateString(IEnumerable<Stage> stages)
        => string.Join("|", stages.Select(s => string.Join(",", s.Servers.Select(srv => srv.IsBusy ? "busy" : "idle"))));
}