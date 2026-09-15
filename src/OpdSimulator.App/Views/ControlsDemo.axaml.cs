using Avalonia.Controls;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Phase-2 showroom host: wires demo interactions (validation-on-blur,
/// toast emitters, themed dialog opener) that live in the view, leaving the
/// data and commands in <see cref="ControlsDemoViewModel"/>.
/// </summary>
public partial class ControlsDemo : UserControl
{
    private readonly ControlsDemoViewModel _vm;

    public ControlsDemo()
    {
        InitializeComponent();

        _vm = new ControlsDemoViewModel();
        DataContext = _vm;
        ArrivalField.FieldLostFocus += (_, _) => _vm.ValidateArrivalRate();
    }

    private void OnAddInfoToast(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.ShowToast("info", "Sample CSV loaded — 1,000 rows, 1 skipped.");

    private void OnAddSuccessToast(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.ShowToast("success", "Simulation finished. Results are ready.");

    private void OnAddWarningToast(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.ShowToast("warning", "p_exit defaulted to 0.5 — unknown column.");

    private void OnAddErrorToast(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _vm.ShowToast("error", "Run refused: arrival rate must be positive.");

    private async void OnOpenDialogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var owner = this.VisualRoot as Window;
        _ = await ThemedDialog.ShowMessageAsync(
            owner,
            "Confirm run",
            "This starts a 3-day simulation.\n\nIt may take under a second. Continue?",
            "Start",
            "Cancel");
    }
}