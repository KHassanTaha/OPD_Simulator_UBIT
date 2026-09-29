using Avalonia.Controls;
using Avalonia.VisualTree;

namespace OpdSimulator.App.Views;

/// <summary>
/// Phase-5 results panel surface. Owns no simulation logic: it only reveals
/// the widget-visibility picker while its toggle is checked; the view model
/// drives everything else.
/// </summary>
public partial class ResultsPanel : UserControl
{
    /// <summary>Unsubscribe target for the results view model.</summary>
    private ViewModels.ResultsPanelViewModel? _results;

    public ResultsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_results is not null)
        {
            _results.CalculationsRequested -= OnCalculationsRequested;
        }

        // The view model announces the request; opening the window is the view's
        // job, the same split every other dialog in this app uses.
        _results = DataContext as ViewModels.ResultsPanelViewModel;
        if (_results is not null)
        {
            _results.CalculationsRequested += OnCalculationsRequested;
        }
    }

    /// <summary>
    /// Opens the calculations dialog (Phase 8M, D-164, FR-UI-29; re-laid-out in
    /// Phase 8N, D-165). The body arrives as structured
    /// <see cref="Services.CalculationRow"/> items so the dialog can widen the label
    /// column and wrap the value column, and the dialog's own Copy button puts the
    /// flat text on the clipboard.
    /// </summary>
    /// <remarks>
    /// D-166: nothing here sets Width, MinWidth or MaxWidth. Those live in
    /// CalculationsDialog.axaml. A call site that overrides them is how the Phase 8M
    /// screenshot test ended up rendering a dialog no user could see.
    /// </remarks>
    private async void OnCalculationsRequested(object? sender, EventArgs e)
    {
        if (_results is null)
        {
            return;
        }

        var dialog = new Controls.CalculationsDialog
        {
            Title = "Calculations",
            Rows = _results.CalculationsRows,
            CopyText = _results.CalculationsText,
        };

        var owner = this.GetVisualRoot() as Window;
        if (owner is not null)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    private void OnCustomiseToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => WidgetPicker!.IsVisible = CustomiseToggle?.IsChecked == true;
}