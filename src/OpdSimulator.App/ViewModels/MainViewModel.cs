using CommunityToolkit.Mvvm.ComponentModel;

namespace OpdSimulator.App.ViewModels;

/// <summary>Window-level view model for MainWindow: exposes the panel view models.</summary>
public partial class MainViewModel : ObservableObject
{
    /// <summary>Configuration panel state (Simulation tab, left column).</summary>
    public ConfigPanelViewModel Config { get; } = new();
}