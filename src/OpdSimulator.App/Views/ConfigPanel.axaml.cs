using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;

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
/// validation to the <see cref="ConfigPanelViewModel"/> and confirms "Clear
/// All" through a themed dialog. Phase 7D moved the data-file picker to
/// <see cref="MainWindow"/> (one picker path) and the stage-mismatch actions
/// to the Input tab.
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
            _vm.ClearAllRequested -= OnClearAllRequested;
        }

        _vm = DataContext as ConfigPanelViewModel;
        if (_vm is not null)
        {
            _vm.ClearAllRequested += OnClearAllRequested;
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
            case "custom-days":
                _vm.ValidateCustomDays();
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
            case "stage-mu":
                (field.DataContext as StageRow)?.ValidateMu();
                break;
        }
    }
}