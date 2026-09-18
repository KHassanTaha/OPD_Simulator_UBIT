using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// Input tab (Phase 7D): the merged home for the data upload, its preview and
/// validation banner, the stage-mismatch warning, and the distribution-fit
/// analysis. All behaviour lives in <see cref="ViewModels.InputTabViewModel"/>.
/// </summary>
public partial class InputTab : UserControl
{
    public InputTab() => InitializeComponent();
}
