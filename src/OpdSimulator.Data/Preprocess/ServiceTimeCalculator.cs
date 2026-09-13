namespace OpdSimulator.Data.Preprocess;

using OpdSimulator.Data.Loaders;

/// <summary>
/// Computes per-stage service times from <c>&lt;stage&gt;_start</c>/<c>&lt;stage&gt;_end</c> columns.
/// </summary>
/// <remarks>
/// FR-DATA-4. Stages are auto-detected via <see cref="StagePairDetector"/>. For each
/// stage, a row contributes its service time (<c>end − start</c>) only when both
/// cells parse; rows with a blank or unparseable stage cell are skipped for that
/// stage (the validator reports them — this calculator never assumes).
/// </remarks>
public static class ServiceTimeCalculator
{
    /// <summary>
    /// Computes the service-time samples for every detected stage.
    /// </summary>
    /// <param name="dataSet">Validated data.</param>
    /// <returns>Stage name → service times (minutes). A stage key is present iff it had any usable row.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="dataSet"/> is null.</exception>
    public static IReadOnlyDictionary<string, double[]> Compute(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var pairs = StagePairDetector.Detect(dataSet.Columns);
        var result = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in pairs.Pairs)
            result[pair.Stage] = new List<double>();

        foreach (var row in dataSet.Rows)
        {
            foreach (var pair in pairs.Pairs)
            {
                if (!row.TryGetValue(pair.StartColumn, out string? startText) || !TimeParser.TryParse(startText, out double start))
                    continue;
                if (!row.TryGetValue(pair.EndColumn, out string? endText) || !TimeParser.TryParse(endText, out double end))
                    continue;
                if (end < start)
                    continue; // invalid pair — the validator flags it; skip here.

                result[pair.Stage].Add(end - start);
            }
        }

        return result.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}