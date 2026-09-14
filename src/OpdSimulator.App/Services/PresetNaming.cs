namespace OpdSimulator.App.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Naming rules shared by the preset store and its tests (AGENTS §17.2): a
/// file-safe display name is the sanitised user text with cross-platform
/// reserved characters removed, and presence checks are always
/// case-insensitive so Linux and Windows behave identically.
/// </summary>
public static class PresetNaming
{
    private const string ReservedChars = "/\\:*?\"<>|";

    /// <summary>
    /// Removes path/reserved/control characters from a preset display name.
    /// </summary>
    /// <returns>The sanitised name (may be empty).</returns>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsControl(c) || ReservedChars.Contains(c))
            {
                continue;
            }
            builder.Append(c);
        }

        return builder.ToString().Trim();
    }
}