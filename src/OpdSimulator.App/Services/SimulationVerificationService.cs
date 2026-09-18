namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Core.Engine;
using OpdSimulator.Data.Fitting;

/// <summary>
/// One verified series of the simulation output (Phase 8B): the engine-generated
/// samples for a series, the chi-square verdict over them, the histogram card
/// built for those samples and any note explaining why a verdict is absent.
/// </summary>
/// <param name="Label">Series heading, e.g. "Inter-arrival" or "Doctor service".</param>
/// <param name="SampleCount">Number of engine-generated samples in this series.</param>
/// <param name="IntendedFamily">Distribution family the user configured for this series.</param>
/// <param name="ChiSquare">Chi-square goodness-of-fit verdict, or null when skipped/failed.</param>
/// <param name="Histogram">Histogram card data (observed bars + fitted-PDF overlay), or an empty card when nothing could be fitted.</param>
/// <param name="Note">Human explanation for a missing verdict, or null when the fit ran.</param>
public sealed record VerificationReport(
    string Label,
    int SampleCount,
    string IntendedFamily,
    ChiSquareResult? ChiSquare,
    HistogramChartData Histogram,
    string? Note);

/// <summary>
/// Runs the output-side verification of a finished simulation (Phase 8B): takes
/// the samples the engine itself generated during the run and chi-square tests
/// them against the distribution family the user configured — the standard
/// model-verification step from Banks and Law &amp; Kelton. This is the mirror
/// of the input-side chi-square: there we check that a fit is a good model of
/// the historical data; here we check that the engine's random-number output
/// matches the configuration. Pure numbers with no UI types, so it is testable
/// outside an Avalonia session.
/// </summary>
/// <remarks>
/// <para>
/// Families "Exponential", "Normal", "Lognormal", "Gamma" and "Uniform" are
/// fitted and tested through the existing <see cref="FitsService"/> pipeline so
/// the result is exactly comparable to the input-side fits. "Deterministic"
/// schedules no chi-square at all (a constant stream cannot be goodness-of-fit
/// tested) and "General" is flattened to Exponential with an explanatory note
/// (D-126: the engine samples service times exponentially for every stage).
/// </para>
/// <para>
/// Histogram bins come exclusively from <see cref="InputAnalysisService.BuildHistogram"/>,
/// which reuses the chi-square result's own bins — the chart and the verdict can
/// never disagree (same invariant as the input tab).
/// </para>
/// </remarks>
public static class SimulationVerificationService
{
    /// <summary>
    /// Verifies every series the engine generated output samples for: one report
    /// for the inter-arrival series plus one per stage, in stage order. The
    /// series count always matches <c>result.StageMetrics.Count + 1</c> — a
    /// series with no samples still yields a report carrying the explanatory
    /// note (Degenerate-run honesty, AGENTS §12), so the Results widget always
    /// renders one card per series.
    /// </summary>
    /// <param name="result">The completed engine result (Phase 8A adds the generated-sample buffers).</param>
    /// <param name="arrivalFamily">Configured inter-arrival distribution family.</param>
    /// <param name="serviceFamilies">Configured service family per stage (mirrors the single-family model of D-126); missing entries default to "Exponential".</param>
    /// <param name="significanceLevel">Alpha the chi-square verdicts are decided at (config Significance section).</param>
    /// <exception cref="ArgumentNullException">When <paramref name="result"/> or <paramref name="serviceFamilies"/> is null.</exception>
    public static IReadOnlyList<VerificationReport> VerifyAll(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<string> serviceFamilies,
        double significanceLevel)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(serviceFamilies);

        var reports = new List<VerificationReport>
        {
            VerifySeries(
                InputAnalysisService.InterArrivalSeriesLabel,
                result.GeneratedInterArrivalSamples,
                arrivalFamily,
                significanceLevel),
        };

        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            var stage = result.StageMetrics[i];
            string family = i < serviceFamilies.Count
                ? serviceFamilies[i]
                : serviceFamilies is { Count: > 0 } ? serviceFamilies[^1] : "Exponential";
            IReadOnlyList<double> samples = i < result.GeneratedServiceSamplesByStage.Count
                ? result.GeneratedServiceSamplesByStage[i]
                : Array.Empty<double>();
            reports.Add(VerifySeries($"{stage.StageName} service", samples, family, significanceLevel));
        }

        return reports;
    }

    /// <summary>
    /// Fits and verifies one series. "Deterministic" never runs a chi-square
    /// (a constant stream has no distribution to fit — D-128 keeps the note
    /// exact), "General" runs as Exponential with an explanatory note, and
    /// fewer than two samples of any variable family cannot fill a chi-square
    /// bin (note: "Insufficient samples"). Histograms always reuse
    /// <see cref="InputAnalysisService.BuildHistogram"/>, which yields an empty
    /// (seriesless) card whenever no fit was possible.
    /// </summary>
    private static VerificationReport VerifySeries(
        string label,
        IReadOnlyList<double> samples,
        string family,
        double alpha)
    {
        bool deterministic = string.Equals(family, "Deterministic", StringComparison.OrdinalIgnoreCase);
        bool general = string.Equals(family, "General", StringComparison.OrdinalIgnoreCase);

        string? note = deterministic
            ? "Deterministic — chi-square not applicable."
            : general
                ? "General treated as Exponential for verification."
                : null;

        FitReport? fit = null;
        if (!deterministic && samples.Count >= 2)
        {
            // Single source of truth for fit + verdict: the same FitsService the
            // input tab and the results chi-square table use, so the output-side
            // verdict is computed identically to an input-side one.
            string effectiveFamily = general ? "Exponential" : family;
            fit = FitsService.Fit(label, samples, effectiveFamily, alpha);
        }
        else if (!deterministic)
        {
            note ??= "Insufficient samples for chi-square.";
        }

        var histogram = InputAnalysisService.BuildHistogram(fit ?? new FitReport(label, samples, null, null));
        return new VerificationReport(label, samples.Count, family, fit?.ChiSquare, histogram, note);
    }
}