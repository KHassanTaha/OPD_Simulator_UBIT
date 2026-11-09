namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;

/// <summary>
/// One decimated point of a stage's queue-length line (Phase 6c.5).
/// </summary>
/// <param name="Time">Simulation clock minutes at the sample.</param>
/// <param name="Length">Number of patients waiting in that stage's queue.</param>
public sealed record QueueChartPoint(double Time, int Length);

/// <summary>One stage's queue-length line data.</summary>
/// <param name="StageName">Stage name, used for the chart legend entry.</param>
/// <param name="Points">Decimated points, first and last always kept (D-122).</param>
/// <param name="StageName">The stage the line describes.</param>
/// <param name="Points">Sampled (time, queue length) points in time order.</param>
public sealed record QueueStageSeries(string StageName, IReadOnlyList<QueueChartPoint> Points)
{
    /// <summary>
    /// Position of the stage in the run's stage list. The builder keys the
    /// palette off this (not off the name) so the same stage is the same colour
    /// in every chart even after a stage is renamed (Phase 8M, D-163).
    /// </summary>
    public int StageIndex { get; init; }
}

/// <summary>
/// Pure, engine-independent description of the queue-length-over-time chart
/// (FR-UI-4 P2, Phase 6c.5). One line per stage coloured from the shared chart
/// palette; the series come from <see cref="StageMetrics.QueueLengthSeries"/>,
/// downsampled so a long run never renders more than
/// <see cref="QueueLengthChartService.MaxPoints"/> points.
/// </summary>
/// <param name="HasSeries">True when the result contributed at least one sampled point.</param>
/// <param name="Series">Per-stage line data in stage order.</param>
public sealed record QueueLengthChartData(
    bool HasSeries,
    IReadOnlyList<QueueStageSeries> Series)
{
    /// <summary>An empty card shown before the first run.</summary>
    public static QueueLengthChartData Empty { get; } =
        new(false, Array.Empty<QueueStageSeries>());

    /// <summary>
    /// When one operating session is selected, the length of that session in
    /// minutes; the X axis is then pinned to <c>0 … XAxisMaximum</c>. Null for
    /// "All sessions", whose axis spans the whole run's absolute clock
    /// (FR-UI-40).
    /// </summary>
    public double? XAxisMaximum { get; init; }
}

/// <summary>
/// Turns a finished <see cref="SimulationResult"/> into
/// <see cref="QueueLengthChartData"/>. The recorded series is one sample per
/// processed event (Engine.cs), which can be tens of thousands of points for a
/// long horizon — far more than a live chart needs — so each stage is
/// downsampled to at most <see cref="MaxPoints"/> points first.
/// </summary>
/// <remarks>
/// <para>
/// Downsampling choice (D-122): pairwise min-max decimation, not LTTB. It is
/// O(n) in the sample count, simple enough to defend in a viva in one sentence
/// ("split into buckets, keep each bucket's minimum and maximum"), and — unlike
/// a naive every-Nth point walk — it provably preserves the extremes of the
/// series, which is exactly what a queue-length chart reader looks for (the
/// peak queue). Both candidate algorithms are O(n); LTTB's better fidelity to
/// the *shape* was not worth its far harder-to-explain selection rule.
/// </para>
/// <para>
/// The guarantee we test: the first and last points are always kept verbatim
/// and the global minimum/maximum queue length of the original series still
/// appear in the downsampled output. Empty segments between the extremes are
/// allowed to thin out; the extremes never are.
/// </para>
/// </remarks>
public static class QueueLengthChartService
{
    /// <summary>Hard cap on rendered points per stage (FR-UI-4 P2, NFR-6).</summary>
    public const int MaxPoints = 2000;

    /// <summary>Selector entry that plots every sample on the absolute clock.</summary>
    public const string AllSessionsLabel = "All sessions";

    /// <summary>Fixed caption under the widget title.</summary>
    public const string Caption =
        "Queue length sampled once per processed event; each stage's series is downsampled to at most 2000 points.";

    /// <summary>
    /// Selector entries for the queue chart: <see cref="AllSessionsLabel"/>
    /// followed by one <c>Day n (Weekday)</c> entry per operating session
    /// (FR-UI-40). A closed day never appears because the list is read from
    /// <see cref="SimulationResult.Sessions"/>, which already excludes it.
    /// Returns an empty list for a plain horizon run (no sessions) and for a
    /// null result, so the view hides the selector.
    /// </summary>
    /// <param name="result">The finished run, or null when the run was refused.</param>
    public static IReadOnlyList<string> SessionOptions(SimulationResult? result)
    {
        if (result is null || result.Sessions.Count == 0)
        {
            return Array.Empty<string>();
        }

        var options = new List<string>(result.Sessions.Count + 1) { AllSessionsLabel };
        foreach (var session in result.Sessions)
        {
            options.Add(SessionLabel(session));
        }

        return options;
    }

    /// <summary>
    /// Formats a session the way the selector and the per-session tables label
    /// it: <c>Day 3 (Tue)</c> (owner ruling 2026-10-04, FR-SIM-12/FR-UI-40).
    /// </summary>
    /// <param name="session">The session to label.</param>
    public static string SessionLabel(ClinicSession session) =>
        $"Day {session.Ordinal} ({WeekdayAbbreviation(session.DayOfWeek)})";

    /// <summary>
    /// Builds the chart data from a finished run. A null result (refused run,
    /// or none yet) yields an empty card so the widget can show its "run a
    /// simulation" empty state.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    /// <param name="sessionLabel">
    /// A label from <see cref="SessionOptions"/>. Null or
    /// <see cref="AllSessionsLabel"/> plots every sample on the absolute clock;
    /// any session label filters each stage's series to that session and
    /// rebases time to minutes from the session opening (FR-UI-40).
    /// </param>
    public static QueueLengthChartData Build(SimulationResult? result, string? sessionLabel = null)
    {
        var series = new List<QueueStageSeries>();
        if (result is null)
        {
            return new QueueLengthChartData(false, series);
        }

        ClinicSession? selected = FindSession(result, sessionLabel);
        double sessionStart = selected is { } session
            ? session.BlockIndex * ClinicCalendar.MinutesPerDay
            : 0;
        double sessionLength = result.SessionLengthMinutes > 0 ? result.SessionLengthMinutes : 0;
        double sessionEnd = sessionStart + sessionLength;

        int pointCount = 0;
        int stageIndex = 0;
        foreach (var stage in result.StageMetrics)
        {
            IEnumerable<QueueSample> samples = stage.QueueLengthSeries;
            if (selected is not null)
            {
                samples = samples.Where(s => s.Time >= sessionStart && s.Time <= sessionEnd);
            }

            // Decimation is applied per selection (FR-UI-40), so a filtered
            // session never inherits the thinning chosen for the whole run.
            var points = Decimate(samples.ToList(), MaxPoints)
                .Select(sample => new QueueChartPoint(
                    selected is null ? sample.Time : sample.Time - sessionStart,
                    sample.Length))
                .ToList();
            series.Add(new QueueStageSeries(stage.StageName, points) { StageIndex = stageIndex });
            pointCount += points.Count;
            stageIndex++;
        }

        // A filtered session with no samples still gets an axis: showing the
        // empty 0…165 frame is truthful, while the "run a simulation" empty
        // state would claim no run exists.
        bool hasSeries = pointCount > 0 || (selected is not null && series.Count > 0);
        return new QueueLengthChartData(hasSeries, series)
        {
            XAxisMaximum = selected is null ? null : sessionLength,
        };
    }

    /// <summary>
    /// Resolves a selector label back to the session it names. An unknown label
    /// falls back to "all sessions" rather than throwing: the selection is UI
    /// state, and a stale label must not blank the widget.
    /// </summary>
    private static ClinicSession? FindSession(SimulationResult result, string? sessionLabel)
    {
        if (string.IsNullOrEmpty(sessionLabel) || sessionLabel == AllSessionsLabel)
        {
            return null;
        }

        foreach (var session in result.Sessions)
        {
            if (SessionLabel(session) == sessionLabel)
            {
                return session;
            }
        }

        return null;
    }

    private static string WeekdayAbbreviation(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun",
    };

    /// <summary>
    /// Dollar-bucket min-max decimation (D-122): keeps the first and last
    /// samples verbatim, then splits the interior into buckets of roughly
    /// equal size and keeps the minimum and maximum sample of each bucket (in
    /// time order). Returns the input unchanged when it already fits within
    /// <paramref name="maxPoints"/>. O(n).
    /// </summary>
    /// <param name="samples">The recorded queue-length samples, in time order.</param>
    /// <param name="maxPoints">Hard cap on returned points (defaults to <see cref="MaxPoints"/>).</param>
    public static IReadOnlyList<QueueSample> Decimate(
        IReadOnlyList<QueueSample> samples,
        int maxPoints = MaxPoints)
    {
        if (samples.Count <= maxPoints)
        {
            return samples;
        }

        var output = new List<QueueSample>(maxPoints) { samples[0] };

        // Two slots go to first/last; each interior bucket may emit at most two
        // (its min and its max), so cap the bucket count accordingly.
        int interiorCount = samples.Count - 2;
        int bucketCount = Math.Max(1, (maxPoints - 2) / 2);
        int bucketSize = (int)Math.Ceiling((double)interiorCount / bucketCount);

        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            int start = 1 + bucket * bucketSize;                  // exclusive-of-first index
            int end = Math.Min(start + bucketSize, samples.Count - 1); // exclusive of last sample
            if (start >= end)
            {
                break;
            }

            int minIndex = start;
            int maxIndex = start;
            for (int i = start + 1; i < end; i++)
            {
                if (samples[i].Length < samples[minIndex].Length)
                {
                    minIndex = i;
                }

                if (samples[i].Length > samples[maxIndex].Length)
                {
                    maxIndex = i;
                }
            }

            // Emit min and max in time order (they can coincide, e.g. a flat plateau).
            int earlier = Math.Min(minIndex, maxIndex);
            int later = Math.Max(minIndex, maxIndex);
            output.Add(samples[earlier]);
            if (earlier != later)
            {
                output.Add(samples[later]);
            }
        }

        output.Add(samples[^1]);
        return output;
    }
}