using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace OpdSimulator.App.Controls;

/// <summary>Outcome of a closed <see cref="ThemedDialog"/>.</summary>
public enum DialogResult
{
    /// <summary>The affirmative (primary) action was chosen.</summary>
    Confirm,

    /// <summary>The dialog was cancelled (Cancel / Escape / window-close).</summary>
    Cancel,
}

/// <summary>
/// Themed modal dialog for confirmations and errors (FR-UI-10). Never uses an
/// OS-native dialog: Enter confirms, Escape cancels, and focus is restored to
/// the opener (AGENTS §16.7). The wrong-variant styling (icon + accent colour)
/// comes from Theme.axaml resources, switched in code-behind.
/// </summary>
public partial class ThemedDialog : Window
{
    private readonly TaskCompletionSource<DialogResult> _resultSource = new();
    private Control? _lastFocused;

    /// <summary>
/// Parameterless constructor for the XAML runtime loader; dialogs are always
/// created through the parameterised constructor in practice.
/// </summary>
public ThemedDialog() : this(string.Empty, string.Empty)
{
}

/// <summary>Creates the dialog.</summary>
    /// <param name="title">Title bar text.</param>
    /// <param name="message">Body text.</param>
    /// <param name="isError">When true, renders the error variant (red accent + glyph).</param>
    /// <param name="confirmText">Label of the primary button (default "OK").</param>
    /// <param name="cancelText">Label of the secondary button; null hides it.</param>
    /// <param name="showInput">Whether an input field is shown (prompt dialog).</param>
    /// <param name="inputText">Initial input text.</param>
    public ThemedDialog(string title, string message, bool isError = false,
        string confirmText = "OK", string? cancelText = "Cancel",
        bool showInput = false, string? inputText = null)
    {
        InitializeComponent();

        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (cancelText is null)
        {
            CancelButton.IsVisible = false;
        }
        else
        {
            CancelButton.Content = cancelText;
        }

        if (showInput)
        {
            InputTextBox.IsVisible = true;
            InputTextBox.Text = inputText ?? string.Empty;
        }

        ApplyVariant(isError);
    }

    /// <summary>Gets the input text when this is a prompt dialog.</summary>
    public string? InputText => InputTextBox.Text;

    /// <summary>Gets the chosen outcome after the dialog closes.</summary>
    public DialogResult Result { get; private set; } = DialogResult.Cancel;

    /// <summary>
    /// Shows a dialog with a text input (preset names, etc.) and awaits both
    /// the outcome and the entered text.
    /// </summary>
    /// <returns>The outcome and the input value (null when cancelled).</returns>
    public static async Task<(DialogResult Result, string? Value)> ShowPromptAsync(
        Window? owner, string title, string message,
        string? initialValue = null, string confirmText = "Save", string? cancelText = "Cancel")
    {
        var dialog = new ThemedDialog(title, message, isError: false,
            confirmText: confirmText, cancelText: cancelText, showInput: true, inputText: initialValue);
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }

        return (dialog.Result, dialog.Result == DialogResult.Confirm ? dialog.InputText : null);
    }

    /// <summary>
    /// Shows the dialog modally over the given owner and awaits its outcome.
    /// </summary>
    public static async Task<DialogResult> ShowAsync(Window? owner, string title, string message,
        bool isError = false, string confirmText = "OK", string? cancelText = "Cancel")
    {
        var dialog = new ThemedDialog(title, message, isError, confirmText, cancelText);
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

    private void ApplyVariant(bool isError)
    {
        if (isError)
        {
            Glyph.Text = "\uEA39"; // error circle
            IconBadge.Background = ResolveBrush("BrushToastErrorBackground");
            Glyph.Foreground = ResolveBrush("BrushToastErrorText");
        }
        else
        {
            Glyph.Text = "\uE73E"; // check circle
            IconBadge.Background = ResolveBrush("BrushAccentInfo");
            Glyph.Foreground = ResolveBrush("BrushTextOnBrand");
        }
    }

    private IBrush? ResolveBrush(string resourceKey)
        => this.TryFindResource(resourceKey, out var value) ? value as IBrush : null;

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => CloseWith(DialogResult.Confirm);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => CloseWith(DialogResult.Cancel);

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Remember who opened us so we can hand focus back on close (§16.7).
        _lastFocused = FocusManager?.GetFocusedElement() as Control;

        // A prompt dialog lands the caret in its input; a confirmation lands
        // on the primary action.
        if (InputTextBox.IsVisible)
        {
            Dispatcher.UIThread.Post(() =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            });
        }
        else if (ConfirmButton.IsVisible)
        {
            Dispatcher.UIThread.Post(() => ConfirmButton.Focus());
        }
        else if (CancelButton.IsVisible)
        {
            Dispatcher.UIThread.Post(() => CancelButton.Focus());
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            CloseWith(DialogResult.Cancel);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CloseWith(DialogResult.Confirm);
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _resultSource.TrySetResult(Result);

        // Restore focus to the opener only if it is still live in this tree.
        if (_lastFocused?.IsAttachedToVisualTree() == true)
        {
            _lastFocused.Focus();
        }
    }

    private void CloseWith(DialogResult result)
    {
        Result = result;
        Close();
    }
}