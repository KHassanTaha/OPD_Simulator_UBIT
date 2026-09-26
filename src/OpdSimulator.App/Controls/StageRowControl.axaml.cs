using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OpdSimulator.App.Controls;

/// <summary>
/// One stage row in the Stages section: name, servers, model shortcut, optional
/// advanced family selection with the spread input that family needs, and the
/// service rate μ (Phase 8K, D-150).
/// </summary>
/// <remarks>
/// Extracted from an inline <c>DataTemplate</c> in <c>ConfigPanel.axaml</c> so the
/// row is one reusable component rather than a template that has to stay in step
/// with the row view model. It carries no logic of its own — every binding targets
/// <c>StageRow</c>, and the blur validation dispatched by
/// <c>ConfigPanelValidation.ValidationKey</c> is handled by the panel that hosts it.
/// </remarks>
public sealed partial class StageRowControl : UserControl
{
    /// <summary>Creates the control and loads its XAML.</summary>
    public StageRowControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
