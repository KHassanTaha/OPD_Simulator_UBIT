using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Phase 8T.1 — the horizon counts OPERATING SESSIONS, not calendar days
/// (FR-SIM-12, D-199).
/// </summary>
/// <remarks>
/// <para>
/// These tests exist because the old behaviour was not merely mislabelled — it
/// produced different numbers. From a Saturday start, asking for 4 days ran
/// Saturday, Monday and Tuesday with an empty Sunday in between, so the run
/// covered 3 real sessions and a closed block contributed a zero backlog and a
/// zero drain to every multi-day average.
/// </para>
/// <para>
/// The clock arithmetic is deliberately unchanged (CONTEXT §5.1): a day block is
/// still 1440 minutes and a weekday is still <c>(StartDayOfWeek + block) mod 7</c>.
/// Only the resolution of "how many days did the user ask for" changed.
/// </para>
/// </remarks>
public sealed class Phase8TTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    // λ0 = 0.5/min; Reception ρ = 0.33, Screening ρ = 0.25, Doctor ρ ≈ 0.146 — all
    // stable, so a multi-session run's differences are calendar arithmetic rather
    // than queue blow-up.
    private static NetworkTopology ClinicNetwork()
        => new NetworkTopology(0.5, new[]
        {
            new StageSpec("Reception", serverCount: 1, serviceRate: 1.5),
            new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
            new StageSpec("Doctor", serverCount: 3, serviceRate: 0.8),
        }, exitStageIndex: 1, exitProbability: 0.3);

    // ---- Session enumeration (the calendar's half) ----------------------

    [Fact]
    public void EnumerateSessions_FourFromSaturday_SkipsSunday()
    {
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var sessions = calendar.EnumerateSessions(4);

        Assert.Equal(
            new[]
            {
                (1, 0, DayOfWeek.Saturday), (2, 2, DayOfWeek.Monday),
                (3, 3, DayOfWeek.Tuesday), (4, 4, DayOfWeek.Wednesday),
            },
            sessions.Select(s => (s.Ordinal, s.BlockIndex, s.DayOfWeek)));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, 4, new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday })]
    [InlineData(DayOfWeek.Friday, 4, new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday })]
    [InlineData(DayOfWeek.Saturday, 3, new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Tuesday })]
    [InlineData(DayOfWeek.Sunday, 2, new[] { DayOfWeek.Monday, DayOfWeek.Tuesday })]
    public void EnumerateSessions_FromAnyStart_ReturnsOnlyOpenWeekdays(
        DayOfWeek start, int count, DayOfWeek[] expected)
    {
        var calendar = new ClinicCalendar(startDayOfWeek: start);

        var sessions = calendar.EnumerateSessions(count);

        Assert.Equal(expected, sessions.Select(s => s.DayOfWeek));
        Assert.All(sessions, s => Assert.True(calendar.IsOpenDay(s.DayOfWeek), $"{s.DayOfWeek} is a closed weekday"));
    }

    [Fact]
    public void EnumerateSessions_OrphanalBlock_StartsAtTheNextOpenDay()
    {
        // A custom schedule open only on Sunday: the run still has to produce a
        // session, and it must be the Sunday rather than nothing at all.
        var calendar = new ClinicCalendar(
            openDays: new[] { DayOfWeek.Sunday }, startDayOfWeek: DayOfWeek.Monday);

        var session = Assert.Single(calendar.EnumerateSessions(1));

        Assert.Equal((1, 6, DayOfWeek.Sunday), (session.Ordinal, session.BlockIndex, session.DayOfWeek));
    }

    [Fact]
    public void EnumerateSessions_HorizonBeyondAWeek_StepsOverEveryClosedDay()
    {
        // 12 sessions spans three full weeks: the block index is what proves the
        // closed days were stepped over rather than truncated.
        var calendar = new ClinicCalendar();

        var sessions = calendar.EnumerateSessions(12);

        // From Monday the 12th open weekday is block 15: blocks 4, 6, 11 and 13 are
        // the Fridays and Sundays stepped over on the way.
        Assert.Equal(12, sessions.Count);
        Assert.Equal(Enumerable.Range(1, 12), sessions.Select(s => s.Ordinal));
        Assert.Equal(15, sessions[^1].BlockIndex);
        Assert.Equal(16, sessions[^1].BlockIndex + 1);
        Assert.Equal(4, sessions[^1].BlockIndex + 1 - sessions.Count); // the closed blocks stepped over

        // The invariant that matters for a stepped-over horizon: every block between
        // two consecutive sessions is a CLOSED weekday. If one were open, that day
        // would have been a session and the request would have been satisfied earlier.
        for (int i = 1; i < sessions.Count; i++)
        {
            for (int block = sessions[i - 1].BlockIndex + 1; block < sessions[i].BlockIndex; block++)
            {
                Assert.False(calendar.IsOpenDay(calendar.DayOfWeekAt(block)),
                    $"block {block} ({calendar.DayOfWeekAt(block)}) is open, so it must have been a session");
            }
        }
    }

    [Fact]
    public void EnumerateSessions_ZeroOrNegative_Throws()
    {
        var calendar = new ClinicCalendar();

        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.EnumerateSessions(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.EnumerateSessions(-3));
    }

    // ---- The engine half -------------------------------------------------

    [Fact]
    public void Run_FourSessionsFromSaturday_StopsAtTheEndOfWednesdayAndReportsFourSessions()
    {
        // The gate condition from the Phase 8T plan, stated as assertions.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 4, seed: 42);

        Assert.Equal(
            new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
            result.Sessions.Select(s => s.DayOfWeek));
        Assert.Equal(4, result.GeneratorDays);

        // No closed day has a row: this is the assertion that fails against the
        // old block-counting arithmetic, which produced three sessions and a
        // fourth row for the Sunday.
        Assert.Equal(4, result.AdmittedPerSession.Count);
        Assert.Equal(4, result.ScreeningAdmittedPerSession.Count);
        Assert.Equal(4, result.BacklogPerSession.Count);
        Assert.Equal(4, result.DrainPerSession.Count);
        Assert.All(result.StageMetrics, m =>
        {
            Assert.Equal(4, m.BacklogAtCloseBySession.Count);
            Assert.Equal(4, m.DrainMinutesBySession.Count);
        });
        Assert.All(result.AdmittedPerSession, a => Assert.True(a > 0, "every operating session admits patients"));
    }

    [Fact]
    public void Run_FourSessionsFromSaturday_EndsArrivalsInsideWednesdaysWindow()
    {
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 4, seed: 42);

        // Wednesday is block 4, so arrivals must stop at 4·1440 + 165 and NOT
        // before: the old formula ended Tuesday's block at 3·1440 + 165.
        double expectedStop = 4 * ClinicCalendar.MinutesPerDay + calendar.OpenDurationMinutes;

        // Observable consequence of that formula: arrivals are drawn until the close
        // of the last open block, so the cumulative stream time lands just short of
        // it — and strictly past the close of the block before, which is exactly
        // where the old horizon formula stopped and where D-192's bug lived.
        double generated = result.GeneratedInterArrivalSamples.Sum();
        double previousBlockClose = 3 * ClinicCalendar.MinutesPerDay + calendar.OpenDurationMinutes;

        Assert.InRange(generated, expectedStop - 5, expectedStop);
        Assert.True(
            generated > previousBlockClose,
            $"the arrival stream stopped at {generated:F1}, at or before the Tuesday close " +
            $"({previousBlockClose}) — that is the old horizon formula, not the session one");

        // The decisive form: the run reached Wednesday at all, so the clock spans
        // five blocks and the operating time covers four sessions' work.
        Assert.Equal(DayOfWeek.Wednesday, result.Sessions[^1].DayOfWeek);
        Assert.Equal(4, result.Sessions[^1].BlockIndex);
    }

    [Fact]
    public void Run_FourSessionsFromFriday_SkipsBothLeadingAndTrailingClosedDays()
    {
        // Friday is closed, so session 1 is Saturday — block 1, not block 0. A
        // leading closed block must not produce an empty first session.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Friday);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 4, seed: 42);

        Assert.Equal(
            new[] { DayOfWeek.Saturday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday },
            result.Sessions.Select(s => s.DayOfWeek));
        Assert.Equal(new[] { 1, 3, 4, 5 }, result.Sessions.Select(s => s.BlockIndex));
        Assert.Equal(4, result.AdmittedPerSession.Count);
        Assert.All(result.AdmittedPerSession, a => Assert.True(a > 0, "every operating session admits patients"));
    }

    [Fact]
    public void Run_CapResetsOnEveryOperatingSession_NotOnEveryBlock()
    {
        // The cap is the observable that distinguishes a per-session reset from a
        // per-block one: a closed block in the middle would reset the counter under
        // the old code and let one more patient in, so 7 sessions × cap must come
        // out as exactly 7 × cap and every entry must be exactly the cap.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 7, seed: 42, dailyCap: 4);

        Assert.Equal(new[] { 4, 4, 4, 4, 4, 4, 4 }, result.AdmittedPerSession);
        Assert.Equal(28, result.TotalPatientsServed);
    }

    [Fact]
    public void Run_SessionsCarryWeekdays_ForLabelsWithoutASecondLookup()
    {
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 3, seed: 42);

        // The label form the per-session totals table will use (FR-UI-38).
        Assert.Equal(
            new[] { "Day 1 (Sat)", "Day 2 (Mon)", "Day 3 (Tue)" },
            result.Sessions.Select(s => $"Day {s.Ordinal} ({Abbreviate(s.DayOfWeek)})"));
    }

    [Fact]
    public void Run_HorizonMode_HasNoSessionsAndKeepsSingleSessionSeries()
    {
        // The diagnostic trace is a minutes horizon, not a calendar run: it must
        // report zero sessions and keep its one-entry series, or the new session
        // indexing would silently corrupt it.
        var result = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), horizonMinutes: 60, seed: 42);

        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.GeneratorDays);
        Assert.Empty(result.AdmittedPerSession);
        Assert.Empty(result.BacklogPerSession);
        Assert.All(result.StageMetrics, m => Assert.Single(m.BacklogAtCloseBySession));
        Assert.All(result.StageMetrics, m => Assert.Single(m.DrainMinutesBySession));
    }

    [Fact]
    public void Run_SameSeedAndSessions_ProduceIdenticalPerSessionFigures()
    {
        // Reproducibility (FR-VAL-3) must survive the reindexing: the same seed and
        // the same session list give the same per-session series.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var first = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 5, seed: 42);
        var second = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 5, seed: 42);

        Assert.Equal(first.Sessions, second.Sessions);
        Assert.Equal(first.AdmittedPerSession, second.AdmittedPerSession);
        Assert.Equal(first.BacklogPerSession, second.BacklogPerSession);
        Assert.Equal(first.DrainPerSession, second.DrainPerSession);
        Assert.Equal(first.TotalPatientsServed, second.TotalPatientsServed);
        Assert.Equal(first.OperatingTimeMinutes, second.OperatingTimeMinutes);
    }

    [Fact]
    public void Run_ExtendedHorizon_ChangesEveryResultBecauseTheRunIsLonger()
    {
        // The point of the fix, stated as a difference rather than a shape: asking
        // for one more session must reach a day the old run stopped before. Under
        // block counting, Days = 4 from Saturday ended on Tuesday and Days = 5 on
        // Wednesday; under session counting both claims shift by the Sunday.
        var calendar = new ClinicCalendar(startDayOfWeek: DayOfWeek.Saturday);

        var four = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 4, seed: 42);
        var five = new Engine(new SeededRandomSource(), Log)
            .Run(ClinicNetwork(), calendar, generatorDays: 5, seed: 42);

        Assert.Equal(DayOfWeek.Wednesday, four.Sessions[^1].DayOfWeek);
        Assert.Equal(DayOfWeek.Thursday, five.Sessions[^1].DayOfWeek);
        Assert.True(five.TotalPatientsServed > four.TotalPatientsServed,
            "a fifth session must admit more patients than four sessions did");
    }

    private static string Abbreviate(DayOfWeek day) => day.ToString()[..3];
}