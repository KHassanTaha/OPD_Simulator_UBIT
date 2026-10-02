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

    /// <summary>
    /// Widget keys checked on in the results panel, seeded all-on (FR-UI-14).
    /// Seven, not eight: the event trace is pinned and permanently visible
    /// (FR-UI-35), so it is not a toggleable widget. An existing ui.json may still
    /// name "trace" — that key is read and ignored, never honoured, and is not
    /// written back, so an old file cannot keep the trace hidden.
    /// </summary>
    public List<string> VisibleWidgets { get; set; } = new()
    {
        "metrics", "chiSquare", "utilisation", "queueLength", "waitHistogram",
        "simulationVerification", "analyticalValidation",
    };

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
                    // Rebind rather than return `parsed` (8Q.4, D-185).
                    //
                    // System.Text.Json satisfies the parameterless constructor when
                    // one is public, and `WidgetPreferences()` chains to the DEFAULT
                    // file path. So the deserialised instance carries
                    // ~/.config/OpdSimulator/ui.json no matter which file was read,
                    // and returning it silently redirects every later Save() to the
                    // real per-user file. Callers passing an explicit path — tests,
                    // and any future portable path — would write the developer's own
                    // settings and then read back a file nothing ever updated.
                    return new WidgetPreferences(filePath)
                    {
                        VisibleWidgets = parsed.VisibleWidgets,
                        CollapsedSections = parsed.CollapsedSections,
                    };
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