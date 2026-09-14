namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Root view model for the main window. Owns the window title for now;
/// the config/results sub-models are introduced in sub-blocks D and E.
/// </summary>
public class MainViewModel : ViewModelBase
{
    /// <summary>Window title shown in the OS title bar.</summary>
    public string Title => "OPD Clinic Queue Simulator";
}