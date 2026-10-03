using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Core.Trace;
using Serilog;
using Serilog.Core;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Tests for direct-to-Doctor routing — "bypass", where a fraction of patients
/// completing Reception jump straight past Screening (D-179).
/// </summary>
/// <remarks>
/// <para>
/// The clinic capture shows this is not a hypothetical: roughly one patient in
/// six leaves Reception and goes to a doctor without a screening record. Before
/// D-179 the engine had exactly two behaviours — every patient was screened, or
/// the row was rejected as dirty — so that sixth of patients could not be
/// simulated at all.
/// </para>
/// <para>
/// These tests pin three separate things, which is why they are not folded into
/// <see cref="EngineTests"/>: the <b>constructor contract</b> (which
/// configurations are rejected, and how a zero probability is normalised away),
/// the <b>routing behaviour</b> (does the patient land at the right stage, and do
/// the stage counts add up), and the <b>trace fidelity</b> (does the recorded
/// destination match what the engine did).
/// </para>
/// </remarks>
public class BypassRoutingTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    private sealed class CollectingSink : ITraceSink
    {
        public List<TraceEvent> Events { get; } = new();
        public void Write(TraceEvent evt) => Events.Add(evt);
        public void Flush() { }
    }

    private static StageSpec[] ClinicStages() =>
    [
        new StageSpec("Reception", serverCount: 1, serviceRate: 10.0),
        new StageSpec("Screening", serverCount: 2, serviceRate: 4.0),
        new StageSpec("Doctor", serverCount: 3, serviceRate: 1.6),
    ];

    /// <summary>
    /// The clinic network with bypass switched on. Doctor has three
    /// servers at μ = 1.6, so it absorbs the whole inflow even at p_bypass = 0.99
    /// (2.97 / 4.8 = 0.62) without ρ reaching 1.
    /// </summary>
    /// <remarks>
    /// S = 1 is Screening — the stage being <i>skipped</i> — so the draw rides on
    /// Reception's completion, which is the completion on which the patient would
    /// otherwise have been routed into Screening (D-189). That is the same node
    /// D-179 drew on when it called Reception itself the bypass stage, so the
    /// flows, the RNG stream and the served counts are unchanged; only the label
    /// moved.
    /// </remarks>
    private static NetworkTopology WithBypass(double pBypass, double pExit = 0.4) => new(
        arrivalRate: 3.0,
        ClinicStages(),
        exitStageIndex: 1,
        exitProbability: pExit,
        bypassProbability: pBypass,
        bypassStageIndex: 1,
        bypassDestinationIndex: 2);

    // --------------------------------------------------------------- topology

    /// <summary>
    /// p_bypass = 0 switches the mechanism off completely and discards the
    /// indices, mirroring what the exit stage already does for p_exit = 0. A
    /// caller reading <see cref="NetworkTopology.BypassStageIndex"/> back must not
    /// be told a bypass stage is configured when the probability is zero.
    /// </summary>
    [Fact]
    public void NetworkTopology_BypassProbabilityZero_NormalisesIndicesAway()
    {
        var topology = WithBypass(0.0);

        Assert.Equal(0.0, topology.BypassProbability);
        Assert.Equal(-1, topology.BypassStageIndex);
        Assert.Equal(-1, topology.BypassDestinationIndex);
        Assert.False(topology.BypassEnabled);
    }

    /// <summary>
    /// The destination must come strictly after the bypass stage and inside the
    /// stage list. Both bounds are load-bearing: a destination at or before the
    /// bypass stage would be a cycle rather than a skip, and one past the end
    /// indexes outside the array.
    /// </summary>
    [Theory]
    [InlineData(0)]    // equal to the bypass stage — not a skip
    [InlineData(-1)]   // the "disabled" sentinel while the probability is positive
    [InlineData(3)]    // past the end of a 3-stage list
    public void NetworkTopology_BypassDestinationOutOfRange_Throws(int destination)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new NetworkTopology(3.0, ClinicStages(), exitStageIndex: 1, exitProbability: 0.4,
                bypassProbability: 0.2, bypassStageIndex: 0, bypassDestinationIndex: destination));

        Assert.Equal("bypassDestinationIndex", ex.ParamName);
    }

    /// <summary>
    /// p_bypass uses the same [0, 1) domain as p_exit, and 1.0 is excluded for
    /// the same reason: a probability of exactly 1 is a configuration mistake,
    /// not a routing rule.
    /// </summary>
    [Fact]
    public void Engine_BypassProbabilityOne_ThrowsArgumentOutOfRange()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => WithBypass(1.0));

        Assert.Equal("bypassProbability", ex.ParamName);
    }

    /// <summary>
    /// An exit draw and a bypass draw on the same service completion would make the
    /// destination depend on which was consulted first. That configuration is
    /// ambiguous, so it is rejected rather than silently resolved.
    /// </summary>
    /// <remarks>
    /// The collision test is <c>S − 1 == ExitStageIndex</c>, not
    /// <c>S == ExitStageIndex</c>, and getting that wrong is what the 8R rulings
    /// fixed (D-189). A four-stage list is needed to reach a real collision:
    /// with three stages, S = 2 has no destination after it, so the ambiguity
    /// cannot be constructed at all.
    /// </remarks>
    [Fact]
    public void Engine_BypassEnteredFromTheExitStage_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new NetworkTopology(3.0,
            [
                new StageSpec("Reception", 1, 10.0),
                new StageSpec("Triage", 1, 8.0),
                new StageSpec("Screening", 2, 4.0),
                new StageSpec("Doctor", 3, 1.6),
            ],
            exitStageIndex: 1, exitProbability: 0.4,
            bypassProbability: 0.2, bypassStageIndex: 2, bypassDestinationIndex: 3));

        Assert.Contains("is entered on the completion of stage 1", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The three required configurations must all construct: the 3-stage OPD with
    /// S = 1 / D = 2, the 2-stage clinic file with S = 0 / D = 1 (which the old
    /// rule rejected outright), and the S = 0 arrival-time case alongside a
    /// probabilistic exit on the same index (D-189).
    /// </summary>
    /// <remarks>
    /// Each of these was rejected by D-179's <c>S == ExitStageIndex</c> rule for the
    /// wrong reason. If this test fails, the collision predicate has regressed.
    /// </remarks>
    [Theory]
    [InlineData(3, 1, 1, 2)]  // Reception → Screening → Doctor: skip Screening
    [InlineData(2, 0, 0, 1)]  // Screening → Doctor: skip Screening, drawn at arrival
    public void Engine_BypassConfigurationMatchingTheExitStage_Constructs(int stageCount, int bypassStage, int exitStage, int destination)
    {
        var stages = stageCount == 3
            ? ClinicStages()
            : [new StageSpec("Screening", 2, 4.0), new StageSpec("Doctor", 3, 1.6)];

        var topology = new NetworkTopology(3.0, stages,
            exitStageIndex: exitStage, exitProbability: 0.4,
            bypassProbability: 0.2, bypassStageIndex: bypassStage, bypassDestinationIndex: destination);

        Assert.True(topology.BypassEnabled);
        Assert.Equal(bypassStage, topology.BypassStageIndex);
        Assert.Equal(destination, topology.BypassDestinationIndex);
    }

    /// <summary>
    /// S == 0 must be recorded as an arrival-time draw, with no service completion
    /// able to carry it, and S &gt; 0 as a draw on the completion of S − 1 (D-189).
    /// </summary>
    /// <remarks>
    /// No probabilistic exit is configured here on purpose: with an exit on stage 0
    /// the S = 1 and S = 2 rows would construct the genuine collision the
    /// constructor refuses, which
    /// <see cref="Engine_BypassEnteredFromTheExitStage_Throws"/> already covers. This
    /// test is about where the draw rides, not about what is rejected.
    /// </remarks>
    [Theory]
    [InlineData(0, -1, true)]   // the front door: no completion can carry the draw
    [InlineData(1, 0, false)]   // skips stage 1 on stage 0's completion
    [InlineData(2, 1, false)]
    public void Engine_BypassDrawTiming_FollowsTheSkippedStage(int bypassStage, int expectedTrigger, bool atArrival)
    {
        var topology = new NetworkTopology(3.0,
            [
                new StageSpec("A", 1, 10.0),
                new StageSpec("B", 1, 8.0),
                new StageSpec("C", 1, 6.0),
                new StageSpec("D", 1, 4.0),
            ],
            exitStageIndex: -1, exitProbability: 0.0,
            bypassProbability: 0.2, bypassStageIndex: bypassStage, bypassDestinationIndex: 3);

        Assert.Equal(expectedTrigger, topology.BypassTriggerStageIndex);
        Assert.Equal(atArrival, topology.BypassAtArrival);
    }

    // ------------------------------------------------------- effective arrival rate

    /// <summary>
    /// λᵢ must be the sum over every route that reaches stage i, with the bypass
    /// share applied at the trigger node (S − 1) and the exit share applied to the
    /// remainder that carries on into the exit stage (D-189).
    /// </summary>
    /// <remarks>
    /// Hand-computed for the 3-stage OPD at λ₀ = 3.0, p_bypass = 0.2, p_exit = 0.4:
    /// Reception serves everyone (λ = 3.0); the bypass share leaves Reception
    /// straight for the Doctor; Screening gets the remaining 0.8 and passes
    /// 0.8 × 0.6 = 0.48 of it on. So Doctor sees a mass of 0.2 + 0.48 = 0.68 and
    /// λ = 2.04 — not 3.0 × 0.6 = 1.8, which is what the single-exit product of
    /// D-007 would give. The 0.2 difference is the bypass inflow arriving at a
    /// stage that never passed through Screening, and losing it is exactly the
    /// regression this assertion exists to catch.
    /// </remarks>
    [Fact]
    public void EffectiveArrivalRate_SumsBypassInflowAtTheTriggerNode()
    {
        var topology = WithBypass(0.2, pExit: 0.4);

        Assert.Equal(3.0, topology.EffectiveArrivalRate(0), 9);            // Reception serves everyone
        Assert.Equal(2.4, topology.EffectiveArrivalRate(1), 9);            // 3.0 × (1 − 0.2)
        Assert.Equal(3.0 * (0.2 + 0.8 * 0.6), topology.EffectiveArrivalRate(2), 9);
        Assert.NotEqual(3.0 * 0.6, topology.EffectiveArrivalRate(2), 6);   // not the D-007 product
    }

    /// <summary>
    /// The 2-stage clinic topology: the skipped stage is the first one, so the
    /// split happens at the arrival event and λ_screening is reduced by the
    /// bypass share (D-189). This is the configuration the engine could not run
    /// at all before 8R.
    /// </summary>
    [Fact]
    public void EffectiveArrivalRate_SkipsTheFirstStageAtArrival()
    {
        var topology = new NetworkTopology(
            arrivalRate: 0.794,
            [new StageSpec("Screening", 2, 0.280), new StageSpec("Doctor", 3, 0.1055)],
            exitStageIndex: 0, exitProbability: 0.956522,
            bypassProbability: 0.155963, bypassStageIndex: 0, bypassDestinationIndex: 1);

        double lambdaScreening = topology.EffectiveArrivalRate(0);
        double lambdaDoctor = topology.EffectiveArrivalRate(1);

        // λ₀(1 − p_bypass) = 0.794 × 0.844
        Assert.Equal(0.794 * (1.0 - 0.155963), lambdaScreening, 6);
        // λ₀·p_bypass + λ₀(1 − p_bypass)(1 − p_exit) — the bypass stream plus the
        // 4.3% of screened patients who continue.
        Assert.Equal(0.794 * 0.155963 + lambdaScreening * (1.0 - 0.956522), lambdaDoctor, 6);
        Assert.Equal(0.153, lambdaDoctor, 3);
    }

    /// <summary>
    /// A patient can only skip a stage if there is somewhere further along to land.
    /// One stage admits no bypass at all, whatever indices the caller passes.
    /// </summary>
    [Fact]
    public void NetworkTopology_SingleStageWithBypass_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new NetworkTopology(
            arrivalRate: 3.0,
            [new StageSpec("Reception", 1, 10.0)],
            exitStageIndex: -1, exitProbability: 0.0,
            bypassProbability: 0.2, bypassStageIndex: 0, bypassDestinationIndex: 1));

        Assert.Contains("at least two stages", ex.Message, StringComparison.Ordinal);
    }

    // ----------------------------------------------------------------- routing

    /// <summary>
    /// At p_bypass = 0.99 essentially nobody is screened, so Doctor's inflow is
    /// dominated by the bypass stream and Screening sees almost no work. This is
    /// the case the clinic capture motivates and the old engine could not express.
    /// </summary>
    [Fact]
    public void Engine_BypassProbabilityNearOne_MostPatientsSkipScreening()
    {
        var result = new Engine(new SeededRandomSource(), NullLogger)
            .Run(WithBypass(0.99), seed: 42, horizonMinutes: 2000);

        int reception = result.StageMetrics[0].PatientsServed;
        int screening = result.StageMetrics[1].PatientsServed;
        int doctor = result.StageMetrics[2].PatientsServed;

        // The routing-derived inflow says Screening sees ≈ 3% of the stream, so a
        // handful of patients out of thousands — not a meaningful fraction. (These
        // counts are service completions, so a patient who is screened and then
        // continues to the Doctor is counted at both stages: the two numbers are
        // not additive, only bounded.)
        Assert.True(screening < reception * 0.10,
            $"Screening should see under 10% of Reception's load, saw {screening}/{reception}");
        Assert.True(doctor > reception * 0.85,
            $"Doctor should absorb the bypass stream, saw {doctor}/{reception}");
        Assert.True(doctor <= reception,
            "Doctor cannot serve more patients than were released by Reception");
    }

    /// <summary>
    /// p_bypass = 0 must produce exactly the run a topology that never knew about
    /// bypass produces: same stage counts, same patients, no RNG draw consumed by
    /// the disabled mechanism.
    /// </summary>
    [Fact]
    public void Engine_BypassProbabilityZero_AllPatientsScreened()
    {
        var passingTheField = new Engine(new SeededRandomSource(), NullLogger)
            .Run(WithBypass(0.0), seed: 42, horizonMinutes: 1000);
        var noFieldAtAll = new Engine(new SeededRandomSource(), NullLogger)
            .Run(new NetworkTopology(3.0, ClinicStages(), exitStageIndex: 1, exitProbability: 0.4),
                seed: 42, horizonMinutes: 1000);

        Assert.Equal(passingTheField.StageMetrics[1].PatientsServed, noFieldAtAll.StageMetrics[1].PatientsServed);
        Assert.Equal(passingTheField.StageMetrics[2].PatientsServed, noFieldAtAll.StageMetrics[2].PatientsServed);
        Assert.Equal(passingTheField.TotalPatientsServed, noFieldAtAll.TotalPatientsServed);

        // Everyone who is not a Screening exit reaches the Doctor stage, so with bypass
        // off Reception and Screening must have served exactly the same patients.
        Assert.Equal(passingTheField.StageMetrics[0].PatientsServed, passingTheField.StageMetrics[1].PatientsServed);

        // Every patient leaves the system at one of the two exits, so the completed
        // total equals Reception's load; the Doctor stage accounts for only the
        // screening continuations, the rest left at Screening.
        Assert.Equal(passingTheField.TotalPatientsServed, passingTheField.StageMetrics[0].PatientsServed);
        Assert.True(passingTheField.StageMetrics[2].PatientsServed < passingTheField.StageMetrics[1].PatientsServed,
            "the exit fraction must keep some patients out of the Doctor stage");
        Assert.InRange(
            (double)passingTheField.StageMetrics[2].PatientsServed / passingTheField.StageMetrics[1].PatientsServed,
            0.45, 0.75); // ≈ 1 − p_exit = 0.6, wide band for sampling noise
    }

    // ------------------------------------------------------------------- trace

    /// <summary>
    /// The EndService row must name the stage the patient actually went to. This
    /// test fails if the row is emitted before the routing decisions are taken,
    /// because then a patient who bypasses Screening at Reception would be
    /// recorded as "Reception → Screening" and the very next row would show them
    /// at Doctor.
    /// </summary>
    [Fact]
    public void Trace_EndServiceRow_MatchesActualDestination_NotDefaultNext()
    {
        var sink = new CollectingSink();
        new Engine(new SeededRandomSource(), NullLogger)
            .Run(WithBypass(0.99), 42, horizonMinutes: 500, sink);

        var rows = sink.Events
            .Select(e => TraceFormatter.Format(e, TraceLevel.Detailed))
            .Where(line => line is not null)
            .Select(line => line!)
            .ToArray();

        Assert.Contains(rows, line => line.Contains("→ Doctor", StringComparison.Ordinal));

        // Every Reception completion that bypassed says so on its own END_SVC row.
        var receptionEndRows = rows
            .Where(line => line.Contains("END_SVC", StringComparison.Ordinal)
                        && line.Contains("Reception", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(receptionEndRows);
        Assert.All(receptionEndRows, line =>
            Assert.True(
                line.Contains("→ Doctor", StringComparison.Ordinal)
                || line.Contains("→ Screening", StringComparison.Ordinal),
                $"a Reception END_SVC row must name its real destination, got: {line}"));
    }
}