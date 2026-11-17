using Avalonia.Controls;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Theory tab (Phase 9Q, FR-UI-44). The view owns only the FR-UI-17 blur routing:
/// each validated field raises <c>FieldLostFocus</c>, which is sent to the matching
/// validator on <see cref="TheoryTabViewModel"/>. No parsing or arithmetic lives
/// here, so the answer key is fully testable without a window.
/// </summary>
public partial class TheoryTab : UserControl
{
    /// <summary>Creates the view and wires its four fields' blur events.</summary>
    public TheoryTab()
    {
        InitializeComponent();

        LambdaField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("lambda");
        MuField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("mu");
        ArrivalVariabilityField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("arrival");
        ServiceVariabilityField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("service");
    }

    /// <summary>The tab's view model when one is bound (headless tests may omit it).</summary>
    private TheoryTabViewModel? ViewModel => DataContext as TheoryTabViewModel;
}
