using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OpdSimulator.App.Controls;

/// <summary>Which button closed a <see cref="ThemedDialog"/>.</summary>
public enum ThemedDialogResult
{
    /// <summary>The primary (confirm) action was chosen.</summary>
    Primary,

    /// <summary>The secondary (cancel/dismiss) action was chosen.</summary>
    Secondary,

    /// <summary>The dialog was dismissed (Escape / window close).</summary>
    Cancel,
}

/// <summary>
/// The app-wide themed confirmation/alert dialog (AGENTS §16.2 — never the
/// OS-native dialog). Modal (focus-trapping), Escape = Cancel, and focus returns
/// to the owner when the dialog closes.
/// </summary>
public partial class ThemedDialog : Window
{
    public ThemedDialog()
    {
        InitializeComponent();
    }

    /// <summary>The result chosen by the user, or Cancel if dismissed.</summary>
    public ThemedDialogResult Result { get; private set; } = ThemedDialogResult.Cancel;

    /// <summary>
    /// Shows a themed modal dialog over <paramref name="owner"/> and returns the
    /// button the user chose. Passing <paramref name="secondary"/> as null
    /// replaces the secondary button with a simple "Close".
    /// </summary>
    public static async Task<ThemedDialogResult> ShowMessageAsync(
        Window? owner,
        string title,
        string message,
        string primary = "OK",
        string? secondary = null)
    {
        var dialog = new ThemedDialog
        {
            Title = title,
            Message = message,
        };
        dialog.PrimaryButton.Content = primary;
        dialog.SecondaryButton.Content = secondary ?? "Close";

        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }

        return dialog.Result;
    }

    /// <summary>
    /// Optional rich body shown under the message (Phase 8M, D-164). The
    /// calculations dialog uses it for its sections; the message-only callers
    /// leave it null and see exactly the dialog they saw before.
    /// </summary>
    public object? Body
    {
        get => DialogBodyHost.Content;
        set
        {
            DialogBodyHost.Content = value;
            DialogBodyHost.IsVisible = value is not null;
            HasBody = value is not null;
        }
    }

    /// <summary>Backing property so the XAML can bind the body's visibility.</summary>
    public static readonly StyledProperty<bool> HasBodyProperty =
        AvaloniaProperty.Register<ThemedDialog, bool>(nameof(HasBody));

    /// <summary>True when <see cref="Body"/> carries content.</summary>
    public bool HasBody
    {
        get => GetValue(HasBodyProperty);
        private set => SetValue(HasBodyProperty, value);
    }

    /// <summary>
    /// Text placed on the clipboard by the Copy button. Setting it shows the
    /// button; leaving it null (the default) hides it.
    /// </summary>
    public string? CopyText
    {
        get => _copyText;
        set
        {
            _copyText = value;
            HasCopyText = !string.IsNullOrEmpty(value);
            CopyButton.IsVisible = HasCopyText;
        }
    }

    private string? _copyText;

    /// <summary>Backing property so the XAML can bind the Copy button's visibility.</summary>
    public static readonly StyledProperty<bool> HasCopyTextProperty =
        AvaloniaProperty.Register<ThemedDialog, bool>(nameof(HasCopyText));

    /// <summary>True when the Copy button should be visible.</summary>
    public bool HasCopyText
    {
        get => GetValue(HasCopyTextProperty);
        private set => SetValue(HasCopyTextProperty, value);
    }

    /// <summary>Raised after a successful copy, so the view can confirm it.</summary>
    public event EventHandler? Copied;

    /// <summary>The message body (set before showing).</summary>
    public string Message
    {
        get => DialogMessage.Text ?? string.Empty;
        set => DialogMessage.Text = value;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PrimaryButton.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Escape anywhere in a themed dialog means "cancel".
        if (e.Key == Key.Escape)
        {
            Result = ThemedDialogResult.Cancel;
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        Result = ThemedDialogResult.Primary;
        Close();
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        Result = ThemedDialogResult.Secondary;
        Close();
    }

    /// <summary>
    /// Copies <see cref="CopyText"/> to the clipboard. Clipboard access can be
    /// refused by the platform (no display, denied permission), so the failure
    /// is logged and shown rather than swallowed — §12.4 forbids a silent catch.
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CopyText))
        {
            return;
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(this) as Avalonia.Input.Platform.IClipboard;
            if (clipboard is null)
            {
                Serilog.Log.Warning("Clipboard unavailable; copy skipped");
                return;
            }

            await clipboard.SetTextAsync(CopyText);
            Serilog.Log.Information("Calculations text copied to clipboard ({Length} chars)", CopyText.Length);
            Copied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Copying the calculations text to the clipboard failed");
        }
    }
}