using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Interim shell filler that shows a theme-resourced "coming in Phase N" card
/// in a tab until the real panel replaces it. Reused by all four shell tabs
/// (Simulation config/results, Input Analysis, Token Generator, Help).
/// </summary>
public partial class PlaceholderContent : UserControl
{
    /// <summary>
    /// Large heading of the placeholder card.
    /// </summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PlaceholderContent, string?>(nameof(Title));

    /// <summary>
    /// Muted sub-line describing what will eventually occupy the panel.
    /// </summary>
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<PlaceholderContent, string?>(nameof(Hint));

    public PlaceholderContent() => InitializeComponent();

    /// <summary>
    /// Large heading of the placeholder card.
    /// </summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Muted sub-line describing what will eventually occupy the panel.
    /// </summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}