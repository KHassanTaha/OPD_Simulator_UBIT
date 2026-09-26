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
using Avalonia.VisualTree;

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

    /// <summary>
    /// Suppresses the list-commit path while the code sets
    /// <see cref="ListBox.SelectedItem"/> itself. Selection is committed only
    /// on Enter, a user click, or clear — a preview highlight must never
    /// mutate the bound value (FR-UI-6, Phase 8E).
    /// </summary>
    private bool _suppressListCommit;

    /// <summary>
    /// True from <see cref="OpenPopup"/> until the popup closes: focus is
    /// expected to sit on the item list, so the filter box losing focus to the
    /// list is not a reason to close.
    /// </summary>
    private bool _popupHasFocus;

    /// <summary>
    /// True while the control moves focus programmatically (commit / clear), so
    /// the paired GotFocus/LostFocus pair neither reopens nor closes the popup.
    /// </summary>
    private bool _programmaticFocus;

    /// <summary>
    /// The window the outside-press handler is currently attached to, or null
    /// when the popup is closed and no handler is needed.
    /// </summary>
    private TopLevel? _outsideHook;

    public SearchableDropdown()
    {
        InitializeComponent();

        // Safety net: however the popup ends up closing, the focus flag has to
        // go back to false or a later filter-box blur would be wrongly ignored.
        DropdownPopup.Closed += (_, _) =>
        {
            _popupHasFocus = false;
            UnhookOutsidePress();
        };
    }

    /// <summary>
    /// Closes the popup when the user presses anywhere that is neither the
    /// field nor the item list — the behaviour <c>IsLightDismissEnabled</c> used
    /// to provide. The press has to be caught on the <see cref="TopLevel"/>
    /// because a press on a sibling control never bubbles into this one, so the
    /// hook is attached while the popup is open and removed when it closes.
    /// </summary>
    private void OnOutsidePress(object? sender, PointerPressedEventArgs e)
    {
        if (!DropdownPopup.IsOpen || _programmaticFocus)
        {
            return;
        }

        if (e.Source is Visual source
            && (ComboHost.IsVisualAncestorOf(source) || ItemList.IsVisualAncestorOf(source)))
        {
            return;
        }

        ClosePopup();
    }

    private void HookOutsidePress()
    {
        if (_outsideHook is not null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is { } top)
        {
            top.PointerPressed += OnOutsidePress;
            _outsideHook = top;
        }
    }

    private void UnhookOutsidePress()
    {
        if (_outsideHook is null)
        {
            return;
        }

        _outsideHook.PointerPressed -= OnOutsidePress;
        _outsideHook = null;
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

    private void OnFilterGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_programmaticFocus)
        {
            return;
        }

        OpenPopup();
    }

    private void OnFilterLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_programmaticFocus || _popupHasFocus)
        {
            return;
        }

        ClosePopup();
    }

    private void OnListGotFocus(object? sender, GotFocusEventArgs e)
    {
        _popupHasFocus = true;
    }

    private void OnListLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_programmaticFocus)
        {
            return;
        }

        _popupHasFocus = false;
        ClosePopup();
    }

    /// <summary>
    /// Commits a user-initiated list selection. A click on an item changes the
    /// ListBox selection, so this is the mouse path that the earlier version
    /// lacked entirely — only Enter could commit, which is why clicking an
    /// option left the field empty (Phase 8E).
    /// </summary>
    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressListCommit)
        {
            return;
        }

        Commit(ItemList.SelectedItem as string);
    }

    /// <summary>
    /// Sets the list's selection highlight without committing it. Used for the
    /// type-to-filter preview and for re-marking the committed value on open.
    /// </summary>
    private void SetListSelectionSilently(string? value)
    {
        _suppressListCommit = true;
        ItemList.SelectedItem = value;
        _suppressListCommit = false;
    }

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
            SetListSelectionSilently(_filtered.FirstOrDefault(i => i.Contains(filter, StringComparison.OrdinalIgnoreCase)));
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
        // The filter box doubles as the display of the committed value AND as
        // the search box. Pre-filling it with the current value filtered the
        // list down to the current item, so opening a dropdown that already had
        // a value showed nothing to switch to (Phase 8E). Clear the filter on
        // open so the FULL list is offered; the committed value is re-marked on
        // the list below and restored on close-without-commit.
        _filteringFromProgrammaticSet = true;
        FilterBox.Text = string.Empty;
        _filteringFromProgrammaticSet = false;

        RefreshFilter();
        if (_filtered.Count == 0)
        {
            return;
        }

        ItemList.ItemsSource = _filtered;
        SetListSelectionSilently(SelectedItem);
        DropdownPopup.PlacementTarget = ComboHost;
        DropdownPopup.WindowManagerAddShadowHint = false;
        DropdownPopup.HorizontalOffset = 0;
        DropdownPopup.VerticalOffset = 1;
        _popupHasFocus = true;
        DropdownPopup.IsOpen = true;
        HookOutsidePress();
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
        // Reset the committed selection AND the filter in one operation, and
        // leave the full list showing so a replacement option can be picked
        // straight away. The previous version closed the popup here, which is
        // why the user's "clear, then choose" sequence could not commit
        // (Phase 8E).
        //
        // Two details this method has to survive:
        //  1. The whole body is a programmatic mutation. The ListBox raises
        //     SelectionChanged for the selection clearing, and an unguarded
        //     handler would commit that transient state.
        //  2. × sits OUTSIDE the popup, and the popup is light-dismiss, so this
        //     very click closes the popup. The list is therefore torn down
        //     underneath the handler; it is re-opened at the end so the user
        //     still lands on a full, open list.
        _suppressListCommit = true;
        try
        {
            SelectedItem = null;
            _filteringFromProgrammaticSet = true;
            FilterBox.Clear();
            _filteringFromProgrammaticSet = false;
            ClearButton.IsVisible = false;
            RefreshFilter();
            ItemList.ItemsSource = _filtered;
            SetListSelectionSilently(null);
            DropdownPopup.IsOpen = true;
        }
        finally
        {
            _suppressListCommit = false;
        }

        _programmaticFocus = true;
        FilterBox.Focus();
        _programmaticFocus = false;
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
        _programmaticFocus = true;
        FilterBox.Focus();
        _programmaticFocus = false;
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
        // Detaching the source clears ListBox.SelectedItem, which raises
        // SelectionChanged. That is teardown, not a user choice, so it must not
        // be read as a commit (Phase 8E).
        _suppressListCommit = true;
        try
        {
            ItemList.ItemsSource = null;
        }
        finally
        {
            _suppressListCommit = false;
        }

        _popupHasFocus = false;
    }
}