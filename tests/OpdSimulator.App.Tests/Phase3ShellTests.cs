using System.Linq;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 3 shell tests (feat/gui-rebuild gate): MainWindow is now the
/// TabControl shell — Simulation | Input Analysis | Token Generator | Help —
/// with the Simulation tab carrying the 380px-config / fill-results split.
/// Keyboard contract (AGENTS §16.7): the tab headers are the first focusable
/// element and arrow keys move selection between tabs.
/// </summary>
public class Phase3ShellTests
{
    private static TabControl Shell(MainWindow window)
        => window.GetVisualDescendants().OfType<TabControl>().Single();

    private static string[] Headers(TabControl tabs)
        => tabs.Items.Cast<TabItem>()
               .Select(t => t.Header?.ToString() ?? "")
               .ToArray();

    [AvaloniaFact]
    public void MainWindow_HasFourTabs_InExpectedOrder()
    {
        var window = new MainWindow();
        window.Show();

        var tabs = Shell(window);
        Assert.Equal(4, tabs.Items.Count);
        Assert.Equal(
            new[] { "Simulation", "Input Analysis", "Token Generator", "Help" },
            Headers(tabs));

        window.Close();
    }

    [AvaloniaFact]
    public void SimulationTab_IsDefaultSelection()
    {
        var window = new MainWindow();
        window.Show();

        Assert.Equal(0, Shell(window).SelectedIndex);
        Assert.Equal("Simulation", (Shell(window).SelectedItem as TabItem)?.Header as string);

        window.Close();
    }

    [AvaloniaFact]
    public void SimulationTab_LaysOut380pxConfigAndFillResults()
    {
        var window = new MainWindow();
        window.Show();

        var tabs = Shell(window);
        window.UpdateLayout();

        // TabContent is hosted in the TabControl's content presenter, not inside
        // the TabItem subtree — search the window for the 3-column split.
        var grid = window.GetVisualDescendants().OfType<Grid>()
                         .First(g => g.ColumnDefinitions.Count == 3
                                  && g.ColumnDefinitions[0].Width.GridUnitType == GridUnitType.Pixel);

        Assert.Equal(GridUnitType.Pixel, grid.ColumnDefinitions[0].Width.GridUnitType);
        Assert.Equal(380d, grid.ColumnDefinitions[0].Width.Value);
        Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[2].Width.GridUnitType);

        window.Close();
    }

    [AvaloniaFact]
    public void Window_KeepsMinSize1100x700()
    {
        var window = new MainWindow();

        Assert.Equal(1100d, window.MinWidth);
        Assert.Equal(700d, window.MinHeight);
        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void ArrowKeys_MoveTabSelection_WhenHeaderFocused()
    {
        var window = new MainWindow();
        window.Show();

        var tabs = Shell(window);
        var firstHeader = (TabItem)tabs.Items[0]!;
        firstHeader.Focus();

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(1, tabs.SelectedIndex);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.Equal(2, tabs.SelectedIndex);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.Equal(1, tabs.SelectedIndex);

        window.Close();
    }
}