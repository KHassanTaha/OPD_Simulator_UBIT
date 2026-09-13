namespace OpdSimulator.Data.Validation;

using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Preprocess;

/// <summary>
/// Validates a loaded <see cref="DataSet"/> against the acceptance rules (FR-DATA-2/7).
/// </summary>
/// <remarks>
/// <para>
/// Rules enforced:
/// <list type="bullet">
/// <item>Present columns: <c>arrival_time</c>, <c>departure_stage</c>, and at least
/// one <c>&lt;stage&gt;_start</c>/<c>&lt;stage&gt;_end</c> pair.</item>
/// <item>No empty rows.</item>
/// <item>No blank cells in any required column (arrival_time, departure_stage, every stage pair).</item>
/// <item>Every time value parses via <see cref="TimeParser"/>.</item>
/// <item><c>departure_stage</c> ∈ {Screening, Doctor} (case-insensitive).</item>
/// <item>Arrival times are monotonically non-decreasing.</item>
/// <item>For each stage, <c>end ≥ start</c> (no inverted rows).</item>
/// </list>
/// All issues are collected — validation never stops at the first problem, so the
/// caller sees the complete dirty-data report (FR-DATA-7).
/// </para>
/// <para>
/// NOTE (surfaced conflict): this validator treats any departure stage outside
/// Screening/Doctor as a rejection. That is stricter than the D-008
/// warn-and-exclude treatment of <c>Reception</c>; the M2 kickoff's B1 rule takes
/// precedence in the CLI data path. See DECISIONS entry M2-D-….
/// </para>
/// </remarks>
public static class DataValidator
{
    /// <summary>
    /// Validates the dataset, returning all issues found (empty = valid).
    /// </summary>
    /// <param name="dataSet">The loaded dataset.</param>
    /// <returns>All issues (empty when the data is valid).</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="dataSet"/> is null.</exception>
    public static IReadOnlyList<ValidationIssue> ValidateReturningIssues(DataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var issues = new List<ValidationIssue>();

        var columns = new HashSet<string>(dataSet.Columns, StringComparer.OrdinalIgnoreCase);
        RequireColumn(issues, columns, "arrival_time");
        RequireColumn(issues, columns, "departure_stage");

        var stageSet = StagePairDetector.Detect(dataSet.Columns);
        if (stageSet.Pairs.Count == 0)
            issues.Add(new ValidationIssue(0, null,
                "No <stage>_start/<stage>_end column pair found; at least one service stage is required."));
        foreach (var s in stageSet.UnmatchedStarts)
            issues.Add(new ValidationIssue(0, s, "Column ends with '_start' but no matching '<prefix>_end' column exists."));
        foreach (var e in stageSet.UnmatchedEnds)
            issues.Add(new ValidationIssue(0, e, "Column ends with '_end' but no matching '<prefix>_start' column exists."));

        double? previousArrival = null;

        for (int i = 0; i < dataSet.Rows.Count; i++)
        {
            int rowNumber = i + 1; // 1-based data row
            var row = dataSet.Rows[i];

            bool rowHasAny = row.Values.Any(v => !string.IsNullOrWhiteSpace(v));
            if (!rowHasAny)
            {
                issues.Add(new ValidationIssue(rowNumber, null, "Empty row."));
                continue;
            }

            // -- arrival_time --
            if (row.TryGetValue("arrival_time", out string? arrivalText) && !string.IsNullOrWhiteSpace(arrivalText))
            {
                if (TimeParser.TryParse(arrivalText, out double arrival))
                {
                    if (previousArrival is not null && arrival < previousArrival.Value)
                        issues.Add(new ValidationIssue(rowNumber, "arrival_time",
                            $"Arrival time {arrivalText} is out of order (must be non-decreasing)."));
                    previousArrival = arrival;
                }
                else
                {
                    issues.Add(new ValidationIssue(rowNumber, "arrival_time",
                        $"Time value '{arrivalText}' could not be parsed."));
                }
            }
            else
            {
                issues.Add(new ValidationIssue(rowNumber, "arrival_time", "Missing value in required column."));
            }

            // -- departure_stage --
            if (row.TryGetValue("departure_stage", out string? departureText) && !string.IsNullOrWhiteSpace(departureText))
            {
                if (!IsValidDepartureStage(departureText))
                    issues.Add(new ValidationIssue(rowNumber, "departure_stage",
                        $"Departure stage '{departureText}' must be 'Screening' or 'Doctor' (case-insensitive)."));
            }
            else
            {
                issues.Add(new ValidationIssue(rowNumber, "departure_stage", "Missing value in required column."));
            }

            // -- each stage pair --
            foreach (var pair in stageSet.Pairs)
            {
                ValidateStageCell(issues, row, rowNumber, pair.StartColumn);
                ValidateStageCell(issues, row, rowNumber, pair.EndColumn);

                if (row.TryGetValue(pair.StartColumn, out string? startT)
                    && !string.IsNullOrWhiteSpace(startT)
                    && row.TryGetValue(pair.EndColumn, out string? endT)
                    && !string.IsNullOrWhiteSpace(endT)
                    && TimeParser.TryParse(startT, out double start)
                    && TimeParser.TryParse(endT, out double end)
                    && end < start)
                {
                    issues.Add(new ValidationIssue(rowNumber, pair.EndColumn,
                        $"Service end ({endT}) is before service start ({startT})."));
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// Validates the dataset, throwing when any issue is found.
    /// </summary>
    /// <param name="dataSet">The loaded dataset.</param>
    /// <exception cref="DataValidationException">If any issue was found.</exception>
    public static void Validate(DataSet dataSet)
    {
        var issues = ValidateReturningIssues(dataSet);
        if (issues.Count > 0)
            throw new DataValidationException(issues);
    }

    private static void RequireColumn(List<ValidationIssue> issues, HashSet<string> columns, string column)
    {
        if (!columns.Contains(column))
            issues.Add(new ValidationIssue(0, column, "Required column missing."));
    }

    private static void ValidateStageCell(List<ValidationIssue> issues, IReadOnlyDictionary<string, string> row, int rowNumber, string column)
    {
        if (!row.TryGetValue(column, out string? text) || string.IsNullOrWhiteSpace(text))
        {
            issues.Add(new ValidationIssue(rowNumber, column, "Missing value in required column."));
            return;
        }

        if (!TimeParser.TryParse(text, out _))
            issues.Add(new ValidationIssue(rowNumber, column, $"Time value '{text}' could not be parsed."));
    }

    private static bool IsValidDepartureStage(string stage)
        => stage.Equals("Screening", StringComparison.OrdinalIgnoreCase)
            || stage.Equals("Doctor", StringComparison.OrdinalIgnoreCase);
}