using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Services;

namespace OpdSimulator.App.Views;

/// <summary>
/// Left-panel code-behind: owns only view concerns — the OS file picker, the
/// preset dialogs and focus routing (FR-UI-17). All values, validation and
/// persistence live in <see cref="ConfigViewModel"/>.
/// </summary>
public partial class ConfigPanel : UserControl
{
    /// <summary>Creates the config panel.</summary>
    public ConfigPanel()
    {
        InitializeComponent();
    }

    private ConfigViewModel? Config => DataContext as ConfigViewModel;

    private MainViewModel? Root =>
        (this.GetVisualRoot() as Window)?.DataContext as MainViewModel;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (Config is not null)
        {
            Config.PropertyChanged += OnConfigPropertyChanged;
            Config.FocusFieldRequested += FocusField;

            if (Root is not null)
            {
                WireViewServices(Root);
            }
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Route every '?' badge to the in-program guide (§17.1 deep-link).
        foreach (var icon in this.GetVisualDescendants().OfType<InfoIcon>())
        {
            icon.HelpRequested += OnHelpRequested;
        }

        if (Root is not null)
        {
            WireViewServices(Root);
        }
    }

    private void WireViewServices(MainViewModel root)
    {
        // OS file picker comes from the view; the VM just asks.
        Config!.PickFileRequested = ShowFilePickerAsync;

        // Show the preview rows as soon as data lands (and on later loads).
        if (Config.HasDataFile)
        {
            PreviewTable.Load(Config.Preview);
        }
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfigViewModel.DataFilePath) && Config is not null)
        {
            // Bound VM sends collection reset anyway; keep the preview fresh.
            PreviewTable.Load(Config.Preview);
        }
        else if (e.PropertyName == nameof(ConfigViewModel.SelectedPresetName))
        {
            // The dropdown cycles the same property, so a preset change that
            // did not come from ApplyPreset must be applied here. Guarding on
            // the last applied name prevents a reload loop.
            if (Config is not null && !string.IsNullOrEmpty(Config.SelectedPresetName)
                && !string.Equals(Config.SelectedPresetName, _lastAppliedPreset, StringComparison.Ordinal))
            {
                Config.ApplyPreset(Config.SelectedPresetName);
                _lastAppliedPreset = Config.SelectedPresetName;
            }
        }
    }

    private string? _lastAppliedPreset;

    private async Task<string?> ShowFilePickerAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load patient log",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Spreadsheet or CSV")
                {
                    Patterns = new[] { "*.xlsx", "*.csv" },
                },
                FilePickerFileTypes.All,
            },
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private void OnHelpRequested(object? sender, EventArgs e)
    {
        if (sender is InfoIcon { HelpAnchor: { Length: > 0 } anchor } && Root is not null)
        {
            Root.OpenGuide(anchor);
        }
    }

    private void OnClearDataClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => Config?.ClearData();

    private async void OnSavePresetClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Config is null || Root is null)
        {
            return;
        }

        var (result, name) = await ThemedDialog.ShowPromptAsync(
            (Window)this.GetVisualRoot()!,
            "Save preset",
            "Name this saved clinic configuration. Invalid file characters are removed automatically.",
            initialValue: Config.SelectedPresetName);

        if (result != DialogResult.Confirm || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        bool ok = Config.SavePreset(name, Root.Results.VisibleWidgets, Config.CollapsedSectionKeys);
        _lastAppliedPreset = Config.SelectedPresetName;
        if (ok)
        {
            Root.Toasts.Show($"Preset '{PresetNaming.Sanitize(name)}' saved.", ToastKind.Success);
        }
        else
        {
            Root.Toasts.Show("The preset name contains no usable characters.", ToastKind.Error);
        }
    }

    private async void OnManagePresetsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Config is null || Root is null)
        {
            return;
        }

        var owner = (Window)this.GetVisualRoot()!;
        await PresetManagerDialog.ShowAsync(owner, Config);
        Root.Toasts.Show("Presets updated.", ToastKind.Info);
    }

    private void FocusField(string key)
    {
        var box = key switch
        {
            "arrival" => ArrivalBox,
            "receptionServers" => ReceptionServersBox,
            "screeningServers" => ScreeningServersBox,
            "doctorServers" => DoctorServersBox,
            "receptionRate" => ReceptionRateBox,
            "screeningRate" => ScreeningRateBox,
            "doctorRate" => DoctorRateBox,
            "horizon" => HorizonBox,
            "dailyCap" => DailyCapBox,
            "seed" => SeedBox,
            "pExit" => PExitBox,
            _ => null,
        };

        box?.Focus();
    }
}