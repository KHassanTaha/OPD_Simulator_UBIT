namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;

/// <summary>
/// One stage's historical utilisation, computed from a loaded data file and the
/// number of servers the user records as having been open when the data was
/// collected (Phase 8Q.1, D-178).
/// </summary>
/// <param name="StageName">Clinic stage name as detected from the file's columns.</param>
/// <param name="ServerCount">Servers recorded as present at this stage during collection (c).</param>
/// <param name="TotalServiceMinutes">Σ (stage_end − stage_start) across every row that has both values.</param>
/// <param name="OperatingMinutes">Operating time the file covers — see <see cref="HistoricalOperatingBasis"/>.</param>
/// <param name="StageUtilisation">
/// <c>TotalServiceMinutes ÷ (ServerCount × OperatingMinutes)</c>. Stage-level only.
/// </param>
/// <remarks>
/// <para>
/// This is a <b>stage-level</b> figure. It is the fraction of the stage's available
/// server-time that the file records as spent in service, so it answers "how busy
/// was this stage?" and not "how busy was server 3?".
/// </para>
/// <para>
/// <b>Per-server utilisation is assumed even.</b> The clinic data carries
/// <c>&lt;stage&gt;_start</c>/<c>&lt;stage&gt;_end</c> pairs and no server-ID column,
/// so there is nothing in the file that says <i>which</i> table a given patient
/// occupied. A per-server split would require inventing that assignment. The
/// figure shown per server is therefore the stage figure itself, and the UI says
/// so in words rather than implying the data supports a finer answer.
/// </para>
/// </remarks>
public sealed record HistoricalStageMetrics(
    string StageName,
    int ServerCount,
    double TotalServiceMinutes,
    double OperatingMinutes,
    double StageUtilisation)
{
    /// <summary>
    /// True when the stage's busy time exceeds its available server-time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reachable, and not a bug: <c>Σ service ÷ (c × operating)</c> has no ceiling
    /// when c is a <i>recorded</i> number. A clinic that ran two screening tables
    /// but was told "1" produces 199%, and that is true information about the
    /// record rather than an arithmetic error.
    /// </para>
    /// <para>
    /// The simulation cannot produce this — <c>NetworkTopology.Validate</c> refuses
    /// any run whose ρ ≥ 1 (FR-VAL-1) — so a user can legitimately see a
    /// historical figure above 100% beside a run that refused. The two are
    /// answering different questions: one divides <i>observed</i> busy time by a
    /// <i>remembered</i> server count, the other divides <i>modelled</i> arrival
    /// rate by a <i>configured</i> capacity.
    /// </para>
    /// </remarks>
    public bool ExceedsCapacity => StageUtilisation > 1.0;

    /// <summary>Total available server-time, the divisor of <see cref="StageUtilisation"/>.</summary>
    public double AvailableServerMinutes => ServerCount * OperatingMinutes;

    /// <summary>
    /// Why this figure is above 100%, in the user's terms. Empty when it is not
    /// (FR-UI-17's cause-and-remedy rule applied to a computed figure).
    /// </summary>
    public string CapacityWarning =>
        ExceedsCapacity
            ? $"{StageName} recorded {TotalServiceMinutes:F1} min of service but {ServerCount} "
              + $"server(s) over {OperatingMinutes:F1} operating minutes gives only "
              + $"{AvailableServerMinutes:F1} min of server-time — "
              + $"{StageUtilisation * 100:F1}% is above 100%. Either more than "
              + $"{ServerCount} server(s) were open during collection, or the count "
              + $"of {ServerCount} is wrong. Raise the server count above."
            : "";
}

/// <summary>
/// Which divisor produced a historical utilisation figure (Phase 8Q.1, ruling 3).
/// </summary>
/// <remarks>
/// The two bases are not interchangeable, so the UI names the one it used rather
/// than printing a bare percentage. A reader who divides the printed busy minutes
/// by the printed operating minutes must be able to reproduce the printed figure
/// from what is on screen — the defect D-176 recorded in the calculations receipt,
/// which printed the right λ beside a line describing a different window.
/// </remarks>
public enum HistoricalOperatingBasis
{
    /// <summary>Operating time is not yet known, so no utilisation is offered.</summary>
    None = 0,

    /// <summary>
    /// The file's own <c>session_date</c> values: sessions × the clinic's 165
    /// operating minutes per session (D-172). The same divisor the run uses, so
    /// the historical and simulated figures are directly comparable.
    /// </summary>
    ObservedWindow = 1,

    /// <summary>
    /// A file with no <c>session_date</c> column, so there is no session structure
    /// to count. Operating time is the span from the first arrival to the last
    /// service completion instead.
    /// </summary>
    SpannedWindow = 2,
}

/// <summary>
/// Computes stage-level historical utilisation from a loaded data file and the
/// server counts the user recorded for it (Phase 8Q.1, D-178).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the user supplies the server count.</b> Historical utilisation is busy
/// time over available server-time, and the denominator's server count is not in
/// the file. A stage's busy time alone says nothing about how loaded it was: 40
/// minutes of screening work is 40% of a one-table hour and 20% of a two-table
/// hour. Without c the figure is uncomputable, and — worse — silently ambiguous,
/// because a reader would take 40 minutes at face value as a percentage against a
/// single server whether or not two were open.
/// </para>
/// <para>
/// <b>Why it is the user's number and not a guess.</b> The alternative — assuming
/// c = 1 — is worse than asking, because it produces a confident wrong answer
/// rather than an obviously missing one. A clinic that ran two screening tables
/// and is recorded as one reads as 199% busy, which at least announces that
/// something is inconsistent. Assuming one server would have printed 99% and the
/// inconsistency would have gone unmentioned.
/// </para>
/// </remarks>
public static class HistoricalMetricsService
{
    /// <summary>Clinic operating minutes per session (D-172).</summary>
    private const double MinutesPerSession = 165.0;

    /// <summary>
    /// Computes historical utilisation per stage.
    /// </summary>
    /// <param name="binding">The analysed data file.</param>
    /// <param name="serverCounts">
    /// Stage name → recorded server count. A stage absent from the dictionary is
    /// skipped rather than defaulted, so a missing count surfaces as a missing
    /// figure instead of a fabricated 1-server one.
    /// </param>
    /// <returns>
    /// One entry per stage that has both a server count and usable service times,
    /// in the file's detected stage order. Empty when the file could not be read
    /// or no operating time can be established.
    /// </returns>
    public static IReadOnlyList<HistoricalStageMetrics> ComputeFor(
        DataBindingResult binding,
        IReadOnlyDictionary<string, int> serverCounts)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(serverCounts);

        var operating = OperatingTimeFor(binding);
        if (operating.Minutes is not > 0 || binding.DataSet is null)
            return Array.Empty<HistoricalStageMetrics>();

        var result = new List<HistoricalStageMetrics>();

        foreach (string stage in binding.StageNames)
        {
            if (!serverCounts.TryGetValue(stage, out int servers) || servers < 1)
                continue;

            // ServiceMinutesByStage is keyed by the same stage names the detector
            // produced, and holds one sample per row with both cells parseable.
            if (!binding.ServiceMinutesByStage.TryGetValue(stage, out var times) || times.Count == 0)
                continue;

            double total = times.Sum();
            result.Add(new HistoricalStageMetrics(
                stage,
                servers,
                total,
                operating.Minutes.Value,
                total / (servers * operating.Minutes.Value)));
        }

        return result;
    }

    /// <summary>
    /// The operating time a set of figures was divided by, and which basis it was
    /// (ruling 3).
    /// </summary>
    /// <param name="binding">The analysed data file.</param>
    /// <returns>
    /// The minutes and the basis. Minutes is null when the file has no session
    /// column and no arrival/service times to span.
    /// </returns>
    public static (double? Minutes, HistoricalOperatingBasis Basis) OperatingTimeFor(
        DataBindingResult binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        // Basis 1 — the file declares its sessions, so use the clinic's own
        // operating minutes per session. This is the divisor a run uses, which is
        // the whole reason it is preferred: a historical figure and a simulated
        // figure divided by different numbers could not be compared, and the UI
        // invites exactly that comparison.
        if (binding.ObservedWindow is { } window && window.OperatingMinutes > 0)
            return (window.OperatingMinutes, HistoricalOperatingBasis.ObservedWindow);

        // Basis 2 — no session column, so the only operating time available is the
        // span the data actually covers. This includes any idle time inside that
        // span, which is why it is the fallback and not the default.
        double? spanned = SpannedWindowMinutes(binding);
        return spanned is > 0
            ? (spanned, HistoricalOperatingBasis.SpannedWindow)
            : (null, HistoricalOperatingBasis.None);
    }

    /// <summary>
    /// The label naming which divisor produced a figure (rulings 3, 4).
    /// </summary>
    /// <param name="basis">The basis a figure was computed on.</param>
    /// <param name="window">The observed window, when the basis is the observed one.</param>
    /// <returns>One sentence a reader can check the division against.</returns>
    public static string DescribeBasis(HistoricalOperatingBasis basis, ObservationWindow? window) =>
        basis switch
        {
            HistoricalOperatingBasis.ObservedWindow =>
                $"{window?.OperatingMinutes:F0} operating minutes from the file's own session dates "
              + $"({ObservationWindowService.Describe(window!)} × {MinutesPerSession:F0} min per session) "
              + "— the same divisor the simulation uses.",

            HistoricalOperatingBasis.SpannedWindow =>
                "single-session file — spanned window used "
              + "(last service completion − first arrival). This span includes any "
              + "idle time inside it, so it is not the clinic's operating time.",

            _ => "No operating time could be established from this file.",
        };

    /// <summary>
    /// First arrival to last service completion, for a file with no session column.
    /// </summary>
    /// <param name="binding">The analysed data file.</param>
    /// <returns>The span in minutes, or null when the file has neither times.</returns>
    private static double? SpannedWindowMinutes(DataBindingResult binding)
    {
        if (binding.DataSet is not { } dataSet)
            return null;

        double? firstArrival = null;
        double lastEnd = double.NegativeInfinity;

        foreach (var row in dataSet.Rows)
        {
            if (row.TryGetValue("arrival_time", out string? arrivalText)
                && OpdSimulator.Data.Preprocess.TimeParser.TryParse(arrivalText, out double arrival)
                && (firstArrival is null || arrival < firstArrival))
            {
                firstArrival = arrival;
            }

            // The last completion across every stage, so a doctor finishing after
            // the final screening is counted.
            foreach (var stage in binding.StageNames)
            {
                if (!row.TryGetValue($"{stage.ToLowerInvariant()}_end", out string? endText)
                    || !OpdSimulator.Data.Preprocess.TimeParser.TryParse(endText, out double end))
                {
                    continue;
                }

                if (end > lastEnd)
                    lastEnd = end;
            }
        }

        return firstArrival is null || lastEnd == double.NegativeInfinity
            ? null
            : lastEnd - firstArrival.Value;
    }
}