namespace OpdSimulator.App.Services;

using System.Globalization;
using OpdSimulator.Core.Engine;

/// <summary>
/// Pure description of the waiting-time histogram for one stage (FR-UI-4 P2,
/// Phase 6c.5, reshaped in Phase 8Z / D-224): a zero-wait bar, up to ten
/// fixed-width positive bins, and an optional overflow bar. The stage is chosen
/// by the user in the Results widget; only one stage is ever shown at a time.
/// </summary>
/// <param name="StageName">The stage whose samples are binned.</param>
/// <param name="HasSeries">True when the stage had any waiting-time samples to bin.</param>
/// <param name="Categories">One short bin label per bar, e.g. <c>&gt;0–1</c> or <c>0</c>.</param>
/// <param name="Counts">Patient count per bin, in the same order as <paramref name="Categories"/>.</param>
public sealed record WaitHistogramData(
    string StageName,
    bool HasSeries,
    IReadOnlyList<string> Categories,
    IReadOnlyList<double> Counts)
{
    /// <summary>
    /// Position of the described stage in the run's stage list, so the
    /// histogram paints in the stage's own colour (Phase 8M, D-163) instead of
    /// always taking palette slot 0. Zero when the stage is unknown.
    /// </summary>
    public int StageIndex { get; init; }

    /// <summary>Number of completed waits (waits that ended) contributing to the bars.</summary>
    public int CompletedCount { get; init; }

    /// <summary>Number of censored waits (patients still queued at the run end) contributing to the bars.</summary>
    public int UnfinishedCount { get; init; }

    /// <summary>How many of the observed waits were exactly zero (the first bar).</summary>
    public int ZeroWaitCount { get; init; }

    /// <summary>Fixed width of a regular positive bin in minutes; null when there were no positive waits.</summary>
    public double? BinWidthMinutes { get; init; }

    /// <summary>Lower bound of the overflow bar in minutes; null when no overflow bar was emitted.</summary>
    public double? OverflowFromMinutes { get; init; }

    /// <summary>Longest observed wait in minutes; null when there were no samples.</summary>
    public double? LongestWaitMinutes { get; init; }

    /// <summary>Share of the total each bar represents, in percent, aligned with <see cref="Counts"/>.</summary>
    public IReadOnlyList<double> Percentages { get; init; } = Array.Empty<double>();

    /// <summary>Explanatory line under the widget title (built by the service, not XAML).</summary>
    public string Caption { get; init; } = string.Empty;

    /// <summary>An empty histogram shown before the first run.</summary>
    public static WaitHistogramData Empty { get; } =
        new(string.Empty, false, Array.Empty<string>(), Array.Empty<double>());
}

/// <summary>
/// Builds <see cref="WaitHistogramData"/> from <see cref="StageMetrics.WaitingTimeSamples"/>
/// and the censored <see cref="StageMetrics.UnfinishedWaitSamples"/>. Separate from
/// <see cref="QueueLengthChartService"/> because the two charts answer different
/// questions and consume different engine output: the queue chart is a per-stage
/// time series, the wait histogram is a per-stage distribution of waits.
/// </summary>
public static class WaitHistogramService
{
    /// <summary>Maximum number of regular positive bins before the tail rolls into overflow.</summary>
    public const int MaxRegularBins = 10;

    /// <summary>Caption under the widget title before a run, and for a seriesless stage.</summary>
    public const string Caption = "Patient waits at the selected stage, in minutes.";

    /// <summary>Appended to the caption when the log Y axis is active (empty bins are hidden there).</summary>
    public const string LogScaleNote = " · log scale hides empty bins";

    /// <summary>Empty-state wording shown before the first run.</summary>
    public const string EmptyStateText = "Run a simulation to see the waiting-time distribution.";

    private const double Epsilon = 1e-9;

    // A short, readable ladder of fixed widths (minutes). Picking the first width
    // whose 10-regular-bin range covers the 95th percentile keeps two similar runs
    // on the same axis instead of letting one outlier set the scale (D-224).
    private static readonly double[] BinWidths = { 0.5, 1, 2, 5, 10, 15, 30, 60 };

    /// <summary>
    /// Builds the histogram for one named stage of a finished run. A null
    /// result, an unknown stage, or a stage with no recorded samples yields a
    /// seriesless empty histogram.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    /// <param name="stageName">The selected stage name (see <paramref name="result"/>'s metrics).</param>
    public static WaitHistogramData Build(SimulationResult? result, string stageName)
    {
        if (result is null)
        {
            return new WaitHistogramData(stageName, false, Array.Empty<string>(), Array.Empty<double>());
        }

        int stageIndex = -1;
        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            if (result.StageMetrics[i].StageName == stageName)
            {
                stageIndex = i;
                break;
            }
        }

        var stage = stageIndex < 0 ? null : result.StageMetrics[stageIndex];
        if (stage is null)
        {
            return new WaitHistogramData(stageName, false, Array.Empty<string>(), Array.Empty<double>());
        }

        // The timestamps are dropped here on purpose: the histogram is a
        // distribution of waits, so it reads Minutes only (FR-STAT-9, D-201).
        // Censored waits shape the bars but are counted separately in the caption.
        var histogram = BuildForSamples(
            stageName,
            stage.WaitingTimeSamples.Select(w => w.Minutes).ToArray(),
            stage.UnfinishedWaitSamples.Select(w => w.Minutes).ToArray());
        return histogram with { StageIndex = stageIndex };
    }

    /// <summary>
    /// The stage names a run produced, in order — the histogram's selector
    /// choices. A null or refused result yields none (the selector hides).
    /// </summary>
    public static IReadOnlyList<string> StageNames(SimulationResult? result)
    {
        if (result is null)
        {
            return Array.Empty<string>();
        }

        return result.StageMetrics.Select(m => m.StageName).ToArray();
    }

    /// <summary>
    /// Bins completed and (optional) censored waits into a zero bar, fixed-width
    /// positive bins chosen from <see cref="BinWidths"/>, and an overflow bar.
    /// No sample is dropped: the bar counts always sum to the total sample count.
    /// </summary>
    /// <param name="stageName">Stage owning the samples (carried into the data).</param>
    /// <param name="completed">Waits that ended, in minutes.</param>
    /// <param name="unfinished">
    /// Wait-so-far for patients still queued when the run stopped, in minutes;
    /// normally empty (every GUI run drains). These shape the bars but are
    /// disclosed separately in the caption.
    /// </param>
    public static WaitHistogramData BuildForSamples(
        string stageName,
        IReadOnlyList<double> completed,
        IReadOnlyList<double>? unfinished = null)
    {
        int completedCount = completed.Count;
        int unfinishedCount = unfinished?.Count ?? 0;
        int total = completedCount + unfinishedCount;
        if (total == 0)
        {
            return new WaitHistogramData(stageName, false, Array.Empty<string>(), Array.Empty<double>());
        }

        var all = new double[total];
        for (int i = 0; i < completedCount; i++)
        {
            all[i] = completed[i];
        }
        if (unfinished is not null)
        {
            for (int i = 0; i < unfinishedCount; i++)
            {
                all[completedCount + i] = unfinished[i];
            }
        }

        int zeroCount = all.Count(v => v <= Epsilon);
        double[] positive = all.Where(v => v > Epsilon).OrderBy(v => v).ToArray();
        double longest = all.Max();

        var categories = new List<string>();
        var counts = new List<double>();
        double? binWidth = null;
        double? overflowFrom = null;

        if (zeroCount > 0)
        {
            categories.Add("0");
            counts.Add(zeroCount);
        }

        if (positive.Length > 0)
        {
            double p95 = positive[Math.Min(positive.Length - 1, (int)Math.Ceiling(0.95 * positive.Length) - 1)];
            double width = BinWidths.FirstOrDefault(w => p95 <= MaxRegularBins * w, BinWidths[^1]);
            binWidth = width;

            double maxPositive = positive[^1];
            int regularCount = Math.Min(MaxRegularBins, Math.Max(1, (int)Math.Ceiling(maxPositive / width)));
            double overflowBoundary = MaxRegularBins * width;
            int overflowCount = positive.Count(v => v > overflowBoundary);

            for (int i = 0; i < regularCount; i++)
            {
                categories.Add(BinLabelFor(i, width));
                int bin = i;
                counts.Add(positive.Count(v => v <= overflowBoundary && (int)Math.Ceiling(v / width) - 1 == bin));
            }

            if (overflowCount > 0)
            {
                categories.Add($">{BinLabel(overflowBoundary)}");
                counts.Add(overflowCount);
                overflowFrom = overflowBoundary;
            }
        }

        var percentages = counts.Select(c => c * 100.0 / total).ToArray();
        string caption = BuildCaption(total, completedCount, unfinishedCount, zeroCount, longest, binWidth, overflowFrom);

        return new WaitHistogramData(
            stageName,
            true,
            categories,
            counts)
        {
            CompletedCount = completedCount,
            UnfinishedCount = unfinishedCount,
            ZeroWaitCount = zeroCount,
            BinWidthMinutes = binWidth,
            OverflowFromMinutes = overflowFrom,
            LongestWaitMinutes = longest,
            Percentages = percentages,
            Caption = caption,
        };
    }

    /// <summary>
    /// Hover text for bar <paramref name="index"/>: the short bin label, the
    /// patient count and the share of all waits. Kept here (not in XAML) so the
    /// wording is unit-testable without a chart (D-224 §5.6).
    /// </summary>
    /// <param name="data">The histogram the bar belongs to.</param>
    /// <param name="index">Zero-based bar index.</param>
    public static string BarTooltip(WaitHistogramData data, int index)
    {
        string label = data.Categories[index];
        string unit = label == "0" ? "0 min (no wait)" : $"{label} min";
        return $"{unit} · {data.Counts[index].ToString("0", CultureInfo.InvariantCulture)} patients " +
               $"({data.Percentages[index].ToString("0.#", CultureInfo.InvariantCulture)}%)";
    }

    private static string BuildCaption(
        int total,
        int completedCount,
        int unfinishedCount,
        int zeroCount,
        double longest,
        double? binWidth,
        double? overflowFrom)
    {
        string waits = unfinishedCount > 0
            ? $"{total} waits ({completedCount} completed + {unfinishedCount} still waiting when the run ended)"
            : $"{total} waits";
        var parts = new List<string>
        {
            waits,
            $"{zeroCount} had no wait",
            $"longest {BinLabel(longest)} min",
        };
        if (binWidth is { } width)
        {
            parts.Add($"{BinLabel(width)}-min bins");
        }
        if (overflowFrom is { } overflow)
        {
            parts.Add($"last bar > {BinLabel(overflow)} min");
        }
        if (unfinishedCount > 0)
        {
            parts.Add($"{unfinishedCount} still waiting when the run ended");
        }
        return string.Join(" · ", parts);
    }

    private static string BinLabelFor(int index, double width) => index == 0
        ? $">{BinLabel(width)}"
        : $"{BinLabel(index * width)}–{BinLabel((index + 1) * width)}";

    private static string BinLabel(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
