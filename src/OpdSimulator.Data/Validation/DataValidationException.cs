namespace OpdSimulator.Data.Validation;

/// <summary>
/// Thrown by <see cref="DataValidator"/> when a dataset fails validation.
/// </summary>
/// <remarks>
/// Carries <em>every</em> problem found (validation collects all issues rather than
/// stopping at the first), so the CLI can present the complete dirty-data report
/// in one shot (FR-DATA-7).
/// </remarks>
public sealed class DataValidationException : Exception
{
    /// <summary>
    /// Creates the exception holding all validation issues.
    /// </summary>
    /// <param name="issues">The complete set of issues found.</param>
    public DataValidationException(IReadOnlyList<ValidationIssue> issues)
        : base($"Data validation failed with {issues.Count} issue(s).")
    {
        Issues = issues;
    }

    /// <summary>
    /// Creates the exception from a single human-readable reason.
    /// </summary>
    /// <param name="message">The reason.</param>
    public DataValidationException(string message)
        : base(message)
    {
        Issues = Array.Empty<ValidationIssue>();
    }

    /// <summary>All issues found during validation (complete set, never truncated).</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }
}