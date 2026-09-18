namespace OpdSimulator.App.Services;

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
public sealed record QueueStageSeries(string StageName, IReadOnlyList<QueueChartPoint> Points);

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

    /// <summary>Fixed caption under the widget title.</summary>
    public const string Caption =
        "Queue length sampled once per processed event; each stage's series is downsampled to at most 2000 points.";

    /// <summary>
    /// Builds the chart data from a finished run. A null result (refused run,
    /// or none yet) yields an empty card so the widget can show its "run a
    /// simulation" empty state.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    public static QueueLengthChartData Build(SimulationResult? result)
    {
        var series = new List<QueueStageSeries>();
        if (result is null)
        {
            return new QueueLengthChartData(false, series);
        }

        int pointCount = 0;
        foreach (var stage in result.StageMetrics)
        {
            var points = Decimate(stage.QueueLengthSeries, MaxPoints)
                .Select(sample => new QueueChartPoint(sample.Time, sample.Length))
                .ToList();
            series.Add(new QueueStageSeries(stage.StageName, points));
            pointCount += points.Count;
        }

        return new QueueLengthChartData(pointCount > 0, series);
    }

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