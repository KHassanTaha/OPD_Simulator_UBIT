using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace OpdSimulator.App.Controls;

/// <summary>
/// FR-UI-17 validated input: persistent label, format-example watermark, unit,
/// tooltip, help icon, and an inline error zone. The control raises
/// <see cref="FieldLostFocus"/> so the owning view model validates on blur only;
/// it presents whatever <see cref="HasError"/>/<see cref="ErrorMessage"/> the
/// model gives it and clears the error instantly when <see cref="HasError"/>
/// becomes false.
/// </summary>
public partial class ValidatedField : UserControl
{
    public ValidatedField()
    {
        InitializeComponent();
    }

    /// <summary>Fired when the input loses focus (validation trigger).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FieldLostFocusEvent =
        RoutedEvent.Register<ValidatedField, RoutedEventArgs>(nameof(FieldLostFocus), RoutingStrategies.Bubble);

    /// <summary>Fired when the input loses focus.</summary>
    public event EventHandler<RoutedEventArgs>? FieldLostFocus
    {
        add => AddHandler(FieldLostFocusEvent, value);
        remove => RemoveHandler(FieldLostFocusEvent, value);
    }

    /// <summary>Persistent visible label (never just a placeholder).</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(Label));

    /// <summary>Persistent visible label.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Placeholder text that shows the expected format, e.g. "0.5".</summary>
    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(Watermark));

    /// <summary>Placeholder text that shows the expected format.</summary>
    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    /// <summary>Hover tooltip explaining what the value means.</summary>
    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(Tooltip));

    /// <summary>Hover tooltip explaining what the value means.</summary>
    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    /// <summary>Unit shown as a suffix inside the field, e.g. "/ min".</summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(Unit));

    /// <summary>Unit shown as a suffix inside the field.</summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Unit repeated in the label row for screenshots (hidden by default).</summary>
    public static readonly StyledProperty<string?> UnitLabelProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(UnitLabel));

    /// <summary>Unit shown next to the label.</summary>
    public string? UnitLabel
    {
        get => GetValue(UnitLabelProperty);
        set => SetValue(UnitLabelProperty, value);
    }

    /// <summary>The field value (two-way).</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The field value (two-way).</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>True when the value is invalid (drives the red state).</summary>
    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ValidatedField, bool>(nameof(HasError));

    /// <summary>True when the value is invalid.</summary>
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    /// <summary>Cause + remedy message shown under the field when invalid.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(ErrorMessage));

    /// <summary>Cause + remedy message shown under the field when invalid.</summary>
    public string? ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>Guide anchor for the help icon (Phase 6 deep-link).</summary>
    public static readonly StyledProperty<string> HelpAnchorProperty =
        AvaloniaProperty.Register<ValidatedField, string>(nameof(HelpAnchor));

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

        if (change.Property == HasErrorProperty || change.Property == ErrorMessageProperty)
        {
            UpdateErrorState();
        }

        if (change.Property == LabelProperty && InputBox is not null)
        {
            AutomationProperties.SetName(InputBox, Label ?? string.Empty);
        }
    }

    private void UpdateErrorState()
    {
        bool hasError = HasError && !string.IsNullOrWhiteSpace(ErrorMessage);
        var resources = Application.Current!.Resources;
        if (resources.TryGetResource("BrushError", null, out var errorBrush)
            && resources.TryGetResource("BrushBorderDefault", null, out var defaultBrush))
        {
            FieldBorder.BorderBrush = hasError ? (IBrush)errorBrush! : (IBrush)defaultBrush!;
        }

        FieldBorder.BorderThickness = hasError ? new Thickness(2) : new Thickness(1);
        ErrorRow.IsVisible = hasError;
        ErrorText.Text = ErrorMessage ?? string.Empty;
        AutomationProperties.SetName(this, hasError ? $"{Label}: error — {ErrorMessage}" : $"{Label}: ok");
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e)
        => RaiseEvent(new RoutedEventArgs(FieldLostFocusEvent));
}