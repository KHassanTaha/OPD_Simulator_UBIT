using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A small "?" badge shown next to complex/technical controls (FR-UI-8).
/// Hovering reveals <see cref="HelpTip"/> in a tooltip; activating requests
/// the in-program guide at <see cref="HelpAnchor"/> (§17.1 deep-link).
/// </summary>
public partial class InfoIcon : UserControl
{
    /// <summary>Identifies the <see cref="HelpAnchor"/> styled property.</summary>
    public static readonly StyledProperty<string> HelpAnchorProperty =
        AvaloniaProperty.Register<InfoIcon, string>(nameof(HelpAnchor));

    /// <summary>Identifies the <see cref="HelpTip"/> styled property.</summary>
    public static readonly StyledProperty<string> HelpTipProperty =
        AvaloniaProperty.Register<InfoIcon, string>(nameof(HelpTip));

    /// <summary>Raises when the badge is activated; subscribers open the guide at the anchor.</summary>
    public event EventHandler? HelpRequested;

    /// <summary>Gets or sets the guide section anchor to jump to (e.g. "arrival-rate").</summary>
    public string HelpAnchor
    {
        get => GetValue(HelpAnchorProperty);
        set => SetValue(HelpAnchorProperty, value);
    }

    /// <summary>Gets or sets the short explanation shown in the hover tooltip (≤ 120 chars).</summary>
    public string HelpTip
    {
        get => GetValue(HelpTipProperty);
        set => SetValue(HelpTipProperty, value);
    }

    /// <summary>Creates the badge.</summary>
    public InfoIcon()
    {
        InitializeComponent();
    }

    private void OnInfoButtonClick(object? sender, RoutedEventArgs e)
    {
        HelpRequested?.Invoke(this, EventArgs.Empty);
    }
}