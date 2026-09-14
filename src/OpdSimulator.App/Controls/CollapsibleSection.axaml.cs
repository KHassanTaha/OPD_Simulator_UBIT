using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A section header with a chevron that collapses its body (FR-UI-12).
/// <see cref="IsExpanded"/> is two-way bindable so a view model can persist
/// the collapsed state (wire <see cref="SessionKey"/> to the config view
/// model in sub-block D).
/// </summary>
public class CollapsibleSection : ContentControl
{
    private ContentPresenter? _bodyPresenter;
    private TextBlock? _chevron;

    /// <summary>Identifies the <see cref="IsExpanded"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(nameof(IsExpanded), defaultValue: true);

    /// <summary>Identifies the <see cref="Header"/> styled property.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<CollapsibleSection, string>(nameof(Header), defaultValue: "Section");

    /// <summary>Identifies the <see cref="SessionKey"/> styled property.</summary>
    public static readonly StyledProperty<string?> SessionKeyProperty =
        AvaloniaProperty.Register<CollapsibleSection, string?>(nameof(SessionKey));

    /// <summary>Gets or sets whether the body is expanded (two-way).</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Gets or sets the header text.</summary>
    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the persistence key (consumed by sub-block D to remember
    /// collapsed sections across configurations).
    /// </summary>
    public string? SessionKey
    {
        get => GetValue(SessionKeyProperty);
        set => SetValue(SessionKeyProperty, value);
    }

    /// <summary>Creates the collapsible section.</summary>
    public CollapsibleSection()
    {
        // The chrome is provided by the type-keyed ControlTheme in
        // ControlStyles.axaml, so no XAML body is needed here.
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _bodyPresenter = e.NameScope.Find<ContentPresenter>("PART_BodyPresenter");
        _chevron = e.NameScope.Find<TextBlock>("PART_Chevron");

        UpdateVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        // Collapsing is implemented by removing the body from layout, which
        // gives the "chevron" behaviour without hidden space or animation.
        if (_bodyPresenter is not null)
        {
            _bodyPresenter.IsVisible = IsExpanded;
        }

        if (_chevron is not null)
        {
            // E70D (chevron up) when expanded, E70E (chevron down) when collapsed.
            _chevron.Text = IsExpanded ? "\uE70D" : "\uE70E";
        }
    }
}