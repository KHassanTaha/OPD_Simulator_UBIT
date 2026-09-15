using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A circular "?" glyph that shows a tooltip (≤120 chars, AGENTS §16.2).
/// The long-form rationale lives in the on-top guide section named by
/// <see cref="HelpAnchor"/> so clicking a future guide link can deep-link.
/// </summary>
public partial class InfoIcon : UserControl
{
    public InfoIcon()
    {
        InitializeComponent();
    }

    /// <summary>The short tooltip text shown on hover.</summary>
    public static readonly StyledProperty<string> HelpTextProperty =
        AvaloniaProperty.Register<InfoIcon, string>(nameof(HelpText));

    /// <summary>The tooltip text (≤120 chars) shown on hover.</summary>
    public string HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    /// <summary>Guide section anchor this icon deep-links to (e.g. "arrival-rate").</summary>
    public static readonly StyledProperty<string> HelpAnchorProperty =
        AvaloniaProperty.Register<InfoIcon, string>(nameof(HelpAnchor));

    /// <summary>Guide section anchor this icon deep-links to (Phase 6 guide).</summary>
    public string HelpAnchor
    {
        get => GetValue(HelpAnchorProperty);
        set => SetValue(HelpAnchorProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HelpTextProperty || change.Property == HelpAnchorProperty)
        {
            ToolTip.SetTip(this, HelpText);
            AutomationProperties.SetName(this, $"Help: {(string.IsNullOrEmpty(HelpAnchor) ? "general" : HelpAnchor)}");
        }
    }
}