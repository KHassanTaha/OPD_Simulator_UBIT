using CommunityToolkit.Mvvm.ComponentModel;

namespace OpdSimulator.App.ViewModels;

/// <summary>
/// Base class for all view models: provides change notification and the
/// shared contract that <see cref="ViewLocator"/> uses to resolve views.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}