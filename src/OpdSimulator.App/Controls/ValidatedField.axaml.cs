using Avalonia;
using Avalonia.Controls;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Wraps any single input into a labelled, validated unit (FR-UI-17): a
/// persistent label, an optional '?' link to the in-program guide, a themed
/// field border, and an inline actionable error message. The wrapped input is
/// the control's <see cref="Content"/>. Error state is switched purely by
/// <see cref="HasError"/>; the message states the cause AND the remedy.
/// </summary>
public partial class ValidatedField : UserControl
{
    /// <summary>Identifies the <see cref="Label"/> styled property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ValidatedField, string>(nameof(Label));

    /// <summary>Identifies the <see cref="HasError"/> styled property.</summary>
    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<ValidatedField, bool>(nameof(HasError));

    /// <summary>Identifies the <see cref="ErrorMessage"/> styled property.</summary>
    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<ValidatedField, string>(nameof(ErrorMessage), "Invalid value");

    /// <summary>Identifies the <see cref="ShowHelp"/> styled property.</summary>
    public static readonly StyledProperty<bool> ShowHelpProperty =
        AvaloniaProperty.Register<ValidatedField, bool>(nameof(ShowHelp), true);

    /// <summary>Identifies the <see cref="HelpAnchor"/> styled property.</summary>
    public static readonly StyledProperty<string?> HelpAnchorProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(HelpAnchor));

    /// <summary>Identifies the <see cref="HelpTip"/> styled property.</summary>
    public static readonly StyledProperty<string?> HelpTipProperty =
        AvaloniaProperty.Register<ValidatedField, string?>(nameof(HelpTip));

    /// <summary>Gets or sets the persistent visible label.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Gets or sets whether the field is currently invalid.</summary>
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    /// <summary>Gets or sets the inline error text shown below the field.</summary>
    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>Gets or sets whether the '?' help badge is visible.</summary>
    public bool ShowHelp
    {
        get => GetValue(ShowHelpProperty);
        set => SetValue(ShowHelpProperty, value);
    }

    /// <summary>Gets or sets the guide section anchor opened by the '?' badge.</summary>
    public string? HelpAnchor
    {
        get => GetValue(HelpAnchorProperty);
        set => SetValue(HelpAnchorProperty, value);
    }

    /// <summary>Gets or sets the tooltip text on the '?' badge.</summary>
    public string? HelpTip
    {
        get => GetValue(HelpTipProperty);
        set => SetValue(HelpTipProperty, value);
    }

    /// <summary>Creates the field wrapper.</summary>
    public ValidatedField()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == HasErrorProperty && FieldBorder is not null)
        {
            ApplyErrorState();
        }
        else if (change.Property == ErrorMessageProperty && FieldBorder is not null)
        {
            ErrorText.Text = ErrorMessage;
            ApplyErrorState();
        }
    }

    private void ApplyErrorState()
    {
        FieldBorder.Classes.Remove("fieldError");
        if (HasError)
        {
            FieldBorder.Classes.Add("fieldError");
        }

        ErrorRow.IsVisible = HasError;
        ErrorText.Text = ErrorMessage;
    }
}