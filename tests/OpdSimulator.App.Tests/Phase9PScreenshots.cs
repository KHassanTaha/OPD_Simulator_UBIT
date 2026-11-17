namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Fitting;
using Xunit;

/// <summary>
/// Phase 9P gate evidence (D-240): the owner-visible walkthrough of the ONE unit
/// selector. The test loads the shipped sample, picks "Hours" through the
/// production <see cref="TimeUnitSelector"/>, asserts the selection reaches the
/// configuration's single <see cref="TimeUnit"/> state, waits for the production
/// refit, then saves one frame of the selector and one of the hourly charts it
/// produced.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the Phase 8X screenshots, which drove a second, display-only
/// "Distribution fitting unit" dropdown. 9P deleted that control, so that test
/// could no longer run; its two frames stay on disk as the record of what it
/// showed. The two frames written here are new filenames (D-166).
/// </para>
/// <para>
/// What the test asserts is the point of the phase: choosing a unit once, in the
/// Simulation panel, moves the fitted readouts and the charts together — there is
/// no second selection to fall out of step.
/// </para>
/// </remarks>
public sealed class Phase9PScreenshots
{
    [AvaloniaFact]
    public void Render_TimeUnitHours_SavePhase9pTimeUnitPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // model section + the four unit-labelled fit cards
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);

            // The production load path: config owns the binding and pushes it to
            // the Input tab, so the unit-change refit below has data to refit.
            main.Config.ApplyLoadedFile(SamplePath("sample_patients.csv"));
            Assert.True(main.Config.Binding?.IsUsable == true, "the sample CSV must analyse cleanly");
            Assert.Contains("patients/min", main.InputTab.MleLambdaText);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 0; // Simulation — where the one selector lives
            window.UpdateLayout();

            var dropdown = window.GetVisualDescendants().OfType<TimeUnitSelector>()
                .Single()
                .FindDescendantOfType<DropdownField>()!;
            dropdown.BringIntoView();
            window.UpdateLayout();
            Assert.True(dropdown.IsEffectivelyVisible, "the time-unit selector must be visible in the Simulation panel");

            // D-169: a visibility flag is not a bounds check.
            double rightEdge = dropdown.TranslatePoint(new Point(dropdown.Bounds.Width, 0), window)!.Value.X;
            Assert.True(
                rightEdge <= window.Bounds.Width,
                $"the time-unit selector ends at x={rightEdge:F0}, past the {window.Bounds.Width:F0}px window");

            var combo = dropdown.FindDescendantOfType<ComboBox>()!;
            combo.IsDropDownOpen = true;
            window.UpdateLayout();
            Assert.True(combo.IsDropDownOpen, "the prebuilt ComboBox must open");

            // All five units are offered, read from the enum rather than copied:
            // a hand-written list is how Days/Sessions would have been missed.
            Assert.Equal(FittingTimeUnits.Options.Count, combo.ItemCount);
            Assert.Equal(Enum.GetNames<TimeUnit>().Length, combo.ItemCount);
            Save(window, "phase-9p-time-unit-selector.png");

            combo.SelectedItem = "Hours";
            combo.IsDropDownOpen = false;
            window.UpdateLayout();

            Assert.Equal("Hours", dropdown.SelectedItem);
            Assert.Equal(TimeUnit.Hours, main.Config.TimeUnit);
            Assert.Equal(FittingTimeUnit.Hours, main.InputTab.SelectedFittingUnit);

            // The shell refits on the background thread. Wait for the production
            // apply to land rather than calling Apply again from the test.
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!main.InputAnalysis.Charts.Any(c => c.Title == "Inter-arrival time (hours)"))
            {
                Assert.True(DateTime.UtcNow < deadline, "the Input-tab charts never refitted in hours");
                Dispatcher.UIThread.RunJobs();
                System.Threading.Thread.Sleep(10);
            }

            tabs.SelectedIndex = 1; // Input — the charts the unit produced
            window.UpdateLayout();

            Assert.Contains("patients/hour", main.InputTab.MleLambdaText);
            Assert.Contains("converted from minutes", main.InputTab.MleLambdaText);

            // The fitted caption is derived from the scaled samples, so its
            // parameter values are hourly even though the verdict is unchanged.
            var histogram = main.InputAnalysis.Charts.Single(c => c.Title == "Inter-arrival time (hours)");
            Assert.NotNull(histogram.Caption);
            Assert.Contains("p =", histogram.Caption!);

            Save(window, "phase-9p-time-unit-hours.png");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);

        var length = new FileInfo(shotPath).Length;
        Assert.True(length >= 512, $"{fileName} missing or suspiciously small ({length} bytes)");
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("could not locate OpdSimulator.sln from " + start);
    }

    private static string SamplePath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "samples")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException("could not locate samples/ from " + AppContext.BaseDirectory);
        }

        return Path.Combine(dir, "samples", fileName);
    }
}
