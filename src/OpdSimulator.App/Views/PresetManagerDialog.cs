using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Views;

/// <summary>
/// Modal dialog for loading, renaming, duplicating, deleting, importing and
/// exporting presets (§17.2 Manage… surface). Styled using the same theme
/// resources as <see cref="ThemedDialog"/> so it stays consistent.
/// </summary>
public sealed class PresetManagerDialog : Window
{
    private readonly ConfigViewModel _config;
    private readonly ListBox _list = new() { MinHeight = 120 };

    /// <summary>Shows the dialog and returns the (potentially modified) preset name that was loaded, if any.</summary>
    public static async Task ShowAsync(Window owner, ConfigViewModel config)
    {
        var dlg = new PresetManagerDialog(config);
        await dlg.ShowDialog(owner);
    }

    private PresetManagerDialog(ConfigViewModel config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        Title = "Manage presets";
        Width = 520;
        MinWidth = 440;
        Height = 380;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = AppBrush("BrushBackgroundWindow");

        var inner = new Border
        {
            Background = AppBrush("BrushBackgroundPanel"),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(24),
            Padding = new Thickness(24),
        };

        var root = new StackPanel { Spacing = 12 };

        var title = new TextBlock
        {
            Text = "Saved clinic presets",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
        };
        root.Children.Add(title);

        RefreshList();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new(0, 8, 0, 0) };
        buttons.Children.Add(MakeButton("Load", OnLoad));
        buttons.Children.Add(MakeButton("Rename…", OnRename));
        buttons.Children.Add(MakeButton("Duplicate…", OnDuplicate));
        buttons.Children.Add(MakeButton("Delete", OnDelete));
        buttons.Children.Add(MakeButton("Export…", OnExport));
        buttons.Children.Add(MakeButton("Import…", OnImport));

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        footer.Children.Add(MakeButton("Close", OnClose, isPrimary: true));
        root.Children.Add(_list);
        root.Children.Add(buttons);
        root.Children.Add(footer);

        inner.Child = root;
        Content = inner;
    }

    /// <summary>Resolves one theme brush, falling back to white when the app resources are not available.</summary>
    private static IBrush AppBrush(string key)
        => Avalonia.Application.Current?.Resources.TryGetResource(key, null, out var value) == true && value is IBrush brush
            ? brush
            : Brushes.White;

    private void RefreshList()
    {
        _list.ItemsSource = _config.PresetStore.List();
        _list.SelectedIndex = _list.ItemCount > 0 ? 0 : -1;
    }

    private string? Selected => _list.SelectedItem as string;

    private void OnLoad()
    {
        if (Selected is null) return;
        _config.ApplyPreset(Selected);
        Close();
    }

    private async void OnRename()
    {
        if (Selected is null) return;
        var owner = (Window)this.GetVisualRoot()!;
        var (result, newName) = await ThemedDialog.ShowPromptAsync(owner, "Rename preset",
            $"Enter a new name for '{Selected}'.", Selected, "Rename");
        if (result != DialogResult.Confirm || string.IsNullOrWhiteSpace(newName)) return;

        try { _config.PresetStore.Rename(Selected, newName); }
        catch (PresetException ex) { await ShowError(ex.Message); return; }
        _config.RefreshPresetNames();
        RefreshList();
    }

    private async void OnDuplicate()
    {
        if (Selected is null) return;
        var owner = (Window)this.GetVisualRoot()!;
        var (result, newName) = await ThemedDialog.ShowPromptAsync(owner, "Duplicate preset",
            $"Enter a name for the copy of '{Selected}'.", Selected + " (copy)", "Duplicate");
        if (result != DialogResult.Confirm || string.IsNullOrWhiteSpace(newName)) return;

        try { _config.PresetStore.Duplicate(Selected, newName); }
        catch (PresetException ex) { await ShowError(ex.Message); return; }
        _config.RefreshPresetNames();
        RefreshList();
    }

    private async void OnDelete()
    {
        if (Selected is null) return;
        var owner = (Window)this.GetVisualRoot()!;
        var r = await ThemedDialog.ShowAsync(owner, "Delete preset",
            $"Delete the preset '{Selected}'? This cannot be undone.", isError: true,
            confirmText: "Delete", cancelText: "Keep");
        if (r != DialogResult.Confirm) return;

        _config.PresetStore.Delete(Selected);
        _config.RefreshPresetNames();
        RefreshList();
    }

    private async void OnExport()
    {
        if (Selected is null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export preset",
            SuggestedFileName = PresetNaming.Sanitize(Selected) + ".json",
            FileTypeChoices = new[] { new FilePickerFileType("Preset JSON") { Patterns = new[] { "*.json" } } },
        });
        if (files?.TryGetLocalPath() is not { } path) return;

        try { _config.PresetStore.Export(Selected, path); }
        catch (PresetException ex) { await ShowError(ex.Message); }
    }

    private async void OnImport()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import preset",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Preset JSON") { Patterns = new[] { "*.json" } } },
        });
        if (files.Count == 0) return;

        string path = files[0].TryGetLocalPath() ?? string.Empty;
        try
        {
            string imported = _config.PresetStore.Import(path);
            _config.RefreshPresetNames();
            RefreshList();
            var owner = (Window)this.GetVisualRoot()!;
            await ThemedDialog.ShowAsync(owner, "Imported", $"Preset '{imported}' was imported.", confirmText: "OK", cancelText: null);
        }
        catch (PresetException ex) { await ShowError(ex.Message); }
    }

    private void OnClose() => Close();

    private Task ShowError(string message)
        => ThemedDialog.ShowAsync((Window)this.GetVisualRoot()!, "Preset error", message,
            isError: true, confirmText: "OK", cancelText: null);

    private static Button MakeButton(string label, Action onClick, bool isPrimary = false)
    {
        var btn = new Button
        {
            Content = label,
            MinWidth = 72,
            Classes = { isPrimary ? "primaryDialogButton" : "dialogButton" },
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }
}