using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App;

/// <summary>
/// Maps a view-model type to its view, enabling Avalonia's automatic
/// DataTemplate resolution when a view model is hosted inside content
/// controls (e.g. the results panel hosting the welcome card view model).
/// </summary>
public class ViewLocator : IDataTemplate
{
    /// <inheritdoc/>
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <inheritdoc/>
    public bool Match(object? data) => data is ViewModelBase;
}