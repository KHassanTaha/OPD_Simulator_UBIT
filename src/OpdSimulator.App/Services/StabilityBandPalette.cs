namespace OpdSimulator.App.Services;

using Avalonia;
using Avalonia.Media;
using OpdSimulator.Core.Engine;

/// <summary>
/// The colour for a stability band (Phase 8Q.3, D-183).
/// </summary>
/// <remarks>
/// <para>
/// Follows the same contract as <see cref="StageColourPalette"/>: the value is
/// read from the theme on first use so §16.3's "restyle by editing one file"
/// holds, and a literal fallback carrying the same RGB exists for contexts with
/// no live <see cref="Application"/> — plain unit tests, and the headless
/// Avalonia fixture before theme dictionaries resolve. A degraded context
/// therefore paints identical pixels rather than throwing.
/// </para>
/// <para>
/// The three colours are the semantic <c>ColorSuccess</c>, <c>ColorWarning</c>
/// and <c>ColorError</c> tokens, deliberately not the four stage-series colours.
/// A band colour says "this stage is close to saturation"; a stage colour says
/// "this is stage 3". Reusing a series colour for a verdict would make a status
/// read as an identity, which is the same confusion
/// <see cref="StageColourPalette.ImbalanceHighlight"/> already avoids by not
/// borrowing a series colour.
/// </para>
/// </remarks>
public static class StabilityBandPalette
{
    /// <summary>Green — the stage has real headroom.</summary>
    public static Color Green { get; } = ThemeColor("ColorSuccess", 0x14, 0x5C, 0x39);

    /// <summary>Amber — the stage is close enough to saturation to be worth naming.</summary>
    public static Color Amber { get; } = ThemeColor("ColorWarning", 0x8A, 0x53, 0x00);

    /// <summary>Red — arrivals meet or exceed capacity.</summary>
    public static Color Red { get; } = ThemeColor("ColorError", 0xB3, 0x26, 0x1E);

    /// <summary>
    /// The colour for a band, as a brush for a XAML <c>Foreground</c> binding.
    /// </summary>
    /// <param name="band">The band to colour.</param>
    public static IBrush BrushFor(StabilityBand band) =>
        new SolidColorBrush(band switch
        {
            StabilityBand.Green => Green,
            StabilityBand.Amber => Amber,
            _ => Red,
        });

    private static Color ThemeColor(string key, byte r, byte g, byte b)
    {
        try
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var value) == true
                && value is Color colour)
            {
                return colour;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Stability band colour {Key} unresolvable; using theme fallback", key);
        }

        return Color.FromRgb(r, g, b);
    }
}