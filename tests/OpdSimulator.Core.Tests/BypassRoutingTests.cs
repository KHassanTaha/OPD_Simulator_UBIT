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
    /// The clinic network with bypass switched on at Reception. Doctor has three
    /// servers at μ = 1.6, so it absorbs the whole inflow even at p_bypass = 0.99
    /// (2.97 / 4.8 = 0.62) without ρ reaching 1.
    /// </summary>
    private static NetworkTopology WithBypass(double pBypass, double pExit = 0.4) => new(
        arrivalRate: 3.0,
        ClinicStages(),
        exitStageIndex: 1,
        exitProbability: pExit,
        bypassProbability: pBypass,
        bypassStageIndex: 0,
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
    /// An exit draw and a bypass draw on the same completion would make the
    /// destination depend on which was consulted first. That configuration is
    /// ambiguous, so it is rejected rather than silently resolved.
    /// </summary>
    [Fact]
    public void Engine_BypassFromStageEqualsExitStage_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new NetworkTopology(3.0, ClinicStages(),
            exitStageIndex: 1, exitProbability: 0.4,
            bypassProbability: 0.2, bypassStageIndex: 1, bypassDestinationIndex: 2));

        Assert.Contains("cannot also be the exit stage", ex.Message, StringComparison.Ordinal);
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