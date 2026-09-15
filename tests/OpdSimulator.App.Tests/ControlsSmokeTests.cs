using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 2 gate — one headless test per reusable control (AGENTS §16.5 +
/// milestone rule: every control needs a headless test AND a manual §18 pass).
/// The tests exercise behaviour through public API + visual-tree discovery so
/// they survive template refactors.
/// </summary>
public class ControlsSmokeTests
{
    private static Window Host(Control control, int width = 800, int height = 600)
    {
        var window = new Window { Width = width, Height = height, Content = control };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void InfoIcon_HelpText_ShowsTooltipAndAutomationName()
    {
        var icon = new InfoIcon { HelpText = "Presets live in app data.", HelpAnchor = "presets" };

        Assert.Equal("Presets live in app data.", ToolTip.GetTip(icon));
        Assert.Equal("Help: presets", AutomationProperties.GetName(icon));
    }

    [AvaloniaFact]
    public void ValidatedField_Error_ShowsCauseAndRemedyAndClearsOnFix()
    {
        var field = new ValidatedField
        {
            Label = "Arrival rate λ",
            Value = "0",
            HasError = true,
            ErrorMessage = "Arrival rate must be greater than 0. You entered 0.",
        };
        var window = Host(field);

        // FR-UI-17: the field's accessibility live-region announces cause + remedy,
        // red is never the only cue.
        Assert.Contains("error", AutomationProperties.GetName(field), System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must be greater than 0", AutomationProperties.GetName(field), System.StringComparison.OrdinalIgnoreCase);

        // Fixing clears the error instantly (FR-UI-17 anti-pattern guard).
        field.HasError = false;
        Assert.Equal("Arrival rate λ: ok", AutomationProperties.GetName(field));

        window.Close();
    }

    [AvaloniaFact]
    public void ValidatedField_FieldLostFocus_RaisesOnBlur()
    {
        var field = new ValidatedField { Label = "L", Value = "x" };
        var window = Host(field);

        bool raised = false;
        field.FieldLostFocus += (_, _) => raised = true;

        var box = field.FindDescendantOfType<TextBox>()!;
        box.RaiseEvent(new RoutedEventArgs(TextBox.LostFocusEvent));
        Assert.True(raised, "blur must surface as FieldLostFocus for validate-on-blur");

        window.Close();
    }

    [AvaloniaFact]
    public void SearchableDropdown_Filter_ReducesItemsAndCommitRoundTrips()
    {
        var dropdown = new SearchableDropdown
        {
            Label = "Service distribution",
            Items = new[] { "exponential", "erlang", "lognormal", "uniform" },
            Placeholder = "Choose…",
        };
        var window = Host(dropdown);

        var filterBox = dropdown.FindDescendantOfType<TextBox>()!;
        var popup = dropdown.FindDescendantOfType<Popup>()!;

        // Focus (GotFocus) opens the popup and fills the item list.
        filterBox.RaiseEvent(new GotFocusEventArgs());
        Assert.True(popup.IsOpen);

        var list = (popup.Child as Border)!.FindDescendantOfType<ListBox>()!;
        Assert.Equal(4, list.ItemCount);

        // Type-to-filter drops the list to the single matching item.
        filterBox.Text = "log";
        filterBox.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
        Assert.Equal(1, list.ItemCount);
        Assert.Equal("lognormal", list.SelectedItem);

        // Enter commits the filtered selection back to the SelectedItem DP.
        var enter = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };
        list.RaiseEvent(enter);
        Assert.Equal("lognormal", dropdown.SelectedItem);

        window.Close();
    }

    [AvaloniaFact]
    public void CollapsibleSection_Toggle_HidesAndRestoresContent()
    {
        var section = new CollapsibleSection
        {
            Title = "Advanced",
            Content = new TextBlock { Text = "hidden body" },
            IsExpanded = true,
        };
        var window = Host(section);

        Assert.True(section.IsExpanded);

        var toggle = section.FindDescendantOfType<ToggleButton>()!;
        toggle.IsChecked = false;
        Assert.False(section.IsExpanded, "header toggle must flow to the IsExpanded binding");

        toggle.IsChecked = true;
        Assert.True(section.IsExpanded);

        window.Close();
    }

    [AvaloniaFact]
    public void ErrorBanner_Message_ShowsAndDismissClears()
    {
        var banner = new ErrorBanner { Message = "Missing screening column; defaults applied." };
        var window = Host(banner);

        Assert.True(banner.IsVisible);

        var dismiss = banner.FindDescendantOfType<Button>()
            ?? throw new System.InvalidOperationException("banner must render a dismiss button");
        dismiss.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.False(banner.IsVisible);
        Assert.Null(banner.Message);

        window.Close();
    }

    [AvaloniaFact]
    public void ThemedToast_MessageAndSeverityRender_CloseExecutesWithItem()
    {
        var toast = new ThemedToast();
        var window = Host(toast);

        var item = ToastItem.Error("Run refused: arrival rate must be positive.");
        toast.Item = item;

        Assert.True(toast.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == item.Message),
            "toast must render the item message");

        int executed = 0;
        toast.CloseCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<ToastItem>(_ => executed++);

        var closeButton = toast.FindDescendantOfType<Button>()
            ?? throw new System.InvalidOperationException("toast must render a dismiss button");
        closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, executed);

        window.Close();
    }

    [AvaloniaFact]
    public void PinnedFooterBar_PrimaryCommand_ExecutesAndDisabledDimmed()
    {
        var footer = new PinnedFooterBar { PrimaryText = "Start Calculation", PrimaryIsEnabled = false };
        var window = Host(footer);

        var button = footer.FindDescendantOfType<Button>()
            ?? throw new System.InvalidOperationException("footer must render a primary button");
        Assert.False(button.IsEnabled, "PrimaryIsEnabled=false must disable the primary button");

        int executed = 0;
        footer.PrimaryCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => executed++);
        footer.PrimaryIsEnabled = true;
        Assert.True(button.IsEnabled);

        // A real pointer click (not a raised routed event) is what drives a
        // Button's internal command pipeline.
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        Assert.Equal(1, executed);

        window.Close();
    }

    [AvaloniaFact]
    public void ThemedDialog_EscapeCancels_PrimaryConfirms()
    {
        var dialog = new ThemedDialog { Title = "Confirm", Message = "Continue?" };
        dialog.Show();
        dialog.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(ThemedDialogResult.Cancel, dialog.Result);
        dialog.Close();

        var again = new ThemedDialog { Title = "Confirm", Message = "Continue?" };
        again.Show();
        // Declared order in the template: Secondary, then Primary.
        var buttons = again.GetVisualDescendants().OfType<Button>().Take(2).ToList();
        Assert.Equal(2, buttons.Count);

        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ThemedDialogResult.Secondary, again.Result);

        var third = new ThemedDialog { Title = "Confirm", Message = "Continue?" };
        third.Show();
        var primary = third.GetVisualDescendants().OfType<Button>().Skip(1).First();
        primary.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(ThemedDialogResult.Primary, third.Result);

        again.Close();
        third.Close();
    }

    [AvaloniaFact]
    public void DataPreviewTable_HeadersAndSortCycle_AscDescOriginal()
    {
        var table = new DataPreviewTable
        {
            ColumnTitles = new[] { "Value" },
            Rows = new[]
            {
                new[] { "banana" },
                new[] { "apple" },
                new[] { "cherry" },
            },
        };
        var window = Host(table, 640, 400);

        var list = table.FindDescendantOfType<ListBox>();
        Assert.NotNull(list);

        var sortHeader = table.GetVisualDescendants().OfType<Button>().Single();
        sortHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); // asc
        window.UpdateLayout();

        var firstCell = FirstTextInRow(list!);
        Assert.Equal("apple", firstCell, System.StringComparer.OrdinalIgnoreCase);

        sortHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); // desc
        window.UpdateLayout();
        Assert.Equal("cherry", FirstTextInRow(list!), System.StringComparer.OrdinalIgnoreCase);

        sortHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); // original
        window.UpdateLayout();
        Assert.Equal("banana", FirstTextInRow(list!), System.StringComparer.OrdinalIgnoreCase);

        window.Close();
    }

    [AvaloniaFact]
    public void DataPreviewTable_LoadErrorSummary_ShowsBannerInPlaceOfTable()
    {
        var table = new DataPreviewTable { LoadErrorSummary = "Could not read the file." };
        var window = Host(table, 640, 400);

        var banner = table.FindDescendantOfType<ErrorBanner>();
        Assert.True(banner?.IsVisible == true, "load failure must surface the error banner");

        table.LoadErrorSummary = null;
        Assert.False(banner!.IsVisible, "clearing the error must restore the table");

        window.Close();
    }

    private static string? FirstTextInRow(ListBox list)
    {
        var container = list.ContainerFromIndex(0);
        return container?.FindDescendantOfType<SelectableTextBlock>()?.Text;
    }
}