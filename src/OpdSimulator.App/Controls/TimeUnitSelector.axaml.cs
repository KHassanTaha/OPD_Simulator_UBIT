using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using OpdSimulator.App.Models;

namespace OpdSimulator.App.Controls;

/// <summary>
/// Phase 7A wrapper around <see cref="SearchableDropdown"/> for choosing the
/// <see cref="TimeUnit"/> of the rate/mean parameter fields. Reuses the
/// FR-UI-6 dropdown (type-to-filter, × clear, keyboard navigation) instead of
/// building a new one; the internal selection is a display string
/// ("Minutes" / "Seconds" / "Hours") bridged to the strongly typed
/// <see cref="SelectedUnit"/> via a two-way value converter.
/// </summary>
public partial class TimeUnitSelector : UserControl
{
    /// <summary>Display labels, in the same order as the three <see cref="TimeUnit"/> members.</summary>
    private static readonly string[] UnitLabels = { "Minutes", "Seconds", "Hours" };

    public TimeUnitSelector()
    {
        InitializeComponent();
        Dropdown.Items = UnitLabels;
        Dropdown.Bind(SearchableDropdown.SelectedItemProperty,
            new Binding(nameof(SelectedUnit))
            {
                Source = this,
                Mode = BindingMode.TwoWay,
                Converter = UnitConverter.Instance,
            });
    }

    /// <summary>The selected time unit (two-way, bound to the config view model).</summary>
    public static readonly StyledProperty<TimeUnit> SelectedUnitProperty =
        AvaloniaProperty.Register<TimeUnitSelector, TimeUnit>(
            nameof(SelectedUnit),
            defaultValue: TimeUnit.Minutes,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected time unit (two-way, bound to the config view model).</summary>
    public TimeUnit SelectedUnit
    {
        get => GetValue(SelectedUnitProperty);
        set => SetValue(SelectedUnitProperty, value);
    }

    /// <summary>
    /// Maps between the <see cref="TimeUnit"/> enum and the dropdown's display
    /// labels so both stay in two-way sync through one binding.
    /// </summary>
    private sealed class UnitConverter : IValueConverter
    {
        public static readonly UnitConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is TimeUnit unit ? unit.ToString() : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is string label ? ToUnit(label) : TimeUnit.Minutes;

        private static TimeUnit ToUnit(string label)
        {
            for (var i = 0; i < UnitLabels.Length; i++)
            {
                if (string.Equals(UnitLabels[i], label, StringComparison.Ordinal))
                {
                    return (TimeUnit)i;
                }
            }

            return TimeUnit.Minutes;
        }
    }
}