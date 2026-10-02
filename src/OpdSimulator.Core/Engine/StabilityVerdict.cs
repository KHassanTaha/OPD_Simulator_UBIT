namespace OpdSimulator.Core.Engine;

/// <summary>
/// How close a stage is to saturation, decided from its traffic intensity ρ
/// alone (Phase 8Q.3, D-183).
/// </summary>
public enum StabilityBand
{
    /// <summary>ρ &lt; 0.9 — the stage has real headroom.</summary>
    Green,

    /// <summary>0.9 ≤ ρ &lt; 1.0 — the stage is close enough to saturation that a small rise in demand would push it over.</summary>
    Amber,

    /// <summary>ρ ≥ 1.0 — arrivals meet or exceed service capacity.</summary>
    Red,
}

/// <summary>
/// A pure function over traffic intensity, turning a stage's ρ into a
/// three-band stability verdict (Phase 8Q.3, D-183).
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a static, side-effect-free function over a single
/// double rather than a method on the results view model. Two reasons, and the
/// second is the one that matters:
///// </para>
/// <para>
/// <b>It is a judgement, not a measurement.</b> Where the band boundary sits is
/// a modelling decision, so it belongs in one named place that both the UI and
/// the CLI can ask. A reader who wants to know why 0.93 is amber and 0.88 is
/// green finds the answer in one function, not in a comparison buried in a
/// XAML binding.
/// </para>
/// <para>
/// <b>A bound is testable without a display.</b> The thresholds are the whole
/// content of this decision, and a boundary bug — 0.9 classified green, or a
/// comparison written as ≤ instead of &lt; — is invisible in the app and
/// obvious in a table-driven test. Keeping it pure means the four boundary
/// cases the owner named can be asserted directly.
/// </para>
/// <para>
/// <b>The red branch is defensive on purpose.</b> In normal operation ρ ≥ 1 can
/// never reach this function: <see cref="Engine"/> throws
/// <see cref="UnstableSystemException"/> at construction time, and the GUI
/// additionally gates Start (D-128), so a red verdict is unreachable from the
/// running app. It is implemented anyway, for three reasons. First, the CLI
/// and unit tests construct <see cref="StageMetrics"/> by hand and must be able
/// to exercise every band. Second, a classifier that cannot represent the state
/// it is named after is a classifier with a hole in it, and the day someone
/// relaxes the engine's refusal — a legitimate future change — the UI would have
/// no way to say what it is looking at. Third, and most practically: an
/// unreachable branch that is never executed is a branch whose behaviour is
/// unknown, and "we never wrote it" is not the same claim as "we tested it".
/// </para>
/// </remarks>
public static class StabilityClassifier
{
    /// <summary>
    /// The ρ at or above which a stage is classed <see cref="StabilityBand.Amber"/>.
    /// </summary>
    public const double AmberThreshold = 0.9;

    /// <summary>
    /// Classifies one stage's traffic intensity.
    /// </summary>
    /// <param name="rho">
    /// Traffic intensity for the stage, ρ = λᵢ / (cᵢ·μᵢ). Not validated: ρ is a
    /// computed output of the routing arithmetic, and this function's job is to
    /// report what it is given rather than to police it. A negative value cannot
    /// arise from a well-formed run and classifies green.
    /// </param>
    /// <returns>
    /// <see cref="StabilityBand.Green"/> below <see cref="AmberThreshold"/>,
    /// <see cref="StabilityBand.Amber"/> from the threshold up to but excluding
    /// 1.0, and <see cref="StabilityBand.Red"/> at 1.0 or above.
    /// </returns>
    public static StabilityBand Classify(double rho)
    {
        if (rho >= 1.0)
        {
            return StabilityBand.Red;
        }

        return rho >= AmberThreshold
            ? StabilityBand.Amber
            : StabilityBand.Green;
    }

    /// <summary>
    /// Classifies a set of per-stage intensities and names the stage closest to
    /// saturation (Phase 8Q.3).
    /// </summary>
    /// <param name="rhos">One ρ per stage, in stage order.</param>
    /// <returns>
    /// The verdict for the worst stage, and the name of that stage, or
    /// <see cref="string.Empty"/> for both when <paramref name="rhos"/> is empty.
    /// The worst stage is the one with the highest ρ, and ties resolve to the
    /// earliest stage, because "the bottleneck is Screening or Doctor" is not an
    /// answer and a stable ordering keeps the line from flickering between runs.
    /// </returns>
    public static (StabilityBand Band, string Bottleneck) ClassifySet(
        IReadOnlyList<(string StageName, double Rho)> rhos)
    {
        ArgumentNullException.ThrowIfNull(rhos);

        if (rhos.Count == 0)
        {
            return (StabilityBand.Green, string.Empty);
        }

        var worst = rhos[0];
        for (var i = 1; i < rhos.Count; i++)
        {
            if (rhos[i].Rho > worst.Rho)
            {
                worst = rhos[i];
            }
        }

        return (Classify(worst.Rho), worst.StageName);
    }
}