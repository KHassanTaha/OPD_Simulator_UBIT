using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A titled, chevron-toggled collapsible section (AGENTS §16.2). Content is
/// hidden when collapsed; the header stays keyboard-reachable. State is exposed
/// two-way via <see cref="IsExpanded"/> so a view model or preset can persist it.
/// </summary>
public partial class CollapsibleSection : ContentControl
{
    public CollapsibleSection()
    {
        InitializeComponent();
    }

    /// <summary>The section heading text.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CollapsibleSection, string?>(nameof(Title));

    /// <summary>The section heading text.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Whether the content area is visible, two-way updateable.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(
            nameof(IsExpanded),
            true,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the content area is visible.</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Short hover tooltip for the header (empty = icon hidden).</summary>
    public static readonly StyledProperty<string?> HelpTextProperty =
        AvaloniaProperty.Register<CollapsibleSection, string?>(nameof(HelpText));

    /// <summary>Short hover tooltip for the header.</summary>
    public string? HelpText
    {
        get => GetValue(HelpTextProperty);
        set => SetValue(HelpTextProperty, value);
    }

    private ToggleButton? _headerToggle;
    private RotateTransform? _chevronTransform;

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _headerToggle = e.NameScope.Find("PART_HeaderToggle") as ToggleButton;
        _chevronTransform = _headerToggle?.RenderTransform as RotateTransform;

        if (_headerToggle is not null)
        {
            _headerToggle.IsCheckedChanged += OnHeaderToggleChanged;
            _headerToggle.IsChecked = IsExpanded;
        }

        SetChevron(IsExpanded);
    }

    private void OnHeaderToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool expanded = _headerToggle?.IsChecked == true;
        // A toggle by the header must flow back to binding sources.
        if (expanded != IsExpanded)
        {
            IsExpanded = expanded;
        }

        SetChevron(expanded);
    }

    private void SetChevron(bool expanded)
    {
        if (_chevronTransform is not null)
        {
            _chevronTransform.Angle = expanded ? 90 : 0;
        }
    }
}