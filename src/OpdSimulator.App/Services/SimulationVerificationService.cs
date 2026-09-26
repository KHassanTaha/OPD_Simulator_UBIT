namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Core.Distributions;
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
/// PER-STAGE SERIES ARE VERIFIED AGAINST THE CONFIGURED SPEC, NOT A REFIT
/// (Phase 8K, D-154). Refitting the engine's own output and testing the refit
/// answers a different question: it passes whenever the engine produced *some*
/// plausible distribution, so a stage configured as Normal but sampled with a
/// different spread would still be reported as a good fit. Each stage's
/// <see cref="DistributionSpec"/> is passed through
/// <see cref="FittedDistribution.FromSpec"/> into the same
/// <see cref="ChiSquareTest"/> the input tab uses, so the verdict answers what
/// was actually configured.
/// </para>
/// <para>
/// "Deterministic" still schedules no chi-square at all (a constant stream cannot
/// be goodness-of-fit tested) and keeps its exact D-128 note. The former "General"
/// branch is <b>deleted</b>: "General" was never a real family, it was the string
/// the single-family shim used before 8K, and with a typed
/// <see cref="DistributionSpec"/> per stage there is nothing left for it to mean.
/// </para>
/// <para>
/// The inter-arrival series still verifies by family NAME and refit, unlike the
/// stages. That asymmetry is deliberate and recorded rather than tidied away: the
/// per-stage spec is configured and carried on the run parameters, whereas the
/// inter-arrival mean is 1/λ and λ is resolved by the coordinator at run time
/// rather than being a configured distribution. 8K does not change the arrival
/// contract; when an arrival spec is added it should use the spec path here too.
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
    /// <param name="serviceSpecs">Configured service specification per stage, in stage order.</param>
    /// <param name="significanceLevel">Alpha the chi-square verdicts are decided at (config Significance section).</param>
    /// <exception cref="ArgumentNullException">When <paramref name="result"/> or <paramref name="serviceFamilies"/> is null.</exception>
    public static IReadOnlyList<VerificationReport> VerifyAll(
        SimulationResult result,
        string arrivalFamily,
        IReadOnlyList<DistributionSpec> serviceSpecs,
        double significanceLevel)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(serviceSpecs);

        var reports = new List<VerificationReport>
        {
            VerifySeriesByFamilyName(
                InputAnalysisService.InterArrivalSeriesLabel,
                result.GeneratedInterArrivalSamples,
                arrivalFamily,
                significanceLevel),
        };

        for (int i = 0; i < result.StageMetrics.Count; i++)
        {
            var stage = result.StageMetrics[i];
            IReadOnlyList<double> samples = i < result.GeneratedServiceSamplesByStage.Count
                ? result.GeneratedServiceSamplesByStage[i]
                : Array.Empty<double>();

            // A missing spec is a configuration gap, not something to paper over with a
            // default family: reporting "verified against Exponential" for a stage nobody
            // configured would be exactly the kind of silent substitution 8K removes.
            var spec = i < serviceSpecs.Count ? serviceSpecs[i] : null;
            reports.Add(VerifySeriesBySpec($"{stage.StageName} service", samples, spec, significanceLevel));
        }

        return reports;
    }

    /// <summary>
    /// Verifies one per-stage series against the distribution the user CONFIGURED,
    /// rather than against a fresh fit of the engine's output (Phase 8K, D-154).
    /// </summary>
    /// <remarks>
    /// A spec that cannot produce a distribution (a Normal with no σ, a stage with no
    /// spec at all) yields a note instead of a verdict, naming the reason. Silently
    /// falling back to a default family here would hide a misconfiguration behind a
    /// green chi-square, which is the failure mode this change exists to remove.
    /// </remarks>
    private static VerificationReport VerifySeriesBySpec(
        string label,
        IReadOnlyList<double> samples,
        DistributionSpec? spec,
        double alpha)
    {
        if (spec is null)
        {
            return Report(label, samples, "not configured", null, null,
                "No service distribution was configured for this stage.");
        }

        string family = spec.Family.ToString();

        if (spec.Family == DistributionFamily.Deterministic)
        {
            return Report(label, samples, family, null, null, DeterministicNote);
        }

        if (samples.Count < 2)
        {
            return Report(label, samples, family, null, null, "Insufficient samples for chi-square.");
        }

        try
        {
            // The single conversion point from a configured spec to something the
            // chi-square and histogram pipeline understands. No fitting happens.
            var configured = FittedDistribution.FromSpec(spec, samples.Count);
            var chiSquare = ChiSquareTest.Run(samples, configured, alpha);
            return Report(label, samples, family, chiSquare, configured, null);
        }
        catch (Exception ex) when (ex is ArgumentException
                                       or ArgumentNullException
                                       or InvalidOperationException)
        {
            // Two legitimate "no verdict" outcomes land here, and neither may be allowed
            // to escape into the results panel:
            //  - ArgumentException/ArgumentNullException: the spec itself is unusable
            //    (a Normal with no σ, a stage nobody configured).
            //  - InvalidOperationException: ChiSquareTest refuses the test when the
            //    configured distribution puts an expected count below 1 in a bin. This
            //    is now REACHABLE in a way it never was under the old refit: a spec
            //    whose spread is much narrower than the samples the engine produced
            //    leaves the tail bins expecting < 1 observation. That is a genuine
            //    mismatch between configuration and output, so it belongs on the card
            //    as a readable reason — the same treatment FitsService gives a
            //    degenerate fit — not as an exception that blanks the whole widget.
            return Report(label, samples, family, null, null, ex.Message);
        }
    }

    private const string DeterministicNote = "Deterministic — chi-square not applicable.";

    /// <summary>
    /// Builds the card for a report whose fit (or configured distribution) is already
    /// resolved, or whose reason for having no verdict is already known. The histogram
    /// always comes from <see cref="InputAnalysisService.BuildHistogram"/>, which reuses
    /// the verdict's own bins, so the chart and the chi-square cannot disagree.
    /// </summary>
    private static VerificationReport Report(
        string label,
        IReadOnlyList<double> samples,
        string family,
        ChiSquareResult? chiSquare,
        FittedDistribution? distribution,
        string? note) =>
        new(
            label,
            samples.Count,
            family,
            chiSquare,
            InputAnalysisService.BuildHistogram(
                distribution is null
                    ? new FitReport(label, samples, null, null)
                    : new FitReport(label, samples, distribution, chiSquare)),
            note);

    /// <summary>
    /// Fits and verifies one series. "Deterministic" never runs a chi-square
    /// (a constant stream has no distribution to fit — D-128 keeps the note
    /// exact), "General" runs as Exponential with an explanatory note, and
    /// fewer than two samples of any variable family cannot fill a chi-square
    /// bin (note: "Insufficient samples"). Histograms always reuse
    /// <see cref="InputAnalysisService.BuildHistogram"/>, which yields an empty
    /// (seriesless) card whenever no fit was possible.
    /// </summary>
    private static VerificationReport VerifySeriesByFamilyName(
        string label,
        IReadOnlyList<double> samples,
        string family,
        double alpha)
    {
        if (string.Equals(family, "Deterministic", StringComparison.OrdinalIgnoreCase))
        {
            return Report(label, samples, family, null, null, DeterministicNote);
        }

        if (samples.Count < 2)
        {
            return Report(label, samples, family, null, null, "Insufficient samples for chi-square.");
        }

        // Inter-arrival only: no configured arrival spec exists to verify against yet
        // (see the remarks on VerifyAll), so this still refits. The stage path above
        // must not come back to this.
        var fit = FitsService.Fit(label, samples, family, alpha);
        return Report(label, samples, family, fit.ChiSquare, fit.Fitted, null);
    }
}