using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Main window code-behind. The view model is assigned by <see cref="App"/>
/// when the window is created; this class holds no business logic.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Creates the main window.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}