using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A labelled dropdown built on Avalonia's standard <see cref="ComboBox"/>
/// (Phase 8X). Keeps the shared label / help-icon / tooltip treatment while
/// leaving open, move, commit and close behaviour to the prebuilt control.
/// </summary>
public partial class DropdownField : UserControl
{
    public DropdownField()
    {
        InitializeComponent();
    }

    /// <summary>Persistent visible label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<DropdownField, string?>(nameof(Label));

    /// <summary>Persistent visible label.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Format-example placeholder text.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<DropdownField, string?>(nameof(Placeholder));

    /// <summary>Format-example placeholder text.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Short hover tooltip for the field.</summary>
    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<DropdownField, string?>(nameof(Tooltip));

    /// <summary>Short hover tooltip for the field.</summary>
    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    /// <summary>
    /// The selectable items. Typed as <see cref="IEnumerable"/> because the
    /// same control serves string lists and enum lists (distribution families);
    /// the ComboBox renders each item through its own <c>ToString</c>.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsProperty =
        AvaloniaProperty.Register<DropdownField, IEnumerable?>(nameof(Items));

    /// <summary>The selectable items.</summary>
    public IEnumerable? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The committed selection (two-way).</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<DropdownField, object?>(
            nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The committed selection (two-way).</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Guide anchor for the help icon.</summary>
    public static readonly StyledProperty<string> HelpAnchorProperty =
        AvaloniaProperty.Register<DropdownField, string>(nameof(HelpAnchor));

    /// <summary>Guide anchor for the help icon.</summary>
    public string HelpAnchor
    {
        get => GetValue(HelpAnchorProperty);
        set => SetValue(HelpAnchorProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(Combo, Label ?? string.Empty);
        }
        else if (change.Property == TooltipProperty)
        {
            AutomationProperties.SetHelpText(Combo, Tooltip ?? string.Empty);
        }
    }
}
