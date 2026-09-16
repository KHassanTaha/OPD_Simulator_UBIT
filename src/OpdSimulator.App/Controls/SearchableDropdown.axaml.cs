using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Searchable dropdown (FR-UI-6): type-to-filter over <see cref="Items"/>,
/// × clear, and full keyboard navigation (Down opens, Up/Down move, Enter
/// commits, Escape closes). Selection is committed only on Enter, click or clear,
/// so previewing items never mutates the bound value.
/// </summary>
public partial class SearchableDropdown : UserControl
{
    private readonly ObservableCollection<string> _filtered = new();
    private bool _filteringFromProgrammaticSet;

    public SearchableDropdown()
    {
        InitializeComponent();
    }

    /// <summary>Persistent visible label.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SearchableDropdown, string?>(nameof(Label));

    /// <summary>Persistent visible label.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Format-example placeholder text.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SearchableDropdown, string?>(nameof(Placeholder));

    /// <summary>Format-example placeholder text.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Short hover tooltip for the field.</summary>
    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<SearchableDropdown, string?>(nameof(Tooltip));

    /// <summary>Short hover tooltip for the field.</summary>
    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    /// <summary>The selectable items.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> ItemsProperty =
        AvaloniaProperty.Register<SearchableDropdown, IEnumerable<string>?>(nameof(Items));

    /// <summary>The selectable items.</summary>
    public IEnumerable<string>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>The committed selection (two-way).</summary>
    public static readonly StyledProperty<string?> SelectedItemProperty =
        AvaloniaProperty.Register<SearchableDropdown, string?>(
            nameof(SelectedItem),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The committed selection (two-way).</summary>
    public string? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Guide anchor for the help icon.</summary>
    public static readonly StyledProperty<string> HelpAnchorProperty =
        AvaloniaProperty.Register<SearchableDropdown, string>(nameof(HelpAnchor));

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

        bool touchesElements = change.Property == ItemsProperty
            || change.Property == SelectedItemProperty
            || change.Property == LabelProperty;
        if (touchesElements && FilterBox is null)
        {
            return; // property set before the template materialised
        }

        switch (change.Property.Name)
        {
            case nameof(Items):
                RefreshFilter();
                break;
            case nameof(SelectedItem):
                OnSelectedItemChanged(change.NewValue as string);
                break;
            case nameof(Label):
                if (FilterBox is not null)
                {
                    AutomationProperties.SetName(FilterBox, Label ?? string.Empty);
                }

                break;
        }
    }

    private void OnSelectedItemChanged(string? selection)
    {
        _filteringFromProgrammaticSet = true;
        FilterBox.Text = selection ?? string.Empty;
        _filteringFromProgrammaticSet = false;
        ClearButton.IsVisible = !string.IsNullOrEmpty(selection);
    }

    private void OnFilterGotFocus(object? sender, GotFocusEventArgs e) => OpenPopup();

    private void OnFilterLostFocus(object? sender, RoutedEventArgs e) => ClosePopup();

    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_filteringFromProgrammaticSet)
        {
            return;
        }

        string filter = FilterBox.Text ?? string.Empty;
        ClearButton.IsVisible = filter.Length > 0;

        if (filter.Length == 0 && !DropdownPopup.IsOpen)
        {
            return;
        }

        RefreshFilter();
        if (DropdownPopup.IsOpen)
        {
            ItemList.SelectedItem = _filtered.FirstOrDefault(i => i.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void RefreshFilter()
    {
        string filter = (FilterBox.Text ?? string.Empty).Trim();
        string? fallback = SelectedItem;
        _filtered.Clear();
        foreach (var item in Items ?? [])
        {
            if (filter.Length == 0
                || item.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || item == fallback)
            {
                _filtered.Add(item);
            }
        }
    }

    private void OpenPopup()
    {
        RefreshFilter();
        if (_filtered.Count == 0)
        {
            return;
        }

        ItemList.ItemsSource = _filtered;
        ItemList.SelectedItem = SelectedItem;
        DropdownPopup.PlacementTarget = ComboHost;
        DropdownPopup.WindowManagerAddShadowHint = false;
        DropdownPopup.HorizontalOffset = 0;
        DropdownPopup.VerticalOffset = 1;
        DropdownPopup.IsOpen = true;
        ItemList.Focus();
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                Commit(ItemList.SelectedItem as string ?? _filtered.FirstOrDefault());
                e.Handled = true;
                break;
            case Key.Escape:
                ClosePopupRestore();
                e.Handled = true;
                break;
        }
    }

    private void OnFilterKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when !DropdownPopup.IsOpen:
                OpenPopup();
                e.Handled = true;
                break;
            case Key.Down when ItemList.ItemCount > 0:
                ItemList.Focus();
                e.Handled = true;
                break;
            case Key.Enter when ItemList.SelectedItem is string pick:
                Commit(pick);
                e.Handled = true;
                break;
            case Key.Escape when DropdownPopup.IsOpen:
                ClosePopupRestore();
                e.Handled = true;
                break;
        }
    }

    private void OnClearClick(object? sender, PointerPressedEventArgs e)
    {
        SelectedItem = null;
        FilterBox.Clear();
        RefreshFilter();
        ClosePopup();
        FilterBox.Focus();
        e.Handled = true;
    }

    private void Commit(string? pick)
    {
        if (pick is null)
        {
            return;
        }

        SelectedItem = pick;
        ClosePopup();
        OnSelectedItemChanged(pick);
        FilterBox.Focus();
    }

    private void ClosePopupRestore()
    {
        if (SelectedItem is not null)
        {
            _filteringFromProgrammaticSet = true;
            FilterBox.Text = SelectedItem;
            _filteringFromProgrammaticSet = false;
        }

        ClosePopup();
    }

    private void ClosePopup()
    {
        DropdownPopup.IsOpen = false;
        ItemList.ItemsSource = null;
    }
}