namespace OpdSimulator.App.Models;

/// <summary>
/// Where a data-derivable configuration field's current value came from
/// (Phase 9C, D-232). Deliberately shaped like 9P's
/// <c>MuProvenance</c> so the two read the same way in a debugger:
/// <see cref="Default"/> means no provenance decision has been made and the
/// ordinary precedence applies; the other two are decisions.
/// </summary>
public enum FieldProvenance
{
    /// <summary>No decision yet; a load may write this field.</summary>
    Default,

    /// <summary>The current value was written by a fit on load.</summary>
    AutoFitted,

    /// <summary>The user chose this value; a later load must keep it and warn.</summary>
    UserSet,
}
