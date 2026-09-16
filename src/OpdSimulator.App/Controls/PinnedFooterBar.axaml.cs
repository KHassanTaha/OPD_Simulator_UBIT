using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace OpdSimulator.App.Controls;

/// <summary>
/// AGENTS §16.2 "Pinned primary action": a footer bar that stays visible with
/// the primary action (e.g. "Start Calculation"). The left slot is content for
/// caller-hosted actions; the right button is primary-themed.
/// </summary>
public partial class PinnedFooterBar : ContentControl
{
    public PinnedFooterBar()
    {
        InitializeComponent();
    }

    /// <summary>Text shown on the primary button.</summary>
    public static readonly StyledProperty<string?> PrimaryTextProperty =
        AvaloniaProperty.Register<PinnedFooterBar, string?>(nameof(PrimaryText));

    /// <summary>Text shown on the primary button.</summary>
    public string? PrimaryText
    {
        get => GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    /// <summary>Command invoked when the primary action is activated.</summary>
    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<PinnedFooterBar, ICommand?>(nameof(PrimaryCommand));

    /// <summary>Command invoked when the primary action is activated.</summary>
    public ICommand? PrimaryCommand
    {
        get => GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    /// <summary>Whether the primary button is enabled (dimmed + tooltip when disabled).</summary>
    public static readonly StyledProperty<bool> PrimaryIsEnabledProperty =
        AvaloniaProperty.Register<PinnedFooterBar, bool>(nameof(PrimaryIsEnabled), true);

    /// <summary>Whether the primary button is enabled.</summary>
    public bool PrimaryIsEnabled
    {
        get => GetValue(PrimaryIsEnabledProperty);
        set => SetValue(PrimaryIsEnabledProperty, value);
    }
}