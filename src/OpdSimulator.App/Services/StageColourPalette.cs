namespace OpdSimulator.App.Services;

using Avalonia;
using Avalonia.Media;
using Serilog;
using SkiaSharp;

/// <summary>
/// The single source of truth for chart series colour (Phase 8M, D-163, FR-UI-27).
/// </summary>
/// <remarks>
/// <para>
/// A user tracking one stage across the queue-over-time chart, the waiting-time
/// histogram and the utilisation bars needs the same stage to be the same colour
/// everywhere, so every chart asks this class by STAGE INDEX — never by stage
/// name. Indexing is what makes it work: stage names are user-editable, so a
/// name-keyed lookup would break the moment someone renames "Doctor" to
/// "Physician", while an index into <c>result.StageMetrics</c> cannot.
/// </para>
/// <para>
/// The four base colours are the <c>ColorChartSeries1..4</c> tokens in
/// <c>Theme.axaml</c>, which are also what <c>ChartTheme.axaml</c>'s
/// <c>BrushChartSeries1..4</c> derive from. Nothing here hardcodes a brand
/// colour; §16.3's "restyle by editing one file" holds. The literal fallbacks
/// below exist only for contexts where no Avalonia <see cref="Application"/> is
/// alive (plain unit tests) and carry the same values, so a degraded context
/// paints identical pixels. They mirror the existing
/// <c>ChartControlBuilder.BrushColor</c> fallback, which is the established
/// pattern in this codebase.
/// </para>
/// </remarks>
public static class StageColourPalette
{
    /// <summary>
    /// The four base series colours, read from the theme on first use and cached
    /// for the process lifetime. The theme is a merged dictionary fixed at
    /// startup, so caching cannot go stale within a run.
    /// </summary>
    public static IReadOnlyList<Color> Colours => BaseColours;

    /// <summary>
    /// The amber used for a server whose utilisation deviates from its stage mean
    /// by more than the imbalance threshold. Deliberately NOT one of the four
    /// series colours: amber means "this server is out of line", and reusing a
    /// stage colour for it would make a flag look like a stage identity.
    /// </summary>
    public static Color ImbalanceHighlight { get; } = ThemeColor("ColorWarning", 0x8A, 0x53, 0x00);

    /// <summary>Hue rotation applied per wrap cycle when a run has more stages than colours.</summary>
    private const double HueStepDegrees = 15.0;

    /// <summary>Base colours, or null until the first lookup resolves the theme.</summary>
    private static IReadOnlyList<Color>? _base;

    private static IReadOnlyList<Color> BaseColours => _base ??= new[]
    {
        ThemeColor("ColorChartSeries1", 0x1B, 0x7A, 0x4C),
        ThemeColor("ColorChartSeries2", 0x0B, 0x72, 0x85),
        ThemeColor("ColorChartSeries3", 0x6B, 0x2F, 0xBA),
        ThemeColor("ColorChartSeries4", 0xE5, 0x46, 0x00),
    };

    /// <summary>
    /// The colour for one stage, addressed by its position in the run's stage
    /// list.
    /// </summary>
    /// <param name="index">Zero-based stage position; negative values are treated as 0.</param>
    /// <remarks>
    /// A run may have more stages than the palette has colours. Rather than
    /// fail or repeat an identical colour, the index wraps and each wrap cycle
    /// rotates the hue by <see cref="HueStepDegrees"/> × cycle number, so stage
    /// 5 is series 1 rotated 15°, stage 9 is series 1 rotated 30°, and so on.
    /// The rotation is deterministic — the same index always yields the same
    /// colour — which is what lets a stage keep its identity across every chart
    /// and across re-renders.
    /// </remarks>
    public static Color ForStageIndex(int index)
    {
        var base_ = BaseColours;
        int safeIndex = index < 0 ? 0 : index;
        int cycle = safeIndex / base_.Count;
        var colour = base_[safeIndex % base_.Count];

        return cycle == 0 ? colour : RotateHue(colour, HueStepDegrees * cycle);
    }

    /// <summary>
    /// The colour for one stage as a brush, for XAML bindings that style a
    /// swatch or a label.
    /// </summary>
    /// <param name="index">Zero-based stage position; negative values are treated as 0.</param>
    public static IBrush BrushForStageIndex(int index) => new SolidColorBrush(ForStageIndex(index));

    /// <summary>
    /// Reads one theme colour, falling back to the literal token value when the
    /// resource is unreachable (no <see cref="Application"/>, or a theme
    /// dictionary whose StaticResource targets are not yet resolvable).
    /// </summary>
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
            // Same failure mode ChartControlBuilder.BrushColor handles: lazy theme
            // dictionaries can throw in headless contexts.
            Serilog.Log.Warning(ex, "Chart colour {Key} unresolvable; using theme fallback", key);
        }

        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// Rotates a colour's hue by <paramref name="degrees"/>, preserving
    /// saturation and lightness so the result reads as the same colour family.
    /// </summary>
    private static Color RotateHue(Color colour, double degrees)
    {
        var sk = new SKColor(colour.R, colour.G, colour.B, colour.A);
        sk.ToHsl(out float hue, out float saturation, out float lightness);
        float rotatedHue = (float)(((hue + degrees) % 360.0 + 360.0) % 360.0);
        var rotated = SKColor.FromHsl(rotatedHue, saturation, lightness, colour.A);
        return Color.FromArgb(rotated.Alpha, rotated.Red, rotated.Green, rotated.Blue);
    }
}
