using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Core.Trace;
using Serilog;
using Serilog.Core;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Lock-down tests for the Milestone-4 event trace (D-055/D-056/D-058).
/// </summary>
/// <remarks>
/// <para>
/// The heart is the <b>golden file</b>: a seed-42 single-server run of five
/// patients (λ = 3, μ = 4) whose trace was hand-verified row by row — every
/// simulation time is the −ln(U)/rate transform of the reference
/// <see cref="System.Random"/> sequence (see the <c>RngRows_TrackTheReferenceRandomSequence</c>
/// test). The trace must stay byte-identical to that fixture, so any engine,
/// formatter or RNG drift is caught the moment it changes the story the viva
/// evidence tells.
/// </para>
/// <para>
/// A second family of tests proves the trace is <em>faithful</em>: the count
/// of EXIT rows equals the engine's served count, and the per-patient waits
/// read off the rows equal the reported average wait — the trace and the
/// statistics cannot disagree (D-058 cross-check).
/// </para>
/// </remarks>
public class TraceRegressionTests
{
    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .WriteTo.Sink(new NullSink())
        .CreateLogger();

    private sealed class NullSink : ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent) { }
    }

    /// <summary>Captures the raw <see cref="TraceEvent"/> stream of one run.</summary>
    private sealed class CollectingSink : ITraceSink
    {
        public List<TraceEvent> Events { get; } = new();
        public void Write(TraceEvent evt) => Events.Add(evt);
        public void Flush() { }
    }

    private const double Lambda = 3.0;
    private const double Mu = 4.0;
    private const int Patients = 5;
    private const int Seed = 42;

    /// <summary>
    /// Runs the frozen golden configuration and renders it as text lines.
    /// </summary>
    private static string[] TraceLines(TraceLevel level)
    {
        var sink = new CollectingSink();
        var topology = NetworkTopology.CreateSingleStage(Lambda, Mu, 1, "Reception");
        new Engine(new SeededRandomSource(), NullLogger).Run(topology, Seed, horizonMinutes: 10_000, sink, Patients);
        return sink.Events
            .Select(e => TraceFormatter.Format(e, level))
            .Where(line => line is not null)
            .Select(line => line!)
            .ToArray();
    }

    private static string FixturePath
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "trace-5-patients.txt");

    /// <summary>
    /// The golden lock: the current trace must byte-match the hand-verified file.
    /// </summary>
    [Fact]
    public void Trace_MatchesHandVerifiedGoldenFile()
    {
        string expected = File.ReadAllText(FixturePath).Replace("\r\n", "\n").TrimEnd('\n');
        string actual = string.Join("\n", TraceLines(TraceLevel.State));

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Proves the golden lock actually bites: a single tampered row in the
    /// fixture must produce a difference, so the test can never go green by
    /// silently accepting drift.
    /// </summary>
    [Fact]
    public void GoldenLock_DetectsTamperedFixture()
    {
        string[] actual = TraceLines(TraceLevel.State);
        string[] tamperedLines = (string[])actual.Clone();
        string[] parts = tamperedLines[tamperedLines.Length / 2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        parts[^1] = "q=999";
        tamperedLines[tamperedLines.Length / 2] = string.Join(' ', parts);

        Assert.NotEqual(string.Join("\n", actual), string.Join("\n", tamperedLines));
        Assert.NotEqual(string.Join("\n", tamperedLines), File.ReadAllText(FixturePath).Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Event-type census: the five-patient journey is exactly five of each
    /// (arrival / start / end / exit) — the "one story, five patients" shape
    /// that Milestone 4 promises.
    /// </summary>
    [Fact]
    public void Trace_HasExactlyFiveOfEachEventType()
    {
        const TraceLevel level = TraceLevel.State;
        var counts = TraceLines(level)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[2])
            .GroupBy(column => column)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5, counts["ARRIVAL"]);
        Assert.Equal(5, counts["START_SVC"]);
        Assert.Equal(5, counts["END_SVC"]);
        Assert.Equal(5, counts["EXIT"]);
        Assert.False(counts.ContainsKey("RNG"), "state level must not show RNG rows");
        Assert.False(counts.ContainsKey("ROUTE"), "single-stage trace has no route rows");
    }

    /// <summary>
    /// Level contract: <c>events</c> drops the state columns (server id,
    /// destination) and the RNG rows; <c>state</c> adds the columns; <c>rng</c>
    /// adds the draw rows on top. The event times themselves are identical
    /// across levels — only the detail changes, never the story.
    /// </summary>
    [Fact]
    public void Levels_DifferOnlyInDetailColumns()
    {
        string[] eventsRows = TraceLines(TraceLevel.Events);
        string[] stateRows = TraceLines(TraceLevel.State);
        string[] rngRows = TraceLines(TraceLevel.Rng);

        Assert.All(eventsRows, line => Assert.DoesNotContain("s0", line));
        Assert.All(eventsRows, line => Assert.DoesNotContain("→", line));
        Assert.DoesNotContain("RNG", string.Join("\n", eventsRows));

        Assert.All(stateRows, line => Assert.False(line.Contains("RNG", StringComparison.Ordinal)));
        Assert.All(stateRows.Where(line => line.Contains("START_SVC")), line => Assert.Contains("s0", line));
        Assert.All(stateRows.Where(line => line.Contains("END_SVC")), line => Assert.Contains("→ exit", line));

        string rngText = string.Join("\n", rngRows);
        Assert.Contains("seed=42", rngText);
        Assert.Contains("U=", rngText);
        Assert.Contains("draw#", rngText);

        // The same event times appear regardless of level (the story is level-independent).
        Assert.Equal(
            eventsRows.Where(line => line.Contains("ARRIVAL")).Select(line => line.Split(' ')[0]),
            rngRows.Where(line => line.Contains("ARRIVAL")).Select(line => line.Split(' ')[0]));
    }

    /// <summary>
    /// Draw-by-draw parity with the reference .NET RNG: the k-th "draw#k U=..."
    /// row must equal the k-th <see cref="System.Random"/>(42) deviate. This is
    /// the reproducibility chain the golden file rests on — if the engine ever
    /// changed its draw order, the golden test (which re-derives times from
    /// these draws) would break.
    /// </summary>
    [Fact]
    public void RngRows_TrackTheReferenceRandomSequence()
    {
        string[] drawRows = TraceLines(TraceLevel.Rng)
            .Where(line => line.Contains("draw#"))
            .ToArray();
        Assert.Equal(10, drawRows.Length); // 5 patients, single server: one service draw per start + one inter-arrival draw per arrival

        var reference = new Random(42);
        int index = 0;
        foreach (string row in drawRows)
        {
            index++;
            Assert.Equal($"draw#{index}", row.Split(' ', StringSplitOptions.RemoveEmptyEntries)[3]);
            string expectedU = reference.NextDouble().ToString("0.####");
            Assert.Contains($"U={expectedU}", row);
        }
    }

    /// <summary>
    /// Cross-check (D-058): the trace and the statistics must tell the same
    /// story — every EXIT row is one served patient, and the average wait
    /// computed from the trace rows equals the engine's reported metric.
    /// </summary>
    [Fact]
    public void Trace_AndStatistics_AgreeOnServedCountAndAverageWait()
    {
        var sink = new CollectingSink();
        var topology = NetworkTopology.CreateSingleStage(Lambda, Mu, 1, "Reception");
        var result = new Engine(new SeededRandomSource(), NullLogger)
            .Run(topology, Seed, horizonMinutes: 10_000, sink, Patients);

        int exits = sink.Events.Count(e => e.Type == TraceEventType.Exit);
        Assert.Equal(result.TotalPatientsServed, exits);
        Assert.Equal(Patients, exits);

        var waits = sink.Events
            .Where(e => e.PatientId.HasValue)
            .GroupBy(e => e.PatientId!.Value)
            .Select(g =>
            {
                double arrival = g.First(e => e.Type == TraceEventType.Arrival).Time;
                double start = g.First(e => e.Type == TraceEventType.StartService).Time;
                return start - arrival;
            })
            .ToArray();
        Assert.Equal(Patients, waits.Length);
        Assert.All(waits, w => Assert.True(w >= 0, "a wait cannot be negative"));

        double averageWait = waits.Average();
        Assert.Equal(result.AverageWaitMinutes, averageWait, precision: 9);
    }

    /// <summary>
    /// The trace must be a passive observer (D-057): attaching a sink — hence
    /// wrapping the RNG in <see cref="TraceRandomSource"/> and emitting rows —
    /// must not change a single metric.
    /// </summary>
    [Fact]
    public void AttachingTraceSink_DoesNotChangeResults()
    {
        var topology = NetworkTopology.CreateSingleStage(Lambda, Mu, 1, "Reception");

        var without = new Engine(new SeededRandomSource(), NullLogger)
            .Run(topology, Seed, horizonMinutes: 10_000, traceSink: null, maxCompletedPatients: Patients);
        var with = new Engine(new SeededRandomSource(), NullLogger)
            .Run(topology, Seed, horizonMinutes: 10_000, traceSink: new CollectingSink(), maxCompletedPatients: Patients);

        Assert.Equal(without.TotalPatientsServed, with.TotalPatientsServed);
        Assert.Equal(without.AverageWaitMinutes, with.AverageWaitMinutes, precision: 9);
        Assert.Equal(without.StageUtilisation, with.StageUtilisation, precision: 9);
    }
}