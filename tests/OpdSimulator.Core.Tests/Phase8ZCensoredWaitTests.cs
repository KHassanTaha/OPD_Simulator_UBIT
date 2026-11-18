using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Patients;
using OpdSimulator.Core.Queues;
using OpdSimulator.Core.Stages;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Phase 8Z — waits still in progress when a run is stopped early are recorded
/// as a separate, censored list (D-224).
/// </summary>
/// <remarks>
/// The intent is not "add another sample to the histogram". A censored patient
/// has not finished service, so folding their wait into
/// <see cref="StageMetrics.WaitingTimeSamples"/> would break the
/// <c>PatientsServed == WaitingTimeSamples.Count</c> invariant and corrupt every
/// average wait. These tests lock down both the new guarantee and that boundary.
/// </remarks>
public class Phase8ZCensoredWaitTests
{
    private const int Seed = 42;

    private static readonly ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new SilentSink())
        .CreateLogger();

    [Fact]
    public void Engine_EarlyStop_RecordsStillQueuedWaiters()
    {
        // ρ close to 1 (stable, so validation passes) with a small cap: the cap
        // stops the FEL while arrivals have piled up, which is the only shape
        // that can produce censored waits. λ must stay below μ for Validate().
        var topology = NetworkTopology.CreateSingleStage(0.95, 1.0, 1, "Reception");
        var result = new Engine(new SeededRandomSource(), NullLogger)
            .Run(topology, Seed, horizonMinutes: 10_000, traceSink: null, maxCompletedPatients: 3);

        var stage = Assert.Single(result.StageMetrics);
        // The cap counts system EXITS; a patient whose service already started
        // is counted here even though they had not exited at the stop.
        Assert.True(stage.PatientsServed >= 3, "the cap stops after at least three starts");
        Assert.NotEmpty(stage.UnfinishedWaitSamples);
        Assert.All(stage.UnfinishedWaitSamples, sample =>
        {
            Assert.True(sample.Minutes > 0, "a still-queued patient has waited a positive time");
            Assert.True(sample.Time <= 10_000, "the wait is censored at the run end");
        });
        // Completed waits are recorded at service START, PatientsServed at service
        // END, so the two diverge only by patients in service at the stop. The
        // censored list is separate; it never contaminates the completed waits.
        Assert.True(stage.WaitingTimeSamples.Count >= stage.PatientsServed,
            "every ended service has a wait sample; in-service patients add extra samples");
    }

    [Fact]
    public void Engine_DrainedRun_HasNoUnfinishedWaits()
    {
        var topology = NetworkTopology.CreateSingleStage(0.2, 1.0, 1, "Reception");
        var result = new Engine(new SeededRandomSource(), NullLogger)
            .Run(topology, Seed, horizonMinutes: 1_000, traceSink: null);

        var stage = Assert.Single(result.StageMetrics);
        Assert.True(stage.PatientsServed > 0, "the run must actually serve somebody");
        Assert.Empty(stage.UnfinishedWaitSamples);
        Assert.Equal(stage.PatientsServed, stage.WaitingTimeSamples.Count);
    }

    [Fact]
    public void Queue_SnapshotIsACopy()
    {
        var queue = new Queue();
        var first = new Patient(1, 0, 0);
        var second = new Patient(2, 1, 0);
        queue.Enqueue(first);
        queue.Enqueue(second);

        var snapshot = queue.Snapshot();

        Assert.Equal(new[] { first, second }, snapshot);
        snapshot[0] = first; // overwrite with the same reference, then clear
        Array.Clear(snapshot);
        Assert.Equal(2, queue.Count);
        Assert.Same(first, queue.Peek());
    }
}

/// <summary>A no-op sink so these tests never write to the real log files.</summary>
internal sealed class SilentSink : ILogEventSink
{
    public void Emit(LogEvent logEvent) { }
}
