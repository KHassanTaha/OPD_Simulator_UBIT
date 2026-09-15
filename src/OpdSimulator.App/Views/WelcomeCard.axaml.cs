using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Welcome card surface (FR-UI-5 / AGENTS §16.6). The card itself carries no
/// logic: all content binds to <see cref="OpdSimulator.App.ViewModels.WelcomeCardViewModel"/>
/// and the containing results panel decides when it is replaced.
/// </summary>
public partial class WelcomeCard : UserControl
{
    public WelcomeCard()
    {
        InitializeComponent();
    }
}