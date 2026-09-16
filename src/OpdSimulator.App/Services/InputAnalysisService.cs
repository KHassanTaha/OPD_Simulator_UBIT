namespace OpdSimulator.App.Services;

using System.Globalization;
using OpdSimulator.App.Models;

/// <summary>
/// One ready-to-draw histogram card (Phase 6C, FR-UI-4 / FR-STAT-8): the
/// observed-frequency columns and the fitted-density overlay, both derived
/// <strong>only</strong> from the chi-square result's own bins. The bins are
/// never recomputed — the observed counts and bin edges are the exact arrays
/// the goodness-of-fit verdict was computed from, so the chart and the
/// chi-square table can never disagree.
/// </summary>
/// <param name="Title">Card heading, e.g. "Inter-arrival time (minutes)".</param>
/// <param name="Caption">Fit + chi-square verdict line (null when no fit ran).</param>
/// <param name="HasSeries">False when the fit failed — the card shows its empty state.</param>
/// <param name="Categories">One bin label per bar, e.g. "[1.2, 2.1)".</param>
/// <param name="Observed">Observed frequency per bin (from the chi-square result).</param>
/// <param name="FittedPdf">Fitted density at each bin midpoint × bin width × N.</param>
public sealed record HistogramChartData(
    string Title,
    string? Caption,
    bool HasSeries,
    IReadOnlyList<string> Categories,
    IReadOnlyList<double> Observed,
    IReadOnlyList<double> FittedPdf);

/// <summary>
/// Derives the Input Analysis charts from a loaded data binding (Phase 6C,
/// 6c.2). Pure numbers with no UI types, so the binning and PDF scaling are
/// testable without an Avalonia session. Mirrors
/// <see cref="SimulationCoordinator"/>'s run fits (labels "Inter-arrival" and
/// "&lt;stage&gt; service") so the tab shows the same fits the results panel
/// does for the same inputs.
/// </summary>
public static class InputAnalysisService
{
    /// <summary>Series label used for the inter-arrival fit — identical to the results panel's.</summary>
    public const string InterArrivalSeriesLabel = "Inter-arrival";

    /// <summary>
    /// Fits every series a usable binding offers, mirroring the run's fit list:
    /// one inter-arrival report plus one per detected stage. An unusable or
    /// arrival-less binding yields no reports (the tab keeps its empty state).
    /// </summary>
    public static IReadOnlyList<FitReport> FitAll(
        DataBindingResult? binding,
        string interArrivalFamily,
        string serviceFamily,
        double alpha)
    {
        if (binding is null || !binding.IsUsable || binding.FittedArrivalRate is null)
        {
            return Array.Empty<FitReport>();
        }

        var reports = new List<FitReport>
        {
            FitsService.Fit(InterArrivalSeriesLabel, binding.InterArrivalMinutes, interArrivalFamily, alpha),
        };
        foreach (var stage in binding.StageNames)
        {
            if (binding.ServiceMinutesByStage.TryGetValue(stage, out var times))
            {
                reports.Add(FitsService.Fit($"{stage} service", times, serviceFamily, alpha));
            }
        }

        return reports;
    }

    /// <summary>
    /// Projects one fit report onto a histogram card. Bins come exclusively
    /// from <see cref="OpdSimulator.Data.Fitting.ChiSquareResult"/> — bin
    /// edges and observed counts are reused as-is, never recomputed. The
    /// fitted overlay is the density at each bin midpoint scaled to a count:
    /// density(midpoint) × binWidth × N (equal-probability bins have different
    /// widths, so each bin uses its own width). When the fit failed the card
    /// is returned without series; the card surface shows its empty state.
    /// </summary>
    public static HistogramChartData BuildHistogram(FitReport fit)
    {
        string title = TitleFor(fit.Label);

        if (fit.Fitted is not { } fitted || fit.ChiSquare is not { } chi)
        {
            return new HistogramChartData(title, null, false, Array.Empty<string>(), Array.Empty<double>(), Array.Empty<double>());
        }

        var edges = chi.BinEdges;
        int binCount = edges.Count - 1;
        int sampleCount = fit.Samples.Count;

        var categories = new List<string>(binCount);
        var observed = new List<double>(binCount);
        var pdf = new List<double>(binCount);
        for (int i = 0; i < binCount; i++)
        {
            double low = edges[i];
            double high = edges[i + 1];
            double width = high - low;
            categories.Add($"[{low.ToString("0.###", CultureInfo.InvariantCulture)}, {high.ToString("0.###", CultureInfo.InvariantCulture)})");
            observed.Add(chi.Observed[i]);
            pdf.Add(fitted.Distribution.Density((low + high) / 2) * width * sampleCount);
        }

        string caption =
            $"{fitted.Name} ({fitted.ParametersText()}) — χ²({chi.DegreesOfFreedom}) = {chi.Statistic.ToString("0.###", CultureInfo.InvariantCulture)}, " +
            $"p = {chi.PValue.ToString("0.###", CultureInfo.InvariantCulture)} — {chi.Decision}";

        return new HistogramChartData(title, caption, true, categories, observed, pdf);
    }

    private static string TitleFor(string label)
        => label == InterArrivalSeriesLabel ? "Inter-arrival time (minutes)" : $"{label} time (minutes)";
}