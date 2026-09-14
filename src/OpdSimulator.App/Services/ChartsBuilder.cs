namespace OpdSimulator.App.Services;

using System.Globalization;
using OpdSimulator.App.Models;
using OpdSimulator.Core.Engine;

/// <summary>One named series of a categorical/column chart.</summary>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values);

/// <summary>A column/bar chart with category labels and one or more series.</summary>
public sealed record CategoricalChartData(string Title, IReadOnlyList<string> Categories, IReadOnlyList<ChartSeries> Series);

/// <summary>A line chart (X/Y pairs, e.g. queue length over time).</summary>
public sealed record LineChartData(string Title, IReadOnlyList<double> X, IReadOnlyList<double> Y, string SeriesName);

/// <summary>All chart inputs for the Input Analysis tab (FR-UI-4).</summary>
public sealed record ChartsData(
    CategoricalChartData InterArrivalHistogram,
    IReadOnlyList<CategoricalChartData> ServiceHistograms,
    CategoricalChartData ChiSquare,
    CategoricalChartData Utilisation,
    IReadOnlyList<LineChartData> QueueOverTime,
    IReadOnlyList<CategoricalChartData> WaitingHistograms);

/// <summary>
/// Turns run metrics + fit reports into plain chart numbers — pure data with
/// no UI types, so the tab renders straight from these and the conversions
/// (binning, down-sampling) are testable without an Avalonia session.
/// </summary>
public static class ChartsBuilder
{
    private const int HistogramBinCount = 16;
    private const int MaxQueuePoints = 2_000;

    /// <summary>Builds every P1/P2 chart read off a completed run (FR-UI-4).</summary>
    public static ChartsData Build(SimulationResult result, IReadOnlyList<FitReport> fits)
    {
        var interArrival = HistogramWithFit(fits.FirstOrDefault(f => f.Label == "Inter-arrival"),
            "Inter-arrival times (minutes)");
        var service = new List<CategoricalChartData>();
        var waiting = new List<CategoricalChartData>();
        foreach (var fit in fits)
        {
            if (fit.Label.EndsWith(" service", StringComparison.Ordinal))
            {
                service.Add(HistogramWithFit(fit, $"{fit.Label} (minutes)"));
            }
            if (fit.Label.Contains("P2", StringComparison.Ordinal))
            {
                // Waiting histograms come from engine samples, not fits.
            }
        }

        // Per-stage waiting-time histograms from the recorded samples (P2).
        foreach (var m in result.StageMetrics)
        {
            waiting.Add(PlainHistogram($"{m.StageName} waiting time (minutes)", m.WaitingTimeSamples));
        }

        var chiSquare = BuildChiSquare(fits);
        var utilisation = BuildUtilisation(result);
        var queueLines = BuildQueueLines(result);

        return new ChartsData(interArrival, service, chiSquare, utilisation, queueLines, waiting);
    }

    private static CategoricalChartData HistogramWithFit(FitReport? fit, string title)
    {
        if (fit is null)
        {
            return new CategoricalChartData(title, Array.Empty<string>(), Array.Empty<ChartSeries>());
        }

        var (edges, midpoints, width) = BinEdges(fit.Samples);
        var observed = BinCounts(fit.Samples, edges);
        var fitted = fit.Fitted is null
            ? Array.Empty<double>()
            : midpoints.Select(m => fit.Fitted.Distribution.Density(m) * width * fit.Samples.Count).ToArray();

        var categories = BinLabels(edges);
        return new CategoricalChartData(title, categories,
            new[]
            {
                new ChartSeries("Observed", observed.Select(v => (double)v).ToArray()),
                new ChartSeries("Fitted distribution", fitted),
            });
    }

    private static CategoricalChartData PlainHistogram(string title, IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return new CategoricalChartData(title, Array.Empty<string>(), Array.Empty<ChartSeries>());
        }

        var (edges, _, _) = BinEdges(samples);
        return new CategoricalChartData(title, BinLabels(edges),
            new[] { new ChartSeries("Count", BinCounts(samples, edges).Select(v => (double)v).ToArray()) });
    }

    private static CategoricalChartData BuildChiSquare(IReadOnlyList<FitReport> fits)
    {
        var first = fits.FirstOrDefault(f => f.ChiSquare is not null);
        if (first?.ChiSquare is not { } chi)
        {
            return new CategoricalChartData("χ² observed vs expected", Array.Empty<string>(), Array.Empty<ChartSeries>());
        }

        var categories = BinLabels(chi.BinEdges);
        return new CategoricalChartData("χ² observed vs expected (" + first.Label + ")",
            categories,
            new[]
            {
                new ChartSeries("Observed", chi.Observed.Select(v => (double)v).ToArray()),
                new ChartSeries("Expected", chi.Expected.ToArray()),
            });
    }

    private static CategoricalChartData BuildUtilisation(SimulationResult result)
    {
        var categories = new List<string>();
        var values = new List<double>();
        foreach (var stage in result.StageMetrics)
        {
            for (int j = 0; j < stage.PerServerUtilisation.Count; j++)
            {
                categories.Add($"{stage.StageName} s{j + 1}");
                values.Add(stage.PerServerUtilisation[j]);
            }
        }

        return new CategoricalChartData("Per-server utilisation", categories,
            new[] { new ChartSeries("Utilisation", values) });
    }

    private static IReadOnlyList<LineChartData> BuildQueueLines(SimulationResult result)
    {
        var lines = new List<LineChartData>();
        foreach (var stage in result.StageMetrics)
        {
            var samples = stage.QueueLengthSeries;
            if (samples.Count == 0)
            {
                continue;
            }

            int stride = Math.Max(1, (int)Math.Ceiling((double)samples.Count / MaxQueuePoints));
            var x = new List<double>();
            var y = new List<double>();
            for (int i = 0; i < samples.Count; i += stride)
            {
                x.Add(samples[i].Time);
                y.Add(samples[i].Length);
            }
            lines.Add(new LineChartData($"{stage.StageName} queue length over time", x, y, "Queue"));
        }
        return lines;
    }

    private static (double[] Edges, double[] Midpoints, double Width) BinEdges(IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return (Array.Empty<double>(), Array.Empty<double>(), 0);
        }

        double min = samples.Min();
        double max = samples.Max();
        if (max - min < 1e-9)
        {
            max = min + 1e-9; // degenerate series (all equal) — avoid zero width
        }

        double width = (max - min) / HistogramBinCount;
        var edges = new double[HistogramBinCount + 1];
        var midpoints = new double[HistogramBinCount];
        for (int i = 0; i <= HistogramBinCount; i++)
        {
            edges[i] = min + i * width;
        }
        for (int i = 0; i < HistogramBinCount; i++)
        {
            midpoints[i] = (edges[i] + edges[i + 1]) / 2;
        }

        return (edges, midpoints, width);
    }

    private static int[] BinCounts(IReadOnlyList<double> samples, double[] edges)
    {
        var counts = new int[edges.Length - 1];
        foreach (double value in samples)
        {
            int bin = (int)((value - edges[0]) / (edges[^1] - edges[0]) * (edges.Length - 1));
            bin = Math.Clamp(bin, 0, counts.Length - 1);
            counts[bin]++;
        }
        return counts;
    }

    private static string[] BinLabels(IReadOnlyList<double> edges)
        => edges.Take(edges.Count - 1).Select(e => e.ToString("0.##", CultureInfo.InvariantCulture)).ToArray();
}