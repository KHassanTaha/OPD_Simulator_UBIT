using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using Serilog;

namespace OpdSimulator.App.Views;

/// <summary>
/// Attached property that labels every <see cref="ValidatedField"/> in the
/// config panel with its validation key. The code-behind uses it to route the
/// blur-time <c>FieldLostFocus</c> event to the exact validator on the view
/// model without a name-scope lookup per field.
/// </summary>
public sealed class ConfigPanelValidation
{
    /// <summary>Attached property identifying which validator a field feeds.</summary>
    public static readonly AttachedProperty<string?> ValidationKeyProperty =
        AvaloniaProperty.RegisterAttached<ConfigPanelValidation, Control, string?>(
            "ValidationKey", null);

    /// <summary>Sets the validation key on a field (used from AXAML).</summary>
    public static void SetValidationKey(Control element, string? value) =>
        element.SetValue(ValidationKeyProperty, value);

    /// <summary>Gets the validation key carried by a field.</summary>
    public static string? GetValidationKey(Control element) =>
        element.GetValue(ValidationKeyProperty);
}

/// <summary>
/// Phase-4 configuration panel. Owns no simulation logic: it forwards blur
/// validation to the <see cref="ConfigPanelViewModel"/>, opens the data-file
/// picker on Upload, and confirms "Clear All" through a themed dialog.
/// </summary>
public partial class ConfigPanel : UserControl
{
    private ConfigPanelViewModel? _vm;

    public ConfigPanel()
    {
        InitializeComponent();

        // All ValidatedField blur events bubble up to the panel; route them by
        // ValidationKey so blur-time validation stays on the view model.
        AddHandler(ValidatedField.FieldLostFocusEvent, OnFieldLostFocus);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.UploadRequested -= OnUploadRequested;
            _vm.ClearAllRequested -= OnClearAllRequested;
            _vm.SyncStagesRequested -= OnSyncStagesRequested;
            _vm.KeepStageMismatchRequested -= OnKeepStageMismatchRequested;
        }

        _vm = DataContext as ConfigPanelViewModel;
        if (_vm is not null)
        {
            _vm.UploadRequested += OnUploadRequested;
            _vm.ClearAllRequested += OnClearAllRequested;
            _vm.SyncStagesRequested += OnSyncStagesRequested;
            _vm.KeepStageMismatchRequested += OnKeepStageMismatchRequested;
        }
    }

    private async void OnUploadRequested(object? sender, EventArgs e)
    {
        var topLevel = this.GetVisualRoot() as TopLevel;
        if (topLevel is null || _vm is null)
        {
            return;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
                return;
            }

            var path = files[0].TryGetLocalPath() ?? files[0].Path.ToString();
            _vm.ApplyLoadedFile(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "File picker failed");
            if (VisualRoot is Window owner)
            {
                await ThemedDialog.ShowMessageAsync(
                    owner,
                    "Could not open file picker",
                    "A system error prevented the file dialog from opening. See logs/errors-*.log for details.");
            }
        }
    }

    private async void OnClearAllRequested(object? sender, EventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        var owner = this.GetVisualRoot() as Window;
        var result = await ThemedDialog.ShowMessageAsync(
            owner,
            "Clear all fields?",
            "Reset the configuration and the current results to the fresh-launch state?",
            "Clear",
            "Cancel");

        if (result != ThemedDialogResult.Primary)
        {
            return;
        }

        if (owner?.DataContext is MainViewModel main)
        {
            // Full reset (Phase 5c.4): config fields + uploaded file + results panel.
            main.ResetAll();
        }
        else
        {
            // Standalone host (controls demo / tests without a MainWindow): config only.
            _vm.ResetToDefaults();
        }
    }

    private async void OnSyncStagesRequested(object? sender, EventArgs e)
    {
        if (_vm is null || _vm.Binding is not { } binding)
        {
            return;
        }

        var owner = this.GetVisualRoot() as Window;
        var result = await ThemedDialog.ShowMessageAsync(
            owner,
            "Sync stages to the data?",
            $"Replace the configured stage list with the {binding.StageNames.Count} stage(s) found in the loaded data?",
            "Sync",
            "Cancel");

        if (result == ThemedDialogResult.Primary)
        {
            _vm.SyncStagesToData();
        }
    }

    private void OnKeepStageMismatchRequested(object? sender, EventArgs e)
    {
        // Non-destructive: no confirmation needed, the warning is merely
        // dismissed for the session (5d.3).
        _vm?.DismissStageMismatchWarning();
    }

    private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not ValidatedField field || _vm is null)
        {
            return;
        }

        switch (ConfigPanelValidation.GetValidationKey(field))
        {
            case "manual-lambda":
                _vm.ValidateManualLambda();
                break;
            case "manual-mu":
                _vm.ValidateManualMuPerStage();
                break;
            case "p-exit":
                _vm.ValidatePExit();
                break;
            case "significance-level":
                _vm.ValidateSignificanceLevel();
                break;
            case "stage-count":
                _vm.ValidateStageCount();
                break;
            case "days":
                _vm.ValidateDays();
                break;
            case "daily-cap":
                _vm.ValidateDailyCap();
                break;
            case "horizon-minutes":
                _vm.ValidateHorizonMinutes();
                break;
            case "seed":
                _vm.ValidateSeed();
                break;
            case "servers":
                (field.DataContext as StageRow)?.ValidateServers();
                break;
        }
    }
}