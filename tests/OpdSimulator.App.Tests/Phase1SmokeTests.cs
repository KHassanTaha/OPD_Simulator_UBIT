using System;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 1 smoke tests (feat/gui-rebuild gate): the rebuilt App boots on the
/// headless platform, the maximized "OPD Clinic Queue Simulator" window opens,
/// and every Theme/Motion token defined in Assets resolves at application level.
/// </summary>
public class Phase1SmokeTests
{
    private static IResourceDictionary Resources
        => Application.Current?.Resources
            ?? throw new InvalidOperationException("Application.Current is null");

    [AvaloniaFact]
    public void MainWindow_HasExpectedTitleAndState()
    {
        var window = new MainWindow();

        Assert.Equal("OPD Clinic Queue Simulator", window.Title);
        Assert.Equal(WindowState.Maximized, window.WindowState);

        window.Show();
        Assert.True(window.IsVisible, "window must open on the headless platform");
        window.Close();
    }

    [AvaloniaFact]
    public void ThemeTokens_AllResolve()
    {
        AssertResolvable("BrushBrandGreen");
        AssertResolvable("BrushBackgroundWindow");
        AssertResolvable("BrushTextPrimary");
        AssertResolvable("BrushFocusRing");
        AssertResolvable("BrushError");
    }

    [AvaloniaFact]
    public void ThemeTypography_SizesMatchContract()
    {
        Assert.Equal(18d, Try<double>("FontSizeTitle"));
        Assert.Equal(14d, Try<double>("FontSizeBody"));
        Assert.Equal(12d, Try<double>("FontSizeCaption"));
    }

    [AvaloniaFact]
    public void ThemeLayout_TokensMatchContract()
    {
        Assert.Equal(new Thickness(4), Try<Thickness>("ThicknessSpaceXxs"));
        Assert.Equal(new Thickness(8), Try<Thickness>("ThicknessSpaceXs"));
        Assert.Equal(new Thickness(12), Try<Thickness>("ThicknessSpaceS"));
        Assert.Equal(new Thickness(16), Try<Thickness>("ThicknessSpaceM"));
        Assert.Equal(new Thickness(24), Try<Thickness>("ThicknessSpaceL"));

        Assert.Equal(new CornerRadius(4), Try<CornerRadius>("CornerRadiusS"));
        Assert.Equal(new CornerRadius(8), Try<CornerRadius>("CornerRadiusM"));
        Assert.Equal(new CornerRadius(12), Try<CornerRadius>("CornerRadiusL"));

        Assert.Equal(new Thickness(2), Try<Thickness>("ThicknessFocusRing"));
        Assert.Equal(new Thickness(1), Try<Thickness>("ThicknessFocusRingOffset"));
        AssertResolvable("ShadowCard");
        AssertResolvable("ShadowOverlay");
    }

    [AvaloniaFact]
    public void MotionDurations_MatchContract()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(150), Try<TimeSpan>("MotionDurationFast"));
        Assert.Equal(TimeSpan.FromMilliseconds(200), Try<TimeSpan>("MotionDurationMedium"));
        Assert.Equal(TimeSpan.FromMilliseconds(250), Try<TimeSpan>("MotionDurationNormal"));
        Assert.Equal(TimeSpan.FromMilliseconds(600), Try<TimeSpan>("MotionDurationSlow"));
        Assert.Equal(TimeSpan.Zero, Try<TimeSpan>("MotionDurationReduced"));
    }

    [AvaloniaFact]
    public void Spacing_SizesMatchContract()
    {
        Assert.Equal(4d, Try<double>("SpaceXxs"));
        Assert.Equal(8d, Try<double>("SpaceXs"));
        Assert.Equal(12d, Try<double>("SpaceS"));
        Assert.Equal(16d, Try<double>("SpaceM"));
        Assert.Equal(24d, Try<double>("SpaceL"));

        Assert.Equal(new Thickness(16), Try<Thickness>("ThicknessWindowPadding"));
    }

    private static void AssertResolvable(string key)
        => Assert.True(Resources.TryGetResource(key, null, out _), $"resource '{key}' must resolve");

    private static T Try<T>(string key)
    {
        Assert.True(Resources.TryGetResource(key, null, out var value), $"resource '{key}' must resolve");
        return Assert.IsType<T>(value);
    }
}