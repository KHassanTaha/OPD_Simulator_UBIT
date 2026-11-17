namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

/// <summary>
/// Phase 9Q evidence (FR-UI-44): headless walkthrough of the production
/// MainWindow — click the new Theory tab, read the worked answer, switch to
/// G/G/1 and confirm the approximation notice replaces the exact-model layout.
/// Both frames use new filenames (D-166); no existing evidence is overwritten.
/// </summary>
public sealed class Phase9QScreenshots
{
    [AvaloniaFact]
    public void Render_TheoryMm1_SavePhase9qMm1Png()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 760;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            SelectTheoryTab(window);

            // Walk the exact sequence the owner will click: open the tab, read
            // the default M/M/1 answer, then confirm the collapsed formula sheet.
            Assert.NotNull(main.TheoryTab.Solution);
            Assert.Equal(TheoryModel.Mm1, main.TheoryTab.CurrentModel);
            Assert.Contains("Erlang-C", main.TheoryTab.Solution!.Result.FormulaName);

            var sheet = window.GetVisualDescendants().OfType<CollapsibleSection>()
                .Single(s => s.Title?.StartsWith("Formula sheet") == true);
            Assert.False(sheet.IsExpanded);

            Save(window, "phase-9q-theory-mm1.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_TheoryGg1_SavePhase9qGg1Png()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 760;
        window.Show();
        try
        {
            var main = Assert.IsType<MainViewModel>(window.DataContext);
            SelectTheoryTab(window);

            main.TheoryTab.SetModel(TheoryModel.Gg1);
            main.TheoryTab.ArrivalVariabilityInput = "1.2";
            main.TheoryTab.ServiceVariabilityInput = "1.4";
            main.TheoryTab.SetVariabilityMode(VariabilityMode.Cv);

            Assert.NotNull(main.TheoryTab.Solution);
            Assert.Contains("not exact in general", main.TheoryTab.ApproximationNotice);
            Assert.DoesNotContain(
                main.TheoryTab.WorkedRows.Select(r => r.Symbol),
                s => s == "P(wait)");

            var notice = window.GetVisualDescendants().OfType<Border>()
                .Single(b => b.BorderThickness == new Avalonia.Thickness(2));
            Assert.True(notice.IsEffectivelyVisible, "the G/G/1 notice must be on screen");

            Save(window, "phase-9q-theory-gg1.png");
        }
        finally
        {
            window.Close();
        }
    }

    private static void SelectTheoryTab(Window window)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 2;
        window.UpdateLayout();
        Assert.Equal("Theory", (tabs.SelectedItem as TabItem)?.Header as string);
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
}
