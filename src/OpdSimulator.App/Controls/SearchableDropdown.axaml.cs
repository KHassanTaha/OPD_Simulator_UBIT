using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpdSimulator.App.Services;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Typing-filterable dropdown of string options (FR-UI-6): type to filter,
/// arrow keys to navigate, Enter to choose, Escape to dismiss, "×" to clear,
/// chevron to toggle the list. Filtering delegates to the pure
/// <see cref="SearchFilter"/> so the ranking can be unit tested.
/// </summary>
public partial class SearchableDropdown : UserControl
{
    private readonly List<string> _allItems = new();

    /// <summary>Identifies the <see cref="Items"/> direct property.</summary>
    public static readonly DirectProperty<SearchableDropdown, IEnumerable<string>?> ItemsProperty =
        AvaloniaProperty.RegisterDirect<SearchableDropdown, IEnumerable<string>?>(
            nameof(Items), c => c.Items, (c, v) => c.Items = v);

    /// <summary>Identifies the <see cref="SelectedValue"/> styled property.</summary>
    public static readonly StyledProperty<string?> SelectedValueProperty =
        AvaloniaProperty.Register<SearchableDropdown, string?>(nameof(SelectedValue));

    /// <summary>Identifies the <see cref="Watermark"/> styled property.</summary>
    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<SearchableDropdown, string>(nameof(Watermark), "(none)");

    private IEnumerable<string>? _items;

    /// <summary>Gets or sets the option list shown in the dropdown.</summary>
    public IEnumerable<string>? Items
    {
        get => _items;
        set
        {
            var changed = !ReferenceEquals(_items, value);
            _items = value;
            _allItems.Clear();
            if (value is not null)
            {
                _allItems.AddRange(value);
            }

            if (changed)
            {
                ReapplyFilter();
            }
        }
    }

    /// <summary>Gets or sets the currently chosen option (two-way bindable).</summary>
    public string? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>Gets or sets the placeholder text shown when nothing is typed.</summary>
    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    /// <summary>Creates the dropdown.</summary>
    public SearchableDropdown()
    {
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedValueProperty && SearchBox is not null)
        {
            var value = (string?)change.NewValue;
            if (!string.Equals(SearchBox.Text, value, StringComparison.Ordinal))
            {
                SearchBox.Text = value ?? string.Empty;
            }
        }
        else if (change.Property == WatermarkProperty && SearchBox is not null)
        {
            SearchBox.Watermark = Watermark;
        }
        else if (change.Property == BoundsProperty)
        {
            // Keep the suggestion list flush beneath the field.
            DropDown.Margin = new Thickness(0, Bounds.Height - 2, 0, 0);
        }
    }

    private void OnFilterChanged(object? sender, TextChangedEventArgs e)
    {
        ReapplyFilter();

        // A typed query implies intent to pick, so surface the list.
        if (QueryLength() > 0)
        {
            DropDown.IsVisible = true;
        }

        ClearButton.IsVisible = QueryLength() > 0;
    }

    private int QueryLength() => SearchBox?.Text?.Length ?? 0;

    private void ReapplyFilter()
    {
        var matches = SearchFilter.Filter(_allItems, SearchBox?.Text);
        Suggestions.ItemsSource = matches;
        NoResults.IsVisible = DropDown.IsVisible && matches.Count == 0 && QueryLength() > 0;
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SelectedValue = null;
        ClearButton.IsVisible = false;
        DropDown.IsVisible = true;
        ReapplyFilter();
        SearchBox.Focus();
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        DropDown.IsVisible = ToggleButton.IsChecked == true;
        ReapplyFilter();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Suggestions.SelectedItem is string chosen)
        {
            CommitSelection(chosen);
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Down when DropDown.IsVisible:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up when DropDown.IsVisible:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter when DropDown.IsVisible && Suggestions.SelectedItem is string chosen:
                CommitSelection(chosen);
                e.Handled = true;
                break;
            case Key.Escape when DropDown.IsVisible:
                DropDown.IsVisible = false;
                ToggleButton.IsChecked = false;
                e.Handled = true;
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (Suggestions.ItemCount == 0)
        {
            return;
        }

        var next = delta > 0
            ? Suggestions.SelectedIndex + 1
            : Suggestions.SelectedIndex - 1;

        if (next < 0 || next >= Suggestions.ItemCount)
        {
            return;
        }

        Suggestions.SelectedIndex = next;
        Suggestions.ScrollIntoView(Suggestions.SelectedItem!);
    }

    private void CommitSelection(string chosen)
    {
        SelectedValue = chosen;
        SearchBox.Text = chosen;
        DropDown.IsVisible = false;
        ToggleButton.IsChecked = false;
        ClearButton.IsVisible = true;
    }
}