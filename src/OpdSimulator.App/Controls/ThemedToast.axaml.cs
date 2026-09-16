using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Renders one toast from a <see cref="ToastItem"/> using shared theme visuals.
/// Severity drives the accent bar, glyph, label and colours (AGENTS §16.2).
/// </summary>
public partial class ThemedToast : UserControl
{
    private static readonly IBrush[] AccentsForSeverity =
    {
        new SolidColorBrush(Color.Parse("#0B7285")),  // info
        new SolidColorBrush(Color.Parse("#145C39")),  // success
        new SolidColorBrush(Color.Parse("#8A5300")),  // warning
        new SolidColorBrush(Color.Parse("#B3261E")),  // error
    };

    public ThemedToast()
    {
        InitializeComponent();
    }

    /// <summary>The toast to display.</summary>
    public static readonly StyledProperty<ToastItem?> ItemProperty =
        AvaloniaProperty.Register<ThemedToast, ToastItem?>(nameof(Item));

    /// <summary>The toast to display.</summary>
    public ToastItem? Item
    {
        get => GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>Raised when the user dismisses the toast.</summary>
    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ThemedToast, ICommand?>(nameof(CloseCommand));

    /// <summary>Command executed when the toast is dismissed.</summary>
    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ItemProperty)
        {
            ApplyItem();
        }
    }

    private void ApplyItem()
    {
        var item = Item;
        if (item is null)
        {
            return;
        }

        int severityIndex = item.Severity.ToLowerInvariant() switch
        {
            "success" => 1,
            "warning" => 2,
            "error" => 3,
            _ => 0,
        };

        AccentBar.Fill = AccentsForSeverity[severityIndex];
        SeverityGlyph.Text = severityIndex switch
        {
            1 => "\u2713",
            2 => "\u26A0",
            3 => "\u2716",
            _ => "\u2139",
        };
        SeverityLabel.Text = severityIndex switch
        {
            1 => "Success",
            2 => "Warning",
            3 => "Error",
            _ => "Info",
        };
        MessageText.Text = item.Message;

        AutomationProperties.SetName(this, $"Toast {SeverityLabel.Text}: {item.Message}");
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // CloseCommand receives the toast itself as the parameter so an owning
        // view model can remove exactly this item from its collection.
        if (CloseCommand is not null && CloseCommand.CanExecute(Item))
        {
            CloseCommand.Execute(Item);
        }
    }
}