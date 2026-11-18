namespace OpdSimulator.App.Models;

/// <summary>
/// Whether the configured stage list is still the data's own list or one the
/// user has shaped (Phase 9C, D-236). A load may adopt the file's stages only
/// while <see cref="Default"/>; after any count edit, rename or explicit sync,
/// the list belongs to the user and is kept with a named consequence instead.
/// </summary>
public enum StageListProvenance
{
    /// <summary>No user edit yet; a load may adopt the data's stages.</summary>
    Default,

    /// <summary>The user shaped this list; a load must not silently replace it.</summary>
    UserSet,
}
