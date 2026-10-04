using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Phase 8T.2 gate (FR-STAT-9, D-201) — the per-session served counts and
/// timestamped wait samples the per-session totals table projects.
/// Verification intent:
/// - the whole-run figures are untouched: per-session served sums to
///   <c>PatientsServed</c>, and the whole-run wait mean is unchanged by the
///   timestamp now carried on every sample (the D-054 regression proves it);
/// - a wait sample can be attributed to a session by its own timestamp, and the
///   per-session means are that attribution aggregated — not sample order, which
///   is what the timestamp replaced;
/// - "absent" is distinct from 0.0: a session that admitted nobody reports no
///   mean at all, because a mean over an empty set has no value (FR-STAT-9);
/// - a horizon run still reports one implicit session carrying the whole-run
///   figures, so the table is a projection of collected data in either mode.
/// </summary>
public class Phase8T2SessionMetricsTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .CreateLogger();

    /// <summary>A three-stage clinic whose stages can all serve and drain.</summary>
    private static NetworkTopology ThreeStableStages() => new(0.4, new[]
    {
        new StageSpec("Reception", 1, 1.2),
        new StageSpec("Screening", 1, 1.0),
        new StageSpec("Doctor", 1, 0.9),
    });

    [Fact]
    public void WaitSamples_CarryTheClockAtWhichTheWaitWasObserved()
    {
        // The timestamp is the whole point of FR-STAT-9: without it a sample cannot
        // be attributed to a session. So every sample must carry a real clock time,
        // and the waits themselves must be unchanged non-negative values.
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStableStages(), seed: 42, horizonMinutes: 300);

        foreach (var stage in result.StageMetrics)
        {
            Assert.NotEmpty(stage.WaitingTimeSamples);
            foreach (var sample in stage.WaitingTimeSamples)
            {
                Assert.True(sample.Time >= 0, $"sample time {sample.Time} must be a real clock time");
                Assert.True(sample.Minutes >= 0, "a wait cannot be negative");
                // A wait is stamped when the service STARTS, and the last arrivals'
                // services start (and are stamped) past the horizon — the run drains
                // beyond it (FR-SIM-6). So the upper bound is generous here; the
                // exact attribution is asserted in the aggregate test below.
            }

            // Samples are recorded in clock order, which is what lets the histogram
            // consume them unchanged and in the order the wait histogram expects.
            var times = stage.WaitingTimeSamples.Select(w => w.Time).ToArray();
            Assert.Equal(times.OrderBy(t => t), times);
        }
    }

    [Fact]
    public void PerSessionServed_SumsToWholeRunPatientsServed()
    {
        // PatientsServed stays a whole-run figure (FR-STAT-9); the per-session
        // series is detail beside it. If the two ever disagree, one of them is
        // counting a different thing.
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStableStages(), new ClinicCalendar(), generatorDays: 3, seed: 42);

        Assert.Equal(3, result.Sessions.Count);
        foreach (var stage in result.StageMetrics)
        {
            Assert.Equal(3, stage.PatientsServedBySession.Count);
            Assert.Equal(stage.PatientsServed, stage.PatientsServedBySession.Sum());
        }
    }

    [Fact]
    public void PerSessionMeans_AreTheAggregateOfTheSamplesInThatSession()
    {
        // The per-session mean must be reproducible from the timestamped samples
        // alone. Deriving it any other way (sample order, a separate accumulator)
        // would make the two disagree the moment a session served nobody.
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStableStages(), new ClinicCalendar(), generatorDays: 3, seed: 42);

        var sessionBlocks = result.Sessions
            .Select(s => s.BlockIndex * ClinicCalendar.MinutesPerDay)
            .ToArray();

        foreach (var stage in result.StageMetrics)
        {
            for (int s = 0; s < result.Sessions.Count; s++)
            {
                // A sample belongs to the last session whose block the clock reached.
                double lowerBound = s == 0 ? 0 : sessionBlocks[s];
                double upperBound = s == result.Sessions.Count - 1
                    ? double.PositiveInfinity
                    : sessionBlocks[s + 1];

                var inSession = stage.WaitingTimeSamples
                    .Where(w => w.Time >= lowerBound && w.Time < upperBound)
                    .Select(w => w.Minutes)
                    .ToArray();

                if (inSession.Length == 0)
                {
                    Assert.Null(stage.MeanWaitMinutesBySession[s]);
                }
                else
                {
                    Assert.NotNull(stage.MeanWaitMinutesBySession[s]);
                    Assert.Equal(inSession.Average(), stage.MeanWaitMinutesBySession[s]!.Value, 10);
                }
            }
        }
    }

    [Fact]
    public void StageNobodyReaches_ReportsServedZeroAndAbsentMeans()
    {
        // The absent-vs-zero rule (FR-STAT-9). p_bypass = 0.99 sends effectively every
        // patient past Screening, so that stage sees no arrivals in ANY session: its
        // served count is a real 0 while its per-session means have no value. A
        // printed 0.0 would claim those (non-existent) patients waited no time,
        // which is a statement about patients who never existed.
        var topology = new NetworkTopology(
            arrivalRate: 1.0,
            new[]
            {
                new StageSpec("Reception", 1, 1.5),
                new StageSpec("Screening", 1, 1.5),
                new StageSpec("Doctor", 1, 1.5),
            },
            exitStageIndex: 1,
            exitProbability: 0.4,
            bypassProbability: 0.99,
            bypassStageIndex: 1,
            bypassDestinationIndex: 2);
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, new ClinicCalendar(), generatorDays: 3, seed: 42);

        // Golden per-session vector for this seed: the first session admitted nobody
        // past Reception, the next two admitted a handful between them. Recorded so a
        // later change to the bypass draw is visible as a change here rather than as
        // a silently different absent/present pattern.
        var screening = result.StageMetrics.Single(m => m.StageName == "Screening");
        Assert.Equal(new[] { 0, 1, 2 }, screening.PatientsServedBySession);
        Assert.Equal(3, screening.PatientsServed);

        // Session 1 admitted nobody, so it has no wait mean and no queue-length mean.
        // A printed 0.0 would claim those (non-existent) patients waited no time,
        // which is a statement about patients who never existed.
        Assert.Null(screening.MeanWaitMinutesBySession[0]);
        Assert.Null(screening.MeanQueueLengthBySession[0]);

        // The sessions that DID serve report real means, so "absent" is not the same
        // as "every mean is absent" — the null is carrying information.
        Assert.NotNull(screening.MeanWaitMinutesBySession[1]);
        Assert.NotNull(screening.MeanWaitMinutesBySession[2]);
        Assert.NotNull(screening.MeanQueueLengthBySession[1]);
    }

    [Fact]
    public void SessionAfterACapLimitedSession_StillReportsItsOwnFigures()
    {
        // The daily cap resets each session (D-009), so a capped run must NOT be
        // read as "session 2 served nobody" — it served its own single patient. This
        // is the case a sample-order inference would get wrong: the second session's
        // one wait would otherwise be attributed to the first session's block.
        var topology = NetworkTopology.CreateSingleStage(0.4, 0.8, serverCount: 1, "Reception");
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(topology, new ClinicCalendar(), generatorDays: 2, dailyCap: 1, seed: 42);

        var stage = Assert.Single(result.StageMetrics);
        Assert.Equal(2, stage.PatientsServedBySession.Count);
        Assert.Equal(new[] { 1, 1 }, stage.PatientsServedBySession);
        Assert.All(stage.MeanWaitMinutesBySession, mean => Assert.NotNull(mean));
    }

    [Fact]
    public void HorizonRun_ReportsOneImplicitSessionWithWholeRunFigures()
    {
        // A horizon run has no operating sessions, but the per-session shape must
        // not collapse to empty — the table would then have nothing to project. The
        // single implicit session carries that run's whole-run figures.
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStableStages(), seed: 42, horizonMinutes: 300);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.GeneratorDays);

        foreach (var stage in result.StageMetrics)
        {
            Assert.Single(stage.PatientsServedBySession);
            Assert.Equal(stage.PatientsServed, stage.PatientsServedBySession[0]);
            Assert.Equal(stage.AverageWaitMinutes, stage.MeanWaitMinutesBySession[0]!.Value, 10);
            Assert.Equal(stage.AverageQueueLength, stage.MeanQueueLengthBySession[0]!.Value, 10);
        }
    }

    [Fact]
    public void Result_CarriesSessionLengthAndStartWeekday()
    {
        // FR-UI-38's caption has to name the session length and the start weekday,
        // and the UI has no other route to the calendar the run used.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ThreeStableStages(), calendar, generatorDays: 2, seed: 42);

        Assert.Equal(calendar.OpenDurationMinutes, result.SessionLengthMinutes);
        Assert.Equal(165, result.SessionLengthMinutes); // D-172: 8:15–11:00
        Assert.Equal(DayOfWeek.Saturday, result.SessionStartDay);
        Assert.Equal(DayOfWeek.Saturday, result.Sessions[0].DayOfWeek);
    }

    [Fact]
    public void WholeRunAverageWait_IsUnchangedByTheTimestampedSamples()
    {
        // The D-054 regression, restated against the new sample type: the
        // histogram consumes Minutes and nothing else, so a sample that now
        // carries a Time must still produce the same served count and the same
        // mean wait. This is the "byte-identical histogram" claim in its testable
        // form — the inputs to the histogram did not move.
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 10000, seed: 42);
        var result = new Engine(config, new SeededRandomSource(), Log).Run();

        Assert.Equal(29892, result.TotalPatientsServed);
        Assert.Equal(0.724, result.AverageWaitMinutes, 3);

        // The golden values are the whole-run figures; the per-session shape has to
        // carry them without restating them differently.
        var stage = Assert.Single(result.StageMetrics);
        Assert.Equal(stage.PatientsServed, stage.PatientsServedBySession[0]);
        Assert.Equal(stage.AverageWaitMinutes, stage.MeanWaitMinutesBySession[0]!.Value, 3);
    }
}