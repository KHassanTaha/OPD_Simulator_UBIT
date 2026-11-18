namespace OpdSimulator.App.Services;

using System.Globalization;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Engine;

/// <summary>
/// One decimated point of a stage's queue-length line (Phase 6c.5).
/// </summary>
/// <param name="Time">Simulation clock minutes at the sample.</param>
/// <param name="Length">Number of patients waiting in that stage's queue.</param>
public sealed record QueueChartPoint(double Time, int Length)
{
    /// <summary>
    /// A seam is a synthetic break inserted between two operating sessions on
    /// the compressed axis. Its Y is <see cref="double.NaN"/> when rendered, so
    /// the line stops instead of joining Day 1's last value to Day 2's first
    /// (Phase 8Y, D-223). It is never a real patient count.
    /// </summary>
    public bool IsSeam { get; init; }
}

/// <summary>One stage's queue-length line data.</summary>
/// <param name="StageName">Stage name, used for the chart legend entry.</param>
/// <param name="Points">Decimated points, first and last always kept (D-122).</param>
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
/// One operating session's slice of the compressed horizontal axis (Phase 8Y,
/// D-223). <paramref name="Start"/> and <paramref name="End"/> are display
/// coordinates, not simulation minutes: the closed time between sessions is
/// removed and replaced by a fixed gutter.
/// </summary>
/// <param name="Start">Compressed-axis coordinate where this session begins.</param>
/// <param name="End">Compressed-axis coordinate where this session's slot ends.</param>
/// <param name="SessionOrdinal">The run's day number, used in tick labels.</param>
/// <param name="SessionLabel">Selector label, e.g. <c>Day 3 (Tue)</c>.</param>
public sealed record QueueAxisSegment(
    double Start,
    double End,
    int SessionOrdinal,
    string SessionLabel);

/// <summary>
/// The horizontal axis contract shared by the service (which maps samples into
/// it) and the builder (which pins the LiveCharts axis to it). Keeping the
/// offsets, tick text and tooltip text here gives those two surfaces one source
/// of truth: the chart can never label a compressed coordinate as a minute
/// count (Phase 8Y, D-223).
/// </summary>
/// <param name="Minimum">Axis minimum, always zero.</param>
/// <param name="Maximum">Axis maximum.</param>
/// <param name="Compressed">
/// True for the All-sessions view, where closed time is removed and the segments
/// carry day labels; false for a selected session and for a plain-horizon run.
/// </param>
/// <param name="Segments">Session slices, empty when <paramref name="Compressed"/> is false.</param>
public sealed record QueueTimeAxis(
    double Minimum,
    double Maximum,
    bool Compressed,
    IReadOnlyList<QueueAxisSegment> Segments)
{
    /// <summary>
    /// Finds the segment containing a coordinate. Coordinates outside every
    /// segment clamp to the nearest one, which is defensive only: the axis
    /// maximum is the last segment's end.
    /// </summary>
    /// <param name="x">Compressed-axis coordinate.</param>
    public QueueAxisSegment? SegmentAt(double x)
    {
        if (Segments.Count == 0)
        {
            return null;
        }

        foreach (var segment in Segments)
        {
            if (x >= segment.Start && x <= segment.End)
            {
                return segment;
            }
        }

        return Segments
            .OrderBy(s => Math.Max(0, Math.Max(s.Start - x, x - s.End)))
            .First();
    }

    /// <summary>
    /// Tick text: <c>Day n</c> at a session's opening, <c>Day n +50m</c>
    /// inside it, and a plain number on an uncompressed axis.
    /// </summary>
    /// <param name="x">Axis coordinate supplied by LiveCharts.</param>
    public string FormatTick(double x)
    {
        if (!Compressed || SegmentAt(x) is not { } segment)
        {
            return x.ToString("0.##", CultureInfo.InvariantCulture);
        }

        double offset = x - segment.Start;
        return offset < 0.5
            ? FormattableString.Invariant($"Day {segment.SessionOrdinal}")
            : FormattableString.Invariant($"Day {segment.SessionOrdinal} +{offset:0}m");
    }

    /// <summary>
    /// Tooltip text. On the compressed axis it names the weekday and states
    /// that the offset is measured from the session opening, so a display
    /// coordinate can never be mistaken for a simulation minute.
    /// </summary>
    /// <param name="x">Axis coordinate supplied by LiveCharts.</param>
    public string FormatTooltip(double x)
    {
        if (!Compressed || SegmentAt(x) is not { } segment)
        {
            return x.ToString("0.##", CultureInfo.InvariantCulture);
        }

        double offset = x - segment.Start;
        string weekday = WeekdayFromLabel(segment.SessionLabel);
        return FormattableString.Invariant(
            $"Day {segment.SessionOrdinal} ({weekday}) · {offset:0} min from open");
    }

    private static string WeekdayFromLabel(string label)
    {
        int open = label.IndexOf('(');
        int close = label.IndexOf(')', StringComparison.Ordinal);
        return open >= 0 && close > open ? label[(open + 1)..close] : string.Empty;
    }
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
    /// The horizontal-axis contract. Null only for a plain-horizon run, whose
    /// absolute clock and automatic range are deliberately unchanged (D-223).
    /// </summary>
    public QueueTimeAxis? TimeAxis { get; init; }

    /// <summary>
    /// On a selected session with a drain tail, the minute at which arrivals
    /// close; the builder draws the dashed marker there. Null when there is no
    /// tail (or on the All-sessions view).
    /// </summary>
    public double? ArrivalsCloseAt { get; init; }

    /// <summary>Caption for the view actually being drawn.</summary>
    public string CaptionForView { get; init; } = QueueLengthChartService.Caption;

    /// <summary>
    /// Axis maximum derived from <see cref="TimeAxis"/>; one source of truth,
    /// so the service and the builder cannot disagree about the axis end
    /// (Phase 8Y, D-223).
    /// </summary>
    public double? XAxisMaximum => TimeAxis?.Maximum;
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
/// <para>
/// Phase 8Y (D-223): for runs with operating sessions, samples are attributed
/// to a session with the same block rule <c>CalendarGate.AdvanceTo</c> uses
/// for drain, then projected into per-session slots with a display-only
/// gutter. Every open session keeps a slot even when its queue never rose
/// above zero (owner ruling 2026-11-14).
/// </para>
/// </remarks>
public static class QueueLengthChartService
{
    /// <summary>Hard cap on rendered points per stage (FR-UI-4 P2, NFR-6).</summary>
    public const int MaxPoints = 2000;

    /// <summary>Selector entry that plots every sample on the compressed axis.</summary>
    public const string AllSessionsLabel = "All sessions";

    /// <summary>Caption for a plain-horizon run and the pre-run empty card.</summary>
    public const string Caption =
        "Queue length sampled once per processed event; each stage's series is downsampled to at most 2000 points.";

    /// <summary>Caption for the All-sessions operating-time view.</summary>
    public const string CompressedCaption =
        "Closed nights and days are removed; each label is a session, and the gaps between them are not elapsed time.";

    /// <summary>Caption for a selected session whose queue drains past the arrival window.</summary>
    public const string DrainCaption =
        "Minutes after the 'arrivals close' marker are drain: no new arrivals, only patients still being served.";

    /// <summary>Display-only gap between two session slots; never a session minute.</summary>
    private const double Gutter = 3.0;

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
    /// <see cref="AllSessionsLabel"/> plots every session on the compressed
    /// operating-time axis; any session label plots that session's full drain
    /// on a 0…slot-width axis (FR-UI-40, D-223).
    /// </param>
    public static QueueLengthChartData Build(SimulationResult? result, string? sessionLabel = null)
    {
        var series = new List<QueueStageSeries>();
        if (result is null)
        {
            return new QueueLengthChartData(false, series);
        }

        if (result.Sessions.Count == 0)
        {
            return BuildPlainHorizon(result, series);
        }

        var sessions = result.Sessions;
        int sessionCount = sessions.Count;
        double sessionLength = result.SessionLengthMinutes > 0 ? result.SessionLengthMinutes : 0;
        double[] starts = sessions
            .Select(s => s.BlockIndex * ClinicCalendar.MinutesPerDay)
            .ToArray();

        // Slot widths are taken from ALL raw samples across ALL stages before
        // decimation, so a single late drain sample still widens the slot even
        // if decimation would have dropped it.
        // One bucket array PER STAGE, each with one list per session. The outer
        // dimension is the stage count — sizing it by session count would throw
        // for a run with more stages than open sessions (Phase 6c5 regression).
        var buckets = new List<QueueSample>[result.StageMetrics.Count][];
        var widths = Enumerable.Repeat(sessionLength, sessionCount).ToArray();
        for (int stageIndex = 0; stageIndex < result.StageMetrics.Count; stageIndex++)
        {
            buckets[stageIndex] = new List<QueueSample>[sessionCount];
            for (int i = 0; i < sessionCount; i++)
            {
                buckets[stageIndex][i] = new List<QueueSample>();
            }

            foreach (var sample in result.StageMetrics[stageIndex].QueueLengthSeries)
            {
                int i = SessionIndexAt(sample.Time, starts);
                buckets[stageIndex][i].Add(sample);
                widths[i] = Math.Max(widths[i], sample.Time - starts[i]);
            }
        }

        var segments = new List<QueueAxisSegment>(sessionCount);
        double cursor = 0;
        for (int i = 0; i < sessionCount; i++)
        {
            segments.Add(new QueueAxisSegment(
                cursor, cursor + widths[i], sessions[i].Ordinal, SessionLabel(sessions[i])));
            cursor += widths[i] + Gutter;
        }

        var selected = FindSession(result, sessionLabel);
        int selectedIndex = -1;
        for (int i = 0; i < sessionCount; i++)
        {
            if (selected is { } chosen && sessions[i].Ordinal == chosen.Ordinal)
            {
                selectedIndex = i;
                break;
            }
        }
        bool compressed = selected is null;

        // Seams add one synthetic point per boundary. Reserve room for them so
        // the ≤ MaxPoints contract (NFR-6) still holds on the compressed axis.
        int budget = Math.Max(2, (MaxPoints - (sessionCount - 1)) / sessionCount);
        int pointCount = 0;
        for (int stageIndex = 0; stageIndex < result.StageMetrics.Count; stageIndex++)
        {
            var points = compressed
                ? MapCompressed(buckets[stageIndex], segments, widths, starts, budget)
                : MapSelected(buckets[stageIndex][selectedIndex], starts[selectedIndex]);
            series.Add(new QueueStageSeries(result.StageMetrics[stageIndex].StageName, points)
            {
                StageIndex = stageIndex,
            });
            pointCount += points.Count;
        }

        bool hasSeries = pointCount > 0 || (selected is not null && series.Count > 0);
        var axis = compressed
            ? new QueueTimeAxis(0, segments[^1].End, true, segments)
            : new QueueTimeAxis(0, widths[selectedIndex], false, Array.Empty<QueueAxisSegment>());
        double? arrivalsClose = !compressed && widths[selectedIndex] > sessionLength
            ? sessionLength
            : null;

        return new QueueLengthChartData(hasSeries, series)
        {
            TimeAxis = axis,
            ArrivalsCloseAt = arrivalsClose,
            CaptionForView = compressed ? CompressedCaption : DrainCaption,
        };
    }

    private static QueueLengthChartData BuildPlainHorizon(
        SimulationResult result,
        List<QueueStageSeries> series)
    {
        int pointCount = 0;
        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            var points = Decimate(result.StageMetrics[i].QueueLengthSeries.ToList(), MaxPoints)
                .Select(sample => new QueueChartPoint(sample.Time, sample.Length))
                .ToList();
            series.Add(new QueueStageSeries(result.StageMetrics[i].StageName, points)
            {
                StageIndex = i,
            });
            pointCount += points.Count;
        }

        return new QueueLengthChartData(pointCount > 0 || series.Count > 0, series);
    }

    private static IReadOnlyList<QueueChartPoint> MapCompressed(
        List<QueueSample>[] buckets,
        IReadOnlyList<QueueAxisSegment> segments,
        double[] widths,
        double[] starts,
        int budget)
    {
        var points = new List<QueueChartPoint>();
        for (int i = 0; i < buckets.Length; i++)
        {
            foreach (var sample in Decimate(buckets[i], budget))
            {
                points.Add(new QueueChartPoint(
                    segments[i].Start + (sample.Time - starts[i]), sample.Length));
            }

            if (i < buckets.Length - 1)
            {
                points.Add(new QueueChartPoint(segments[i].End + Gutter / 2, 0) { IsSeam = true });
            }
        }

        return points;
    }

    private static IReadOnlyList<QueueChartPoint> MapSelected(
        List<QueueSample> bucket,
        double sessionStart) =>
        Decimate(bucket, MaxPoints)
            .Select(sample => new QueueChartPoint(sample.Time - sessionStart, sample.Length))
            .ToList();

    /// <summary>
    /// Session attribution by block index: a sample belongs to the last session
    /// whose opening block is at or before it. Drain that crosses midnight is
    /// attributed to the next session, exactly as CalendarGate does.
    /// </summary>
    private static int SessionIndexAt(double time, IReadOnlyList<double> starts)
    {
        int index = 0;
        for (int i = 1; i < starts.Count; i++)
        {
            if (time >= starts[i])
            {
                index = i;
            }
            else
            {
                break;
            }
        }

        return index;
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
