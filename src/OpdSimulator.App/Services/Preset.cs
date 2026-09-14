namespace OpdSimulator.App.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The on-disk JSON document for a saved configuration preset (FR-UI-19).
/// Field structure follows the schema documented in AGENTS §17.2: mandatory
/// <see cref="SchemaVersion"/> (checked on load), a free-form typed
/// <c>config</c> object (unknown fields ignored, forward-compatible), the
/// data-file reference and the pure-UI <c>view</c> preferences.
/// </summary>
public sealed class Preset
{
    /// <summary>Current schema version; loads of any other version are refused.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Issues with a loaded preset, empty for a (syntactically) valid document.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();

    /// <summary>Mandated schema version — must equal <see cref="CurrentSchemaVersion"/>.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Presentable preset name (the sanitised file base name).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Creation time, UTC ISO-8601.</summary>
    [JsonPropertyName("createdUtc")]
    public string? CreatedUtc { get; set; }

    /// <summary>Last modification time, UTC ISO-8601.</summary>
    [JsonPropertyName("modifiedUtc")]
    public string? ModifiedUtc { get; set; }

    /// <summary>The typed configuration fields (round-trips the config panel).</summary>
    [JsonPropertyName("config")]
    public PresetConfig? Config { get; set; }

    /// <summary>Optional data-file path referenced by the preset.</summary>
    [JsonPropertyName("dataFile")]
    public string? DataFile { get; set; }

    /// <summary>Optional pure-UI preferences (widget visibility, collapsed sections).</summary>
    [JsonPropertyName("view")]
    public PresetView? View { get; set; }

    /// <summary>
    /// Stores any unknown/extra JSON fields so a newer schema version's fields
    /// survive a save round-trip instead of being silently dropped.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraData { get; set; }
}