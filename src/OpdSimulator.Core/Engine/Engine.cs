namespace OpdSimulator.Core.Engine;

using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Events;
using OpdSimulator.Core.Patients;
using OpdSimulator.Core.Queues;
using OpdSimulator.Core.Servers;
using Serilog;

/// <summary>
/// Discrete-event simulation engine for a single-stage M/M/c queue.
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
/// The engine routes a generic single stage (index 0). Its completion event is
/// <see cref="EventType.ReceptionEnd"/>; the <c>StageIndex → EventType</c>
/// mapping keeps the design N-stage generic, so adding Screening/Doctor in
/// Milestone 3 is configuration, not a rewrite (FR-SIM-1, D-006).
/// </para>
/// </remarks>
public sealed class Engine
{
    private readonly EngineConfig _config;
    private readonly IRandomSource _random;
    private readonly ExponentialSampler _interarrivalSampler;
    private readonly ExponentialSampler _serviceSampler;
    private readonly ILogger _log;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="config">The run configuration (λ, μ, c, horizon, seed).</param>
    /// <param name="random">The random source (must support seeding for reproducibility).</param>
    /// <param name="log">Serilog logger for the event trace.</param>
    public Engine(EngineConfig config, IRandomSource random, ILogger log)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _interarrivalSampler = new ExponentialSampler(random);
        _serviceSampler = new ExponentialSampler(random);
    }

    /// <summary>
    /// Runs the discrete-event simulation to completion and returns the aggregate metrics.
    /// </summary>
    /// <returns>The collected statistics of the run.</returns>
    public SimulationResult Run()
    {
        _config.Validate(); // throws UnstableSystemException when ρ ≥ 1 (FR-VAL-1)

        _random.SetSeed(_config.Seed);
        _log.Information("Simulation start: stage={Stage} λ={Lambda} μ={Mu} c={Servers} horizon={Horizon} seed={Seed} ρ={Rho}",
            _config.StageName, _config.ArrivalRate, _config.ServiceRate, _config.ServerCount,
            _config.HorizonMinutes, _config.Seed, _config.Rho);

        var servers = Enumerable.Range(0, _config.ServerCount)
            .Select(i => new Server(i))
            .ToArray();
        var queue = new Queue();
        var fel = new FEL();

        var inService = new Dictionary<int, Patient>();          // patientId -> patient being served
        var patientServer = new Dictionary<int, Server>();       // patientId -> server serving them

        double clock = 0;
        double areaUnderQueue = 0;   // time-integrated queue length (§4 statistics)
        double lastMetricTime = 0;
        double totalWaitMinutes = 0;
        double totalSystemMinutes = 0;
        int completed = 0;
        int nextPatientId = 1;

        // First arrival at t = 0 (CONTEXT §4.3).
        fel.Enqueue(new Event(0, EventType.Arrival, nextPatientId));

        while (fel.Count > 0)
        {
            Event evt = fel.Dequeue();
            clock = evt.Time;

            // Time-weighted queue length: the queue sampled throughout the
            // interval [lastMetricTime, clock] held its previous length.
            areaUnderQueue += queue.Count * (clock - lastMetricTime);
            lastMetricTime = clock;

            _log.Debug("t={Clock:0.###} {Type} patient={PatientId} queueLen={QueueLen} servers=[{ServerStates}]",
                clock, evt.Type, evt.PatientId, queue.Count, ServerStateString(servers));

            switch (evt.Type)
            {
                case EventType.Arrival:
                    HandleArrival(evt, clock, servers, queue, fel, inService, patientServer, ref nextPatientId, ref totalWaitMinutes);
                    break;

                case EventType.ReceptionEnd:
                    HandleServiceEnd(evt, clock, queue, fel, inService, patientServer,
                        ref totalSystemMinutes, ref completed, ref totalWaitMinutes);
                    break;

                default:
                    // M1 simulates a single stage; only Arrival and the stage-0
                    // completion event (ReceptionEnd) may appear on the FEL.
                    throw new InvalidOperationException(
                        $"Event type {evt.Type} is not supported in the single-stage engine.");
            }
        }

        // Operating time = time from first arrival (t=0) to last service end
        // (D-017). The FEL is empty, so `clock` is exactly that (FR-SIM-6).
        double operatingTime = clock;

        double[] perServerUtilisation = servers
            .Select(s => s.Utilisation(operatingTime)) // asserts 0 ≤ util ≤ 1 (FR-VAL-2)
            .ToArray();

        return new SimulationResult
        {
            TotalPatientsServed = completed,
            AverageWaitMinutes = completed > 0 ? (double)totalWaitMinutes / completed : 0,
            AverageSystemTimeMinutes = completed > 0 ? (double)totalSystemMinutes / completed : 0,
            AverageQueueLength = operatingTime > 0 ? areaUnderQueue / operatingTime : 0,
            StageUtilisation = perServerUtilisation.Length > 0 ? perServerUtilisation.Average() : 0,
            PerServerUtilisation = perServerUtilisation,
            ThroughputPerMinute = operatingTime > 0 ? completed / operatingTime : 0,
            OperatingTimeMinutes = operatingTime,
        };
    }

    private void HandleArrival(
        Event evt,
        double clock,
        Server[] servers,
        Queue queue,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        ref int nextPatientId,
        ref double totalWaitMinutes)
    {
        var patient = new Patient(evt.PatientId, clock, stageIndex: 0);

        var idleServer = servers.FirstOrDefault(s => !s.IsBusy);
        if (idleServer is not null)
        {
            StartService(patient, idleServer, clock, fel, inService, patientServer, ref totalWaitMinutes);
        }
        else
        {
            queue.Enqueue(patient);
            _log.Debug("    -> queued patient={PatientId}, queueLen={QueueLen}", patient.Id, queue.Count);
        }

        // Schedule the next arrival only inside the arrival window (FR-SIM-5).
        // The inter-arrival draw is logged (FR-VAL-4).
        double interArrival = _interarrivalSampler.Sample(_config.ArrivalRate);
        double nextArrivalTime = clock + interArrival;
        _log.Debug("    -> inter-arrival draw={Draw:0.####} min, next arrival t={Time:0.###}",
            interArrival, nextArrivalTime);

        if (nextArrivalTime < _config.HorizonMinutes)
        {
            nextPatientId++;
            fel.Enqueue(new Event(nextArrivalTime, EventType.Arrival, nextPatientId));
        }
    }

    private void StartService(
        Patient patient,
        Server server,
        double clock,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        ref double totalWaitMinutes)
    {
        patient.MarkServiceStarted(clock);
        server.StartService(clock);
        inService[patient.Id] = patient;
        patientServer[patient.Id] = server;

        totalWaitMinutes += clock - patient.ArrivalTime; // wait = start - arrival

        double serviceTime = _serviceSampler.Sample(_config.ServiceRate);
        _log.Debug("    -> service start server={ServerId}, service-time draw={Draw:0.####} min, end t={Time:0.###}",
            server.Id, serviceTime, clock + serviceTime);

        fel.Enqueue(new Event(clock + serviceTime, EndEventTypeForStage(patient.StageIndex), patient.Id));
    }

    private void HandleServiceEnd(
        Event evt,
        double clock,
        Queue queue,
        FEL fel,
        Dictionary<int, Patient> inService,
        Dictionary<int, Server> patientServer,
        ref double totalSystemMinutes,
        ref int completed,
        ref double totalWaitMinutes)
    {
        var patient = inService[evt.PatientId];
        var server = patientServer[evt.PatientId];

        patient.MarkServiceCompleted(clock);
        server.EndService(clock);
        inService.Remove(evt.PatientId);
        patientServer.Remove(evt.PatientId);

        totalSystemMinutes += clock - patient.ArrivalTime;
        completed++;

        _log.Debug("    -> service end server={ServerId}, patient done, served={Served}",
            server.Id, completed);

        // The freed server immediately pulls the next patient (FIFO queue).
        if (!queue.IsEmpty)
        {
            var next = queue.Dequeue();
            _log.Debug("    -> dequeue patient={PatientId}, queueLen={QueueLen}", next.Id, queue.Count);
            StartService(next, server, clock, fel, inService, patientServer, ref totalWaitMinutes);
        }
    }

    private static EventType EndEventTypeForStage(int stageIndex)
    {
        // Stage i completes via the (i+1)-th event type (ReceptionEnd=1, ...).
        return (EventType)(stageIndex + 1);
    }

    private static string ServerStateString(IEnumerable<Server> servers)
        => string.Join(",", servers.Select(s => s.IsBusy ? "busy" : "idle"));
}