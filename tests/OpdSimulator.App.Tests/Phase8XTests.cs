using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Fitting;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8X — the three owner-visible behaviour changes that are not covered by
/// the screenshot walkthrough: the move of the fitting-unit selector into the
/// Simulation panel, and the suppression of the Start-blocked banner on a fresh
/// launch. The Token Generator tab removal is asserted by Phase3/Phase7D shell
/// tests, which now expect exactly Simulation | Input | Help.
/// </summary>
public sealed class Phase8XTests
{
    /// <summary>
    /// Why it matters: the selector must write through to the single owner of
    /// the unit (the Input tab), or the charts and the Parameters labels would
    /// report different units.
    /// </summary>
    [Fact]
    public void ConfigPanel_FittingUnitSelector_WritesThroughToTheInputTab()
    {
        var main = new MainViewModel();

        Assert.Equal(FittingTimeUnit.Minutes, main.InputTab.SelectedFittingUnit);

        main.Config.SelectedFittingUnitLabel = "hours";

        Assert.Equal(FittingTimeUnit.Hours, main.InputTab.SelectedFittingUnit);
        Assert.Equal("hours", main.Config.SelectedFittingUnitLabel);
    }

    /// <summary>
    /// Why it matters: a fresh launch has no data file, so the Start gate is
    /// correctly closed. Painting that as a red error before the user has done
    /// anything is the complaint this phase fixes; the explanation must still
    /// be available the moment they interact.
    /// </summary>
    [Fact]
    public void ConfigPanel_StartBlockedBanner_IsSilentUntilTheUserInteracts()
    {
        var config = new ConfigPanelViewModel();

        Assert.False(config.StartIsEnabled);
        Assert.NotEmpty(config.StartBlockedMessage);
        Assert.Equal(string.Empty, config.StartBlockedBannerMessage);

        config.HasInteracted = true;

        Assert.Equal(config.StartBlockedMessage, config.StartBlockedBannerMessage);
        Assert.Contains("upload a usable data file", config.StartBlockedBannerMessage);
    }

    /// <summary>
    /// Why it matters: the suppression must hold in the real shell, not only on
    /// the view model. A window that moves focus on activation must not paint
    /// the blocked banner before the user has typed or clicked, and a real
    /// click inside the panel must then reveal it — the handler is the thing
    /// that decides both, and asserting the view model flag directly would not
    /// exercise it.
    /// </summary>
    [AvaloniaFact]
    public void MainWindow_FreshLaunch_HidesTheBlockedBanner()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            window.UpdateLayout();
            // The Input tab hosts its own ErrorBanner; the Start-blocked one is
            // the banner inside the configuration panel.
            var panel = window.GetVisualDescendants().OfType<ConfigPanel>().Single();
            var banner = panel.FindDescendantOfType<ErrorBanner>();
            Assert.NotNull(banner);
            Assert.False(banner.IsVisible, "a fresh launch must not show the Start-blocked banner");

            var main = Assert.IsType<MainViewModel>(window.DataContext);
            Assert.NotEmpty(main.Config.StartBlockedMessage);
            Assert.Equal(string.Empty, main.Config.StartBlockedBannerMessage);

            // A real pointer press inside the panel is the first interaction.
            var point = panel.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            window.UpdateLayout();

            Assert.True(main.Config.HasInteracted, "a click inside the panel must count as interaction");
            Assert.NotEmpty(main.Config.StartBlockedBannerMessage);
            Assert.True(banner.IsVisible, "the blocked reason must appear once the user interacts");
        }
        finally
        {
            window.Close();
        }
    }
}
