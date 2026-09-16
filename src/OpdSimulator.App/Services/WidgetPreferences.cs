namespace OpdSimulator.App.Services;

using System.Text.Json;

/// <summary>
/// The persisted, pure-UI preferences the app is allowed to carry across
/// sessions (AGENTS §16.11): which results widgets are visible and which
/// collapsible sections are collapsed. Configuration, data files and results
/// are deliberately NOT persisted — startup is always empty (FR-UI-21).
/// </summary>
public sealed class WidgetPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <summary>Creates preferences rooted at the per-user OpdSimulator data file.</summary>
    public WidgetPreferences()
        : this(DefaultFilePath)
    {
    }

    /// <summary>Creates preferences rooted at an explicit file (used by tests).</summary>
    public WidgetPreferences(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>Per-user prefs file — Linux <c>~/.config/OpdSimulator/ui.json</c>, Windows <c>%APPDATA%\OpdSimulator\ui.json</c>.</summary>
    public static string DefaultFilePath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpdSimulator",
            "ui.json");

    /// <summary>Widget keys checked on in the results panel, seeded all-on (FR-UI-14).</summary>
    public List<string> VisibleWidgets { get; set; } = new() { "metrics", "chiSquare", "trace" };

    /// <summary>Collapsible-section keys that are currently collapsed.</summary>
    public List<string> CollapsedSections { get; set; } = new();

    /// <summary>
    /// Loads the persisted preferences, or returns fresh defaults when none
    /// exist yet or the file is corrupt (corruption is logged, never fatal).
    /// </summary>
    public static WidgetPreferences Load(string? filePath = null)
    {
        filePath ??= DefaultFilePath;
        if (File.Exists(filePath))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<WidgetPreferences>(File.ReadAllText(filePath), JsonOptions);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "UI preferences file could not be parsed; using defaults: {Path}", filePath);
            }
        }

        return new WidgetPreferences(filePath);
    }

    /// <summary>Persists the preferences to disk (creates the parent directory on demand).</summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_filePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}