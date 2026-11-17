namespace OpdSimulator.App.Models;

/// <summary>
/// Where a stage row's effective service rate came from (Phase 9P, D-242).
/// </summary>
/// <remarks>
/// <para>
/// This replaces Phase 8K's <c>IsMuFittedLocally</c> flag. That flag was set once
/// and never cleared, and the label read it first, so a μ the user overrode would
/// still read "(fitted)" while the run used the typed number — the label
/// describing a different value from the one that runs (seam S3).
/// </para>
/// <para>
/// Transitions: a new row and Clear All produce <see cref="Default"/>; an edit
/// made while a fit exists produces <see cref="UserSet"/>; the explicit G/G/c
/// path filling a blank μ produces <see cref="Fitted"/>; a new file load turns
/// <see cref="Fitted"/> back into <see cref="Default"/> and keeps
/// <see cref="UserSet"/>.
/// </para>
/// <para>
/// <see cref="Default"/> is not "unset" — it is "no deliberate choice has been
/// made about this row", which is why the fit may take it over silently.
/// </para>
/// </remarks>
public enum MuProvenance
{
    /// <summary>Nothing deliberate: the fit (or the comma list) supplies the rate.</summary>
    Default,

    /// <summary>The rate was filled by the explicit G/G/c auto-fit.</summary>
    Fitted,

    /// <summary>The user typed this rate as an override; it wins over the fit.</summary>
    UserSet,
}
