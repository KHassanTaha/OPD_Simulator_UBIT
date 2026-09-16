using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Root window of the OPD Clinic Queue Simulator. Phase 3 ships the shell:
/// header bar + TabControl (Simulation / Input Analysis / Token Generator /
/// Help), Simulation tab carries the 380px-config / fill-results split.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}