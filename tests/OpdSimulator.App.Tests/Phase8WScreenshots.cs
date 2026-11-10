namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Fitting;
using Xunit;

/// <summary>
/// Phase 8W gate evidence: the owner-visible walkthrough of the fitting-unit
/// selector. The test loads the shipped sample, selects "hours" through the real
/// <see cref="SearchableDropdown"/> (keyboard path: focus the filter box, type,
/// press Enter), waits for the production refit, then asserts what a user would
/// see — an hourly λ readout and hourly chart headings — before saving the frame.
/// </summary>
public sealed class Phase8WScreenshots
{
    [AvaloniaFact]
    public void Render_FittingUnitHours_SavePhase8wFittingUnitHoursPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // status + preview + 4 unit-labelled fit cards
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
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var dropdown = window.GetVisualDescendants().OfType<SearchableDropdown>()
                .Single(d => d.Label == "Fitting unit");
            dropdown.BringIntoView();
            window.UpdateLayout();
            Assert.True(dropdown.IsEffectivelyVisible, "the fitting-unit selector must be visible on the Input tab");

            // D-169: a visibility flag is not a bounds check. The selector is new
            // UI in a two-column header, so prove it actually sits inside the
            // window — a clipped chevron would still report IsEffectivelyVisible.
            double rightEdge = dropdown.TranslatePoint(new Point(dropdown.Bounds.Width, 0), window)!.Value.X;
            Assert.True(
                rightEdge <= window.Bounds.Width,
                $"the fitting-unit selector ends at x={rightEdge:F0}, past the {window.Bounds.Width:F0}px window");

            var filter = dropdown.GetVisualDescendants().OfType<TextBox>()
                .Single(t => t.Name == "FilterBox");
            Assert.True(filter.Focus(), "the fitting-unit filter box must take focus");
            filter.RaiseEvent(new GotFocusEventArgs());
            window.UpdateLayout();

            var popup = dropdown.GetVisualDescendants().OfType<Popup>().Single();
            Assert.True(popup.IsOpen, "the fitting-unit popup must open");
            var list = ((Border)popup.Child!).FindDescendantOfType<ListBox>()!;

            filter.Text = "hours";
            // Headless: setting Text does not raise TextChanged, so raise it the
            // way the control's own keyboard path does.
            filter.RaiseEvent(new TextChangedEventArgs(TextBox.TextChangedEvent));
            window.UpdateLayout();
            Assert.True(
                list.SelectedItem is "hours",
                $"filter left {list.ItemCount} item(s) with selection '{list.SelectedItem}'");

            list.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Enter,
            });
            window.UpdateLayout();

            Assert.Equal("hours", dropdown.SelectedItem);
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

            window.UpdateLayout();

            Assert.Contains("patients/hour", main.InputTab.MleLambdaText);
            Assert.Contains("converted from minutes", main.InputTab.MleLambdaText);

            // The fitted caption is derived from the scaled samples, so its
            // parameter values are hourly even though the verdict is unchanged.
            var histogram = main.InputAnalysis.Charts.Single(c => c.Title == "Inter-arrival time (hours)");
            Assert.NotNull(histogram.Caption);
            Assert.Contains("p =", histogram.Caption!);

            Save(window, "phase-8w-fitting-unit-hours.png");
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
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"Sample file {fileName} not found above {AppContext.BaseDirectory}");
    }
}
