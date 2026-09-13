namespace OpdSimulator.Data.Preprocess;

/// <summary>
/// A detected stage column pair (<c>&lt;stage&gt;_start</c> / <c>&lt;stage&gt;_end</c>).
/// </summary>
/// <param name="Stage">Stage name (the prefix before <c>_start</c> / <c>_end</c>).</param>
/// <param name="StartColumn">The <c>&lt;stage&gt;_start</c> column name.</param>
/// <param name="EndColumn">The <c>&lt;stage&gt;_end</c> column name.</param>
public readonly record struct StagePair(string Stage, string StartColumn, string EndColumn);