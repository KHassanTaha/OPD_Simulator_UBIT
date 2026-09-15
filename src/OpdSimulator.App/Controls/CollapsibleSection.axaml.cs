using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace OpdSimulator.App.Controls;

/// <summary>
/// A titled, chevron-toggled collapsible section (AGENTS §16.2). Content is
/// hidden when collapsed; the header stays keyboard-reachable. State is exposed
/// two-way via <see cref="IsExpanded"/> so a view model or preset can persist it.
/// Optional sections (<see cref="IsOptional"/>) show an enable toggle that
/// disables and dims every descendant field while off (FR-UI-7 explains why via
/// an injected tooltip). The toggle lives in the view model
/// (<see cref="IsEnabledToggle"/>, two-way) so "Clear All" can reset it.
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

    /// <summary>
    /// True when this section's fields are entirely optional. The section then
    /// shows an enable toggle in its header and its fields are disabled and
    /// dimmed while the toggle is off.
    /// </summary>
    public static readonly StyledProperty<bool> IsOptionalProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(nameof(IsOptional));

    /// <summary>True when this section's fields are entirely optional.</summary>
    public bool IsOptional
    {
        get => GetValue(IsOptionalProperty);
        set => SetValue(IsOptionalProperty, value);
    }

    /// <summary>
    /// Two-way enable state of an optional section, persisted in the view model
    /// (not the control) so "Clear All" can reset it. Ignored when
    /// <see cref="IsOptional"/> is false.
    /// </summary>
    public static readonly StyledProperty<bool> IsEnabledToggleProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(
            nameof(IsEnabledToggle),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Two-way enable state of an optional section.</summary>
    public bool IsEnabledToggle
    {
        get => GetValue(IsEnabledToggleProperty);
        set => SetValue(IsEnabledToggleProperty, value);
    }

    private ToggleButton? _headerToggle;
    private RotateTransform? _chevronTransform;
    private ContentPresenter? _body;
    private readonly HashSet<Control> _injectedTooltips = new();

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _headerToggle = e.NameScope.Find("PART_HeaderToggle") as ToggleButton;
        _chevronTransform = _headerToggle?.RenderTransform as RotateTransform;
        _body = e.NameScope.Find("PART_Body") as ContentPresenter;

        if (_headerToggle is not null)
        {
            _headerToggle.IsCheckedChanged += OnHeaderToggleChanged;
            _headerToggle.IsChecked = IsExpanded;
        }

        SetChevron(IsExpanded);

        // First layout pass realises the templated content; refresh the
        // disabled/dimmed state once children exist (tooltips need the tree).
        LayoutUpdated += OnFirstLayoutUpdate;
    }

    private void OnFirstLayoutUpdate(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnFirstLayoutUpdate;
        UpdateSectionState();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsOptionalProperty
            || change.Property == IsEnabledToggleProperty
            || change.Property == TitleProperty)
        {
            UpdateSectionState();
        }
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

    /// <summary>Whether an optional section's fields must be disabled.</summary>
    private bool ShouldDisableBody() => IsOptional && !IsEnabledToggle;

    private void UpdateSectionState()
    {
        if (_body is null)
        {
            return;
        }

        bool disabled = ShouldDisableBody();
        _body.IsEnabled = !disabled;
        _body.Opacity = disabled ? 0.5 : 1.0;

        if (disabled)
        {
            ApplyDisabledTooltips();
        }
        else
        {
            RestoreTooltips();
        }
    }

    /// <summary>
    /// Every field inside a disabled optional section explains why it is
    /// disabled (FR-UI-7): "Enable '…' above to edit this field." Only where
    /// no tooltip already exists — a hand-authored tooltip wins.
    /// </summary>
    private void ApplyDisabledTooltips()
    {
        if (_injectedTooltips.Count > 0)
        {
            return; // already injected while disabled
        }

        string tip = $"Enable \"{Title}\" above to edit this field.";
        foreach (var field in DescendantFields(_body!))
        {
            if (ToolTip.GetTip(field) is null)
            {
                ToolTip.SetTip(field, tip);
                _injectedTooltips.Add(field);
            }
        }
    }

    private void RestoreTooltips()
    {
        foreach (var field in _injectedTooltips)
        {
            ToolTip.SetTip(field, null);
        }

        _injectedTooltips.Clear();
    }

    /// <summary>The user-editable fields (or field wrappers) inside a section.</summary>
    private static IEnumerable<Control> DescendantFields(Visual root)
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is Control control && IsField(control))
            {
                yield return control;
            }

            foreach (var sub in DescendantFields(child))
            {
                yield return sub;
            }
        }
    }

    /// <summary>
    /// A control is a "field" for FR-UI-7 if it is one of the app's field
    /// wrappers (ValidatedField / SearchableDropdown — the tooltip then shows
    /// anywhere in the field) or a focusable input control.
    /// </summary>
    private static bool IsField(Control control) =>
        control is ValidatedField or SearchableDropdown
        || control is TextBox or ComboBox or ListBox or DatePicker
            or CheckBox or ToggleSwitch or RadioButton or Button;
}