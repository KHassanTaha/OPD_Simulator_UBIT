using System;
using System.Threading.Tasks;
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
}