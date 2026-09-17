using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Root window of the OPD Clinic Queue Simulator. Phase 3 ships the shell:
/// header bar + TabControl (Simulation / Input / Token Generator / Help),
/// Simulation tab carries the 380px-config / fill-results split. Phase 7D
/// wires the shell's tab selection and the single data-file picker to the
/// view models.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.TabSelectionChanged += OnTabSelectionChanged;
            vm.PickDataFileAsync = PickDataFileAsync;
        }
    }

    /// <summary>Applies a view-model tab-selection request (RULING 2, Phase 7D).</summary>
    private void OnTabSelectionChanged(object? sender, int index) => MainTabs.SelectedIndex = index;

    /// <summary>
    /// The one file-picker path (RULING 3, Phase 7D): both the config panel's
    /// Upload button and the Input tab's Upload button route here through
    /// <see cref="MainViewModel.PickAndLoadDataFileAsync"/>.
    /// </summary>
    private async Task<string?> PickDataFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select patient data file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Patient data (.xlsx / .csv)")
                {
                    Patterns = new[] { "*.xlsx", "*.csv" },
                },
            },
        });

        if (files.Count == 0)
        {
            return null;
        }

        return files[0].TryGetLocalPath() ?? files[0].Path.ToString();
    }
}
