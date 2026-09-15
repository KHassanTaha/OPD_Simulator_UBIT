using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace OpdSimulator.App.Controls;

/// <summary>
/// FR-UI-9 inline error banner. Shows a red summary strip with an icon and a
/// dismiss action; hiding is driven by setting <see cref="IsVisible"/> or by
/// the user dismissing — both must keep the banner's accessibility name in sync.
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

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MessageProperty)
        {
            UpdateVisibility();
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
        AutomationProperties.SetName(Root, show ? $"Error: {Message}" : "No error");
    }

    private void OnDismissClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Message = null;
        UpdateVisibility();
    }
}