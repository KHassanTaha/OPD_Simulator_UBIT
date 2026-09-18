namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Engine;

/// <summary>
/// One server's utilisation bar. <paramref name="DeltaFromAverage"/> is the
/// server's utilisation minus its stage's mean utilisation, so a positive
/// delta means "busier than the stage" and a negative one "idler".
/// </summary>
/// <param name="StageName">The stage that owns the server.</param>
/// <param name="ServerNumber">1-based server index within the stage.</param>
/// <param name="Utilisation">Server busy time / operating time.</param>
/// <param name="IsOutlier">True when |delta| &gt; <see cref="UtilisationChartService.ImbalanceThreshold"/>.</param>
/// <param name="DeltaFromAverage">Server utilisation minus stage mean utilisation.</param>
public sealed record UtilisationBarRow(
    string StageName,
    int ServerNumber,
    double Utilisation,
    bool IsOutlier,
    double DeltaFromAverage);

/// <summary>
/// The thin reference line drawn at a stage's mean utilisation, spanning that
/// stage's bars.
/// </summary>
/// <param name="FirstBarIndex">Index of the stage's first bar in the bar list.</param>
/// <param name="LastBarIndex">Index of the stage's last bar in the bar list.</param>
/// <param name="Average">Stage utilisation (mean of per-server utilisations, per D-016/D-050).</param>
public sealed record UtilisationStageReference(int FirstBarIndex, int LastBarIndex, double Average);

/// <summary>
/// Pure, engine-independent description of the per-server utilisation chart
/// (FR-STAT-7, Phase 6c.4). Bars are laid out per stage in stage order,
/// server 1..n within each stage; every bar is compared against its stage's
/// mean utilisation and flagged when |Δ| = |server − stage mean| exceeds
/// <see cref="ImbalanceThreshold"/>.
/// </summary>
/// <param name="HasSeries">True when the result contributed at least one bar.</param>
/// <param name="Bars">Server bars in rendering order.</param>
/// <param name="ReferenceLines">One reference line per stage.</param>
public sealed record UtilisationChartData(
    bool HasSeries,
    IReadOnlyList<UtilisationBarRow> Bars,
    IReadOnlyList<UtilisationStageReference> ReferenceLines)
{
    /// <summary>An empty card shown before the first run.</summary>
    public static UtilisationChartData Empty { get; } =
        new(false, Array.Empty<UtilisationBarRow>(), Array.Empty<UtilisationStageReference>());
}

/// <summary>
/// Turns a finished <see cref="SimulationResult"/> into
/// <see cref="UtilisationChartData"/>. Lives in Services because the Results
/// widget receives whole <see cref="SimulationResult"/> objects; the returned
/// record is deliberately UI-agnostic so the imbalance logic stays testable in
/// headless tests without a chart.
/// </summary>
public static class UtilisationChartService
{
    /// <summary>A server is flagged when it deviates from its stage mean by more than this.</summary>
    public const double ImbalanceThreshold = 0.15;

    /// <summary>Fixed caption under the widget title (FR-STAT-7).</summary>
    public const string Caption = "Per-server utilisation (imbalance flagged where |Δ| > 0.15).";

    /// <summary>
    /// Builds the chart data from a finished run. No stage with servers
    /// contributes no bar; a null result (refused run, or none yet) yields an
    /// empty card so the widget can show its "run a simulation" empty state.
    /// </summary>
    /// <param name="result">The completed simulation result, or null when the run was refused.</param>
    public static UtilisationChartData Build(SimulationResult? result)
    {
        var bars = new List<UtilisationBarRow>();
        var references = new List<UtilisationStageReference>();
        if (result is null)
        {
            return new UtilisationChartData(false, bars, references);
        }

        foreach (var stage in result.StageMetrics)
        {
            // StageUtilisation is the mean of the stage's per-server
            // utilisations (Engine.cs), the natural "stage average" for the
            // imbalance comparison.
            var average = stage.StageUtilisation;
            var perServer = stage.PerServerUtilisation;
            var firstBarIndex = bars.Count;
            for (int i = 0; i < perServer.Count; i++)
            {
                var utilisation = perServer[i];
                var delta = utilisation - average;
                bars.Add(new UtilisationBarRow(
                    stage.StageName,
                    i + 1,
                    utilisation,
                    Math.Abs(delta) > ImbalanceThreshold,
                    delta));
            }

            if (perServer.Count > 0)
            {
                references.Add(new UtilisationStageReference(firstBarIndex, bars.Count - 1, average));
            }
        }

        return new UtilisationChartData(bars.Count > 0, bars, references);
    }
}