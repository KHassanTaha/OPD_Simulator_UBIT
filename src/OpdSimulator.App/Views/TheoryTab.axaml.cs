using Avalonia.Controls;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

public partial class TheoryTab : UserControl
{
    public TheoryTab()
    {
        InitializeComponent();

        LambdaField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("lambda");
        MuField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("mu");
        ArrivalVariabilityField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("arrival");
        ServiceVariabilityField.FieldLostFocus += (_, _) => ViewModel?.ValidateField("service");
    }

    private TheoryTabViewModel? ViewModel => DataContext as TheoryTabViewModel;
}
