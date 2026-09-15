using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Root window of the OPD Clinic Queue Simulator. Phase 1 ships the empty
/// maximized shell; the TabControl layout lands in Phase 3.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}