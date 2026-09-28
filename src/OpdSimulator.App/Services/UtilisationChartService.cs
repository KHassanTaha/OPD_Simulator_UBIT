namespace OpdSimulator.App.Services;

using OpdSimulator.Core.Engine;

/// <summary>
/// One server's utilisation bar (Phase 8M, D-160). Bars are drawn on a
/// CONTRIBUTION scale — each server's share of its stage — so that servers are
/// comparable no matter how many servers their stage has.
/// </summary>
/// <param name="StageName">The stage that owns the server.</param>
/// <param name="ServerNumber">1-based server index within the stage.</param>
/// <param name="Utilisation">Server busy time / operating time, 0..1.</param>
/// <param name="IsOutlier">True when |delta| &gt; <see cref="UtilisationChartService.ImbalanceThreshold"/>.</param>
/// <param name="DeltaFromAverage">Server utilisation minus stage mean utilisation.</param>
public sealed record UtilisationBarRow(
    string StageName,
    int ServerNumber,
    double Utilisation,
    bool IsOutlier,
    double DeltaFromAverage)
{
    /// <summary>Position of the owning stage in the run's stage list (drives the palette).</summary>
    public int StageIndex { get; init; }

    /// <summary>
    /// How many servers the stage has. Stored on the bar rather than passed to
    /// <see cref="Contribution"/> because the rescale is <c>1/c</c> and the
    /// builder must not have to re-derive <c>c</c> per bar.
    /// </summary>
    public int ServerCount { get; init; } = 1;

    /// <summary>
    /// The bar's HEIGHT on the chart: this server's share of its stage's
    /// utilisation, <c>Utilisation / ServerCount</c>. A 3-server stage whose
    /// servers are all 60% busy draws three bars of 20% that sum to the 60%
    /// stage utilisation; a 1-server stage draws its full utilisation. This is
    /// the whole reason the chart is readable: without the rescale, a
    /// 3-server stage's bars would sum to 180% and dwarf a 1-server stage's.
    /// </summary>
    public double Contribution => ServerCount > 0 ? Utilisation / ServerCount : 0;
}

/// <summary>
/// The thin reference line drawn across a stage's bars at that stage's EQUAL
/// SHARE (Phase 8M, D-160) — the height every server in the stage would have if
/// the stage's utilisation were spread perfectly evenly.
/// </summary>
/// <param name="FirstBarIndex">Index of the stage's first bar in the bar list.</param>
/// <param name="LastBarIndex">Index of the stage's last bar in the bar list.</param>
/// <param name="EqualShare">
/// <c>StageUtilisation / ServerCount</c> — the contribution an evenly loaded
/// server would contribute. Bars above it are the busier-than-even servers,
/// below it the idler ones.
/// </param>
public sealed record UtilisationStageReference(
    int FirstBarIndex,
    int LastBarIndex,
    double EqualShare)
{
    /// <summary>Position of the stage in the run's stage list (drives the palette).</summary>
    public int StageIndex { get; init; }
}

/// <summary>
/// One line of the per-server detail table shown under the chart (Phase 8M,
/// D-160). The chart alone cannot say how long a server was actually busy —
/// the contribution scale deliberately hides the operating-time division — so
/// this table is where a reader gets the individual numbers back.
/// </summary>
/// <param name="StageName">The stage that owns the server.</param>
/// <param name="ServerNumber">1-based server index within the stage.</param>
/// <param name="Contribution">This server's share of its stage's utilisation.</param>
/// <param name="StageUtilisation">The stage's overall utilisation, for context.</param>
/// <param name="IsOutlier">True when this server deviates from the stage mean by more than the threshold.</param>
public sealed record UtilisationServerDetail(
    string StageName,
    int ServerNumber,
    double Contribution,
    double StageUtilisation,
    bool IsOutlier);

/// <summary>
/// Pure, engine-independent description of the per-server utilisation chart
/// (FR-STAT-7, FR-UI-28). Bars are laid out per stage in stage order, server
/// 1..n within each stage; every bar is compared against its stage's mean
/// utilisation and flagged when |Δ| = |server − stage mean| exceeds
/// <see cref="ImbalanceThreshold"/>.
/// </summary>
/// <param name="HasSeries">True when the result contributed at least one bar.</param>
/// <param name="Bars">Server bars in rendering order.</param>
/// <param name="ReferenceLines">One equal-share reference line per stage.</param>
public sealed record UtilisationChartData(
    bool HasSeries,
    IReadOnlyList<UtilisationBarRow> Bars,
    IReadOnlyList<UtilisationStageReference> ReferenceLines)
{
    /// <summary>
    /// Per-server detail rows under the chart, in the same order as
    /// <see cref="Bars"/>. Empty when there are no bars.
    /// </summary>
    public IReadOnlyList<UtilisationServerDetail> PerServerDetail { get; init; } =
        Array.Empty<UtilisationServerDetail>();

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

    /// <summary>
    /// Fixed caption under the widget title (FR-STAT-7, FR-UI-28). It has to do
    /// three jobs at once: say what one bar IS (a contribution, not a
    /// utilisation), name the benchmark the dashed line draws, and explain the
    /// amber flag — otherwise amber is a colour the reader has to guess at.
    /// </summary>
    public const string Caption =
        "Each bar is one server's contribution to its stage's utilisation: busy time ÷ " +
        "(server count × operating time). The dashed line marks the equal-share benchmark. " +
        "Contributions within a stage sum to the stage utilisation (≤ 100%). " +
        "Bars in amber deviate from their stage mean by more than 15 percentage points.";

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

        var details = new List<UtilisationServerDetail>();
        int stageIndex = 0;
        foreach (var stage in result.StageMetrics)
        {
            // StageUtilisation is the mean of the stage's per-server
            // utilisations (Engine.cs), the natural "stage average" for the
            // imbalance comparison.
            var average = stage.StageUtilisation;
            var perServer = stage.PerServerUtilisation;
            int currentStage = stageIndex++;
            // The bar height is the server's SHARE of the stage, so the scale
            // needs c. PerServerUtilisation is authoritative about the real
            // count; ServerCount is the fallback for a hand-built result whose
            // per-server list was left empty.
            int serverCount = perServer.Count > 0 ? perServer.Count : stage.ServerCount;
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
                    delta)
                {
                    StageIndex = currentStage,
                    ServerCount = serverCount,
                });
            }

            if (perServer.Count > 0)
            {
                foreach (var bar in bars.Skip(firstBarIndex))
                {
                    details.Add(new UtilisationServerDetail(
                        bar.StageName,
                        bar.ServerNumber,
                        bar.Contribution,
                        average,
                        bar.IsOutlier));
                }

                // The benchmark is the equal share: what each server would
                // contribute if the stage's utilisation were spread evenly.
                // Using the stage average here (the pre-8M value) would sit at
                // c× the bar scale and read as a "everything is broken" line.
                references.Add(new UtilisationStageReference(
                    firstBarIndex,
                    bars.Count - 1,
                    serverCount > 0 ? average / serverCount : 0)
                {
                    StageIndex = currentStage,
                });
            }
        }

        return new UtilisationChartData(bars.Count > 0, bars, references) { PerServerDetail = details };
    }
}
