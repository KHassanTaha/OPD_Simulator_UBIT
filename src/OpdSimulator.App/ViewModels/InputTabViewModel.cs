namespace OpdSimulator.App.ViewModels;

using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpdSimulator.App.Models;

/// <summary>
/// Phase 7D: drives the merged Input tab — the single home for the uploaded
/// data file, its preview and validation banner, the stage-mismatch warning,
/// and the distribution-fit analysis. Before 7D these lived on the (deleted)
/// "Data" section of the Simulation tab and on the separate "Input Analysis"
/// tab; <see cref="Analysis"/> is the same instance the shell already owned so
/// the charts are not rebuilt twice.
/// </summary>
/// <remarks>
/// The tab owns no file-picker code and no fitting logic: it raises intent
/// events (<see cref="UploadFileRequested"/>, <see cref="UseForSimulationRequested"/>,
/// <see cref="StageMismatchSyncRequested"/>, <see cref="StageMismatchKeepRequested"/>,
/// <see cref="ClearFileRequested"/>) which <see cref="MainViewModel"/> routes
/// to the one load path and the config panel (RULING 3, Phase 7D).
/// </remarks>
public partial class InputTabViewModel : ObservableObject
{
    private readonly InputAnalysisViewModel _analysis;

    /// <summary>Creates the tab with its own fit-analysis view model (tests).</summary>
    public InputTabViewModel()
        : this(new InputAnalysisViewModel())
    {
    }

    /// <summary>Creates the tab sharing the shell's fit-analysis view model.</summary>
    /// <param name="analysis">The fit-analysis view model the shell already owns.</param>
    public InputTabViewModel(InputAnalysisViewModel analysis)
    {
        _analysis = analysis;
    }

    /// <summary>The loaded-file preview and validation summary.</summary>
    public InputPreviewViewModel Preview { get; } = new();

    /// <summary>The distribution-fit charts for the loaded file (shared with the shell).</summary>
    public InputAnalysisViewModel Analysis => _analysis;

    /// <summary>True once a file has been loaded (valid or rejected).</summary>
    [ObservableProperty]
    private bool hasFile;

    /// <summary>One-line status under the upload buttons.</summary>
    [ObservableProperty]
    private string statusText = "No file loaded.";

    /// <summary>True while the configured stage list differs from the loaded data's stages.</summary>
    [ObservableProperty]
    private bool hasStageMismatch;

    /// <summary>The full mismatch explanation, shown in the tab's amber warning.</summary>
    [ObservableProperty]
    private string stageMismatchMessage = "";

    /// <summary>Raised when the user asks to open the file picker; the shell owns the one picker.</summary>
    public event EventHandler? UploadFileRequested;

    /// <summary>Raised when the user asks to run from this file; the shell sets FitFromData and shows the Simulation tab.</summary>
    public event EventHandler? UseForSimulationRequested;

    /// <summary>Raised when the user asks to replace the config's stages with the data's stages.</summary>
    public event EventHandler? StageMismatchSyncRequested;

    /// <summary>Raised when the user dismisses the mismatch warning without changing the stages.</summary>
    public event EventHandler? StageMismatchKeepRequested;

    /// <summary>Raised after the file is cleared so the shell can unload it from the config panel too.</summary>
    public event EventHandler? ClearFileRequested;

    /// <summary>Hosts the file picker: raises <see cref="UploadFileRequested"/> (the shell resolves the path).</summary>
    [RelayCommand]
    private void UploadFile() => UploadFileRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Unloads the current file and returns the tab to its empty state.</summary>
    [RelayCommand]
    private void ClearFile()
    {
        Clear();
        ClearFileRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks the shell to fit the run from this file and show the Simulation tab.</summary>
    [RelayCommand]
    private void UseForSimulation() => UseForSimulationRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the shell to replace the config's stage list with the data's stages.</summary>
    [RelayCommand]
    private void SyncStagesFromData() => StageMismatchSyncRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Dismisses the mismatch warning for this session (handled by the shell).</summary>
    [RelayCommand]
    private void KeepCurrentStages() => StageMismatchKeepRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Shows a loaded binding: populates the preview (rows, invalid-row map,
    /// validation banner) and moves the tab out of its empty state. The fit
    /// analysis itself is applied by the shell through the shared
    /// <see cref="Analysis"/> instance.
    /// </summary>
    /// <param name="binding">The analysed data binding.</param>
    public void SetLoadedFile(DataBindingResult binding)
    {
        Preview.SetPreview(binding);
        HasFile = true;

        string name = binding.SourcePath is { Length: > 0 } path ? Path.GetFileName(path) : "data file";
        StatusText = binding.ErrorMessage is not null
            ? $"Could not load {name}."
            : $"Loaded {binding.DataSet?.RowCount ?? 0} rows from {name}.";
    }

    /// <summary>Mirrors the config panel's stage-mismatch state into the tab's warning.</summary>
    /// <param name="visible">Whether the warning should show.</param>
    /// <param name="message">The full mismatch explanation.</param>
    public void ApplyStageMismatch(bool visible, string message)
    {
        HasStageMismatch = visible;
        StageMismatchMessage = message;
    }

    /// <summary>Returns the tab to its no-file state: empty preview, empty charts, no warning.</summary>
    public void Clear()
    {
        Preview.Clear();
        HasFile = false;
        StatusText = "No file loaded.";
        HasStageMismatch = false;
        StageMismatchMessage = "";
        _analysis.Apply(null, "Exponential", "Exponential", 0.05);
    }
}
