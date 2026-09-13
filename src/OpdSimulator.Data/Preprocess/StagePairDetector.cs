namespace OpdSimulator.Data.Preprocess;

using OpdSimulator.Data.Loaders;

/// <summary>
/// Detects per-stage <c>_start</c>/<c>_end</c> column pairs in a dataset's headers.
/// </summary>
/// <remarks>
/// A stage is any column named <c>&lt;stage&gt;_start</c> with a matching
/// <c>&lt;stage&gt;_end</c> column (case-insensitive). This is the generic
/// N-stage detection FR-DATA-9 guarantees: adding a new stage to a file is a
/// configuration change, never a code change. Unmatched <c>_start</c> or
/// <c>_end</c> columns are also surfaced so validation can report them.
/// </remarks>
public static class StagePairDetector
{
    /// <summary>
    /// Detects complete stage pairs plus any unmatched stage columns.
    /// </summary>
    /// <param name="columns">Column headers from a <see cref="DataSet"/>.</param>
    /// <returns>Matched pairs and the unmatched <c>_start</c>/<c>_end</c> column names.</returns>
    public static StageColumnSet Detect(IReadOnlyList<string> columns)
    {
        var starts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ends = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string c in columns)
        {
            if (c.EndsWith("_start", StringComparison.OrdinalIgnoreCase)) starts.Add(c);
            else if (c.EndsWith("_end", StringComparison.OrdinalIgnoreCase)) ends.Add(c);
        }

        var pairs = new List<StagePair>();
        var unmatchedStarts = new List<string>();
        var unmatchedEnds = new List<string>();

        foreach (string start in starts)
        {
            string stage = start[..^"_start".Length];
            string expectedEnd = stage + "_end";
            if (ends.Contains(expectedEnd))
                pairs.Add(new StagePair(stage, start, expectedEnd));
            else
                unmatchedStarts.Add(start);
        }

        foreach (string end in ends)
        {
            string stage = end[..^"_end".Length];
            string expectedStart = stage + "_start";
            if (!starts.Contains(expectedStart))
                unmatchedEnds.Add(end);
        }

        return new StageColumnSet(pairs.OrderBy(p => p.Stage, StringComparer.OrdinalIgnoreCase).ToList(),
            unmatchedStarts, unmatchedEnds);
    }
}

/// <summary>
/// Result of <see cref="StagePairDetector.Detect"/>: complete pairs plus leftovers.
/// </summary>
/// <param name="Pairs">Complete <c>_start</c>/<c>_end</c> pairs, stage-sorted.</param>
/// <param name="UnmatchedStarts">Columns ending <c>_start</c> with no matching <c>_end</c>.</param>
/// <param name="UnmatchedEnds">Columns ending <c>_end</c> with no matching <c>_start</c>.</param>
public sealed record StageColumnSet(
    IReadOnlyList<StagePair> Pairs,
    IReadOnlyList<string> UnmatchedStarts,
    IReadOnlyList<string> UnmatchedEnds);