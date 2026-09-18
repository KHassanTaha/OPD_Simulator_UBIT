using Avalonia.Controls;

namespace OpdSimulator.App.Views;

/// <summary>
/// The Input Analysis tab content (Phase 6C): hosts the P1 input charts once a
/// data file is loaded, and a themed empty state ("Load a data file to see fit
/// analysis.") at launch and after Clear All.
/// </summary>
public partial class InputAnalysisView : UserControl
{
    public InputAnalysisView() => InitializeComponent();
}