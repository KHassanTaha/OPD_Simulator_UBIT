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
    /// Opens the themed calculations dialog (Phase 8M, D-164, FR-UI-29). The body
    /// is a read-only monospace TextBlock of the text the pure
    /// <see cref="Services.CalculationsTextBuilder"/> produced, and the dialog's
    /// own Copy button puts it on the clipboard.
    /// </summary>
    private async void OnCalculationsRequested(object? sender, EventArgs e)
    {
        if (_results is null)
        {
            return;
        }

        var body = new TextBlock
        {
            Text = _results.CalculationsText,
            FontFamily = (Avalonia.Media.FontFamily)this.FindResource("FontFamilyMono")!,
            FontSize = 12,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        };

        var dialog = new Controls.ThemedDialog
        {
            Title = "Calculations",
            Body = new ScrollViewer
            {
                MaxHeight = 460,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = body,
            },
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