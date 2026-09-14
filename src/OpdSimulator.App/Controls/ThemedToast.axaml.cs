using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Controls;

/// <summary>
/// One themed notification card, driven by a <see cref="ToastItem"/>'s
/// message and kind (FR-UI-10). The host control is responsible for
/// stacking and auto-dismissing toasts; this control renders a single one.
/// </summary>
public partial class ThemedToast : UserControl
{
    /// <summary>Raises when the user closes the toast explicitly.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Identifies the <see cref="Message"/> styled property.</summary>
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ThemedToast, string>(nameof(Message));

    /// <summary>Identifies the <see cref="ToastKind"/> styled property.</summary>
    public static readonly StyledProperty<ToastKind> ToastKindProperty =
        AvaloniaProperty.Register<ThemedToast, ToastKind>(nameof(ToastKind), defaultValue: ToastKind.Info);

    /// <summary>Gets or sets the message text.</summary>
    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Gets or sets which theme variant (colour + glyph) to render.</summary>
    public ToastKind ToastKind
    {
        get => GetValue(ToastKindProperty);
        set => SetValue(ToastKindProperty, value);
    }

    /// <summary>Creates the toast card.</summary>
    public ThemedToast()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MessageProperty)
        {
            if (MessageText is not null)
            {
                MessageText.Text = Message;
            }
        }
        else if (change.Property == ToastKindProperty)
        {
            if (Card is not null)
            {
                ApplyKind();
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyKind();
    }

    private void ApplyKind()
    {
        var kindClass = ToastKind switch
        {
            ToastKind.Success => "toastSuccess",
            ToastKind.Error => "toastError",
            _ => "toastInfo",
        };

        // Toggle exactly one kind class so conflicting card styles never
        // stack; the message colour follows the same class selector.
        Card.Classes.Remove("toastSuccess");
        Card.Classes.Remove("toastError");
        Card.Classes.Remove("toastInfo");
        Card.Classes.Add(kindClass);
        MessageText.Classes.Add("toastMessage");
        MessageText.Text = Message;

        Glyph.Text = ToastKind switch
        {
            ToastKind.Success => "\uE73E", // check circle
            ToastKind.Error => "\uEA39",   // error circle
            _ => "\uE946",                 // info circle
        };
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}