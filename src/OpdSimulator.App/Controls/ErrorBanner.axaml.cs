using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpdSimulator.App.Controls;

/// <summary>How severe a banner message is; drives the accent colours (5d.4, D-115).</summary>
public enum BannerSeverity
{
    /// <summary>The run failed or was refused — red accents.</summary>
    Error,

    /// <summary>The configuration is workable but compromised — amber accents.</summary>
    Warning,

    /// <summary>Neutral guidance — muted accents.</summary>
    Info,
}

/// <summary>
/// FR-UI-9 inline banner. Shows a summary strip with an icon and a dismiss
/// action; hiding is driven by setting <see cref="IsVisible"/> or by the user
/// dismissing — both must keep the banner's accessibility name in sync.
/// Since Phase 5d (D-115) the banner supports Error / Warning / Info severities
/// so the amber stage-mismatch feedback reuses the same control.
/// </summary>
public partial class ErrorBanner : UserControl
{
    public ErrorBanner()
    {
        InitializeComponent();
        IsVisible = false;
    }

    /// <summary>The user-facing summary shown in the banner.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ErrorBanner, string?>(nameof(Message));

    /// <summary>The user-facing message. Empty/null hides the banner.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Accent severity; defaults to <see cref="BannerSeverity.Error"/>.</summary>
    public static readonly StyledProperty<BannerSeverity> SeverityProperty =
        AvaloniaProperty.Register<ErrorBanner, BannerSeverity>(nameof(Severity), BannerSeverity.Error);

    /// <summary>How the banner is framed and voiced (Error / Warning / Info).</summary>
    public BannerSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MessageProperty)
        {
            UpdateVisibility();
        }
        else if (change.Property == SeverityProperty)
        {
            ApplySeverity();
        }
    }

    private void UpdateVisibility()
    {
        // Hide the whole control, not just its chrome, so IsVisible mirrors
        // Message for data binding and layout (FR-UI-9).
        bool show = !string.IsNullOrWhiteSpace(Message);
        Root.IsVisible = show;
        IsVisible = show;
        SummaryText.Text = Message ?? string.Empty;
        ApplySeverity();
        AutomationProperties.SetName(Root, show ? $"{Severity}: {Message}" : "No banner");
    }

    private void ApplySeverity()
    {
        // Warning reuses the theme's amber tokens (ColorWarning #8A5300 /
        // ColorWarningBackground #FFF4E0, D-115); Info stays muted. The theme
        // always defines these brushes; the literal fallbacks below are an
        // unreachable safety net for nullable-analysis.
        switch (Severity)
        {
            case BannerSeverity.Warning:
                Root.Background = BrushOf("BrushWarningBackground", 0xFF, 0xF4, 0xE0);
                Root.BorderBrush = BrushOf("BrushWarning", 0x8A, 0x53, 0x00);
                AccentGlyph.Foreground = BrushOf("BrushWarning", 0x8A, 0x53, 0x00);
                SummaryText.Foreground = BrushOf("BrushWarning", 0x8A, 0x53, 0x00);
                break;
            case BannerSeverity.Info:
                Root.Background = BrushOf("BrushBackgroundAlt", 0xEE, 0xEE, 0xEE);
                Root.BorderBrush = BrushOf("BrushBorderDefault", 0xC0, 0xC0, 0xC0);
                AccentGlyph.Foreground = BrushOf("BrushTextSecondary", 0x60, 0x60, 0x60);
                SummaryText.Foreground = BrushOf("BrushTextPrimary", 0x1E, 0x1E, 0x1E);
                break;
            default:
                Root.Background = BrushOf("BrushErrorBackground", 0xFC, 0xEA, 0xE8);
                Root.BorderBrush = BrushOf("BrushError", 0xB3, 0x26, 0x1E);
                AccentGlyph.Foreground = BrushOf("BrushError", 0xB3, 0x26, 0x1E);
                SummaryText.Foreground = BrushOf("BrushErrorDark", 0x7A, 0x17, 0x12);
                break;
        }
    }

    private IBrush BrushOf(string key, byte r, byte g, byte b)
        => this.TryFindResource(key, out object? value) && value is IBrush brush
            ? brush
            : new SolidColorBrush(Color.FromRgb(r, g, b));

    private void OnDismissClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Message = null;
        UpdateVisibility();
    }
}