using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>Code-behind for <see cref="ResultsPanel"/>; owns the toy UI state only.</summary>
public partial class ResultsPanel : UserControl
{
    /// <summary>Creates the panel.</summary>
    public ResultsPanel()
    {
        InitializeComponent();
    }

    /// <summary>Reloads the shared data preview into the widget when attached to a view model.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ResultsViewModel vm && ResultsPreviewTable is not null)
        {
            ResultsPreviewTable.Load(vm.Preview);
        }
    }

    private void OnCustomiseToggled(object? sender, RoutedEventArgs e)
    {
        if (WidgetPicker is not null)
        {
            WidgetPicker.IsVisible = WidgetPicker.IsVisible != true;
        }
    }

    private void OnWidgetToggle(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: not null } cb
            && cb.Tag is string key
            && DataContext is ResultsViewModel vm)
        {
            vm.ToggleWidget(key);
        }
    }
}