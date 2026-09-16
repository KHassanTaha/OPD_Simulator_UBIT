using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Phase-5 results panel surface. Owns no simulation logic: it only reveals
/// the widget-visibility picker while its toggle is checked; the view model
/// drives everything else.
/// </summary>
public partial class ResultsPanel : UserControl
{
    public ResultsPanel()
    {
        InitializeComponent();
    }

    private void OnCustomiseToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WidgetPicker!.IsVisible = CustomiseToggle?.IsChecked == true;
}