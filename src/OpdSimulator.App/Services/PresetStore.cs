namespace OpdSimulator.App.Services;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Thrown for deterministic, user-facing preset failures: schema mismatch,
/// missing file, invalid external JSON, collisions without an overwrite.
/// </summary>
public sealed class PresetException : Exception
{
    /// <summary>Creates the exception with a clean message.</summary>
    public PresetException(string message) : base(message)
    {
    }
}

/// <summary>
/// Cross-platform persistence of <see cref="Preset"/> files under
/// <c>&lt;ApplicationData&gt;/OpdSimulator/presets</c> (AGENTS §17.2) —
/// Linux resolves to <c>~/.config/OpdSimulator/presets</c>, Windows to
/// <c>%APPDATA%\OpdSimulator\presets</c>. All naming is sanitised and
/// compared case-insensitively so behaviour is consistent on both platforms.
/// </summary>
public sealed class PresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly string _directory;

    /// <summary>Creates a store rooted at the platform's ApplicationData preset directory.</summary>
    public PresetStore()
        : this(DefaultDirectory)
    {
    }

    /// <summary>Creates a store rooted at an explicit directory (used by tests).</summary>
    /// <param name="directory">The preset root directory; created on demand.</param>
    public PresetStore(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    /// <summary>The default per-user preset directory (never the working directory).</summary>
    public static string DefaultDirectory
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpdSimulator",
            "presets");

    /// <summary>Gets the directory this store writes into.</summary>
    public string DirectoryPath => _directory;

    /// <summary>Writes or overwrites a preset (collisions overwrite by design).</summary>
    /// <param name="preset">The preset to persist; its <see cref="Preset.Name"/>
    /// selects the file.</param>
    /// <exception cref="PresetException">If <see cref="Preset.SchemaVersion"/>
    /// is not the current version or the name is empty after sanitisation.</exception>
    public void Save(Preset preset)
    {
        string name = ValidateName(preset.Name);
        if (preset.SchemaVersion != Preset.CurrentSchemaVersion)
        {
            throw new PresetException(
                $"Preset schema {preset.SchemaVersion} is not supported; expected schema {Preset.CurrentSchemaVersion}.");
        }

        Directory.CreateDirectory(_directory);
        preset.Name = name;
        preset.ModifiedUtc = DateTimeOffset.UtcNow.ToString("O");
        if (string.IsNullOrEmpty(preset.CreatedUtc))
        {
            preset.CreatedUtc = preset.ModifiedUtc;
        }

        string path = PathFor(name);
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions), Encoding.UTF8);
    }

    /// <summary>Loads a preset by name.</summary>
    /// <returns>The parsed, schema-checked preset.</returns>
    /// <exception cref="PresetException">If it does not exist or the schema version is unsupported.</exception>
    public Preset Load(string name)
    {
        string path = ResolveFile(ValidateName(name));
        if (!File.Exists(path))
        {
            throw new PresetException($"Preset '{name}' was not found.");
        }

        return Parse(File.ReadAllText(path, Encoding.UTF8), name);
    }

    /// <summary>Lists preset display names, sorted case-insensitively.</summary>
    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_directory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(_directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .OrderBy(n => n!, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    /// <summary>Deletes a preset (no-op when it does not exist).</summary>
    public void Delete(string name)
    {
        string path = ResolveFile(ValidateName(name));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Renames a preset on disk.</summary>
    /// <exception cref="PresetException">If the source does not exist, the
    /// destination already exists, or both names sanitise to the same file.</exception>
    public void Rename(string currentName, string newName)
    {
        string from = ResolveFile(ValidateName(currentName));
        string to = PathFor(ValidateName(newName));

        if (!File.Exists(from))
        {
            throw new PresetException($"Preset '{currentName}' was not found.");
        }
        if (string.Equals(Path.GetFileNameWithoutExtension(from), Path.GetFileNameWithoutExtension(to), StringComparison.OrdinalIgnoreCase))
        {
            return; // renaming to itself (case-only) is a no-op
        }
        if (File.Exists(to))
        {
            throw new PresetException($"A preset named '{newName}' already exists.");
        }

        File.Move(from, to);
    }

    /// <summary>Duplicates a preset under a new name.</summary>
    /// <exception cref="PresetException">If the source is missing or the copy name collides.</exception>
    public void Duplicate(string sourceName, string copyName)
    {
        string to = PathFor(ValidateName(copyName));
        if (File.Exists(to))
        {
            throw new PresetException($"A preset named '{copyName}' already exists.");
        }

        var preset = Load(sourceName);
        preset.Name = copyName;
        preset.CreatedUtc = DateTimeOffset.UtcNow.ToString("O");
        Save(preset);
    }

    /// <summary>Registers an external JSON preset file, validating it first.</summary>
    /// <returns>The imported preset's name.</returns>
    /// <exception cref="PresetException">If the file is unreadable, schema-mismatched, or its name collides.</exception>
    public string Import(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new PresetException($"The preset file '{sourcePath}' was not found.");
        }

        string json = File.ReadAllText(sourcePath, Encoding.UTF8);
        var preset = Parse(json, null);
        if (Exists(preset.Name!))
        {
            throw new PresetException($"A preset named '{preset.Name}' already exists. Duplicate or overwrite it first.");
        }

        Save(preset);
        return preset.Name!;
    }

    /// <summary>Copies a stored preset out to an arbitrary path.</summary>
    /// <exception cref="PresetException">If the preset does not exist.</exception>
    public void Export(string name, string targetPath)
    {
        string from = PathFor(ValidateName(name));
        if (!File.Exists(from))
        {
            throw new PresetException($"Preset '{name}' was not found.");
        }

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.Copy(from, targetPath, overwrite: true);
    }

    /// <summary>Reports whether a preset name exists (case-insensitive).</summary>
    public bool Exists(string name)
    {
        try
        {
            return File.Exists(ResolveFile(ValidateName(name)));
        }
        catch (PresetException)
        {
            return false;
        }
    }

    private Preset Parse(string json, string? expectedName)
    {
        Preset? preset;
        try
        {
            preset = JsonSerializer.Deserialize<Preset>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PresetException($"The preset file is not valid JSON: {ex.Message}");
        }

        if (preset is null)
        {
            throw new PresetException("The preset file contains no preset document.");
        }
        if (preset.SchemaVersion != Preset.CurrentSchemaVersion)
        {
            throw new PresetException(
                $"Preset schema {preset.SchemaVersion} is not supported; expected schema {Preset.CurrentSchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            if (expectedName is not null)
            {
                preset.Name = expectedName;
            }
            else
            {
                throw new PresetException("The preset has no name.");
            }
        }

        return preset;
    }

    private string PathFor(string name) => Path.Combine(_directory, name + ".json");

    /// <summary>
    /// Resolves a case-insensitive name collision to the file that actually
    /// exists. On Linux the filesystem is case-sensitive, so asking for
    /// <c>casey</c> must still find <c>Casey.json</c> to honour the
    /// cross-platform case-insensitive naming contract (AGENTS §17.2).
    /// </summary>
    private string ResolveFile(string name)
    {
        string direct = PathFor(name);
        if (File.Exists(direct))
        {
            return direct;
        }

        if (!Directory.Exists(_directory))
        {
            return direct;
        }

        string? match = Directory.GetFiles(_directory, "*.json")
            .FirstOrDefault(f
                => string.Equals(Path.GetFileNameWithoutExtension(f), name, StringComparison.OrdinalIgnoreCase));
        return match ?? direct;
    }

    private static string ValidateName(string? name)
    {
        string sanitised = PresetNaming.Sanitize(name);
        if (sanitised.Length == 0)
        {
            throw new PresetException("The preset name is empty or contains only characters that are not allowed on disk.");
        }
        return sanitised;
    }

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };
}