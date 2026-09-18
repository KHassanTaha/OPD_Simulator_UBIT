namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Engine;

/// <summary>
/// Pure description of the waiting-time histogram for one stage (FR-UI-4 P2,
/// Phase 6c.5): equal-width bins of the recorded waiting times with a patient
/// count per bin. The stage is chosen by the user in the Results widget; only
/// one stage is ever shown at a time.
/// </summary>
/// <param name="StageName">The stage whose samples are binned.</param>
/// <param name="HasSeries">True when the stage had waiting-time samples to bin.</param>
/// <param name="Categories">One bin label per bar, e.g. "[0, 2.1)".</param>
/// <param name="Counts">Patient count per bin.</param>
public sealed record WaitHistogramData(
    string StageName,
    bool HasSeries,
    IReadOnlyList<string> Categories,
    IReadOnlyList<double> Counts)
{
    /// <summary>An empty histogram shown before the first run.</summary>
    public static WaitHistogramData Empty { get; } =
        new(string.Empty, false, Array.Empty<string>(), Array.Empty<double>());
}

/// <summary>
/// Builds <see cref="WaitHistogramData"/> from <see cref="StageMetrics.WaitingTimeSamples"/>.
/// Separate from <see cref="QueueLengthChartService"/> because the two charts
/// answer different questions and consume different engine output: the queue
/// chart is a per-stage time series, the wait histogram is a per-stage
/// distribution of the same stage's recorded waits.
/// </summary>
public static class WaitHistogramService
{
    /// <summary>Number of equal-width bins per histogram (spec'd default).</summary>
    public const int DefaultBinCount = 16;

    /// <summary>Caption under the widget title while the histogram is shown.</summary>
    public const string Caption = "Equal-width bins of waiting time; Y is patient count per bin.";

    /// <summary>Empty-state wording shown before the first run.</summary>
    public const string EmptyStateText = "Run a simulation to see the waiting-time distribution.";

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

        var stage = result.StageMetrics.FirstOrDefault(s => s.StageName == stageName);
        if (stage is null)
        {
            return new WaitHistogramData(stageName, false, Array.Empty<string>(), Array.Empty<double>());
        }

        return BuildForSamples(stageName, stage.WaitingTimeSamples);
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
    /// Bins the given waiting times into <see cref="DefaultBinCount"/> equal-width
    /// bins spanning the observed range [min, max]. When every sample is identical
    /// (zero width) a single category is emitted instead; when there are no
    /// samples the histogram is seriesless. Counting excludes samples below zero,
    /// which the engine never produces (waits are non-negative by construction).
    /// </summary>
    /// <param name="stageName">Stage owning the samples (carried into the data).</param>
    /// <param name="samples">Recorded waiting times in minutes, in completion order.</param>
    public static WaitHistogramData BuildForSamples(string stageName, IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return new WaitHistogramData(stageName, false, Array.Empty<string>(), Array.Empty<double>());
        }

        double min = samples.Min();
        double max = samples.Max();

        // A zero-width range (all samples equal) is a single degenerate bin.
        if (max - min <= 0)
        {
            return new WaitHistogramData(
                stageName,
                true,
                new[] { RangeLabel(min, min) },
                new[] { (double)samples.Count });
        }

        double width = (max - min) / DefaultBinCount;
        var counts = new double[DefaultBinCount];
        foreach (var value in samples)
        {
            int bin = (int)((value - min) / width);
            bin = Math.Clamp(bin, 0, DefaultBinCount - 1); // guard the exact max value
            counts[bin]++;
        }

        var categories = new string[DefaultBinCount];
        for (int i = 0; i < DefaultBinCount; i++)
        {
            categories[i] = RangeLabel(min + i * width, min + (i + 1) * width);
        }

        return new WaitHistogramData(
            stageName,
            true,
            categories,
            counts.Select(c => (double)c).ToArray());
    }

    private static string RangeLabel(double low, double high) =>
        $"[{low.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}, " +
        $"{high.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)})";
}