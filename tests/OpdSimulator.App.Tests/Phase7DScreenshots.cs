using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 7D gate evidence (feat/milestone-7-model-driven): the merged Input tab
/// and the config panel's replacement status strip. Each test drives the real
/// <see cref="MainWindow"/> through the normal view-model seams and saves one
/// frame into <c>logs/screenshots/</c>:
/// <list type="bullet">
/// <item><c>phase-7d-input-empty.png</c> — the Input tab before a file loads
/// (upload + clear buttons, empty state).</item>
/// <item><c>phase-7d-input-loaded.png</c> — the Input tab with the sample file
/// loaded: preview table, validation/invalid-row surface, and the fit-analysis
/// cards that used to live on the separate Input Analysis tab.</item>
/// <item><c>phase-7d-input-mismatch.png</c> — the Input tab showing the D-114
/// stage-count mismatch warning and its Sync / Keep actions.</item>
/// <item><c>phase-7d-config-strip.png</c> — the Simulation tab's config panel
/// with the compact data-status strip replacing the old "1 · Data" section.</item>
/// </list>
/// </summary>
public class Phase7DScreenshots
{
    [AvaloniaFact]
    public void Render_InputTabEmpty_SavePhase7dInputEmptyPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            Assert.IsType<MainViewModel>(window.DataContext);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var frame = Capture(window, "phase-7d-input-empty.png");
            Assert.True(frame >= 512, "input-empty frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_InputTabLoaded_SavePhase7dInputLoadedPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // preview banner + preview table + 4 fit cards
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
            Assert.True(binding.IsUsable, "the sample CSV must analyse cleanly for the screenshot");
            main.InputTab.SetLoadedFile(binding);
            main.InputAnalysis.Apply(binding, "Exponential", "Exponential", 0.05);
            Assert.Equal(4, main.InputAnalysis.Charts.Count); // histogram + chi-square per fit

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var preview = window.GetVisualDescendants().OfType<DataPreviewTable>().Single();
            var analysis = window.GetVisualDescendants().OfType<InputAnalysisView>().Single();
            Assert.True(preview.IsEffectivelyVisible && analysis.IsEffectivelyVisible,
                "the merged tab must show both the preview and the fit analysis");

            var frame = Capture(window, "phase-7d-input-loaded.png");
            Assert.True(frame >= 512, "input-loaded frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_InputTabStageMismatch_SavePhase7dInputMismatchPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 1600;
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            // Default config has 3 stages; the sample detects 1 → mismatch (D-114).
            main.Config.ApplyLoadedFile(SamplePath("sample_patients.csv"));
            Assert.True(main.InputTab.HasStageMismatch, "precondition: the sample must trigger the mismatch warning");

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var sync = window.GetVisualDescendants().OfType<Button>()
                .First(b => (b.Content?.ToString() ?? "") == "Sync stages from data");
            Assert.True(sync.IsEffectivelyVisible, "the mismatch actions must be visible in the frame");

            var frame = Capture(window, "phase-7d-input-mismatch.png");
            Assert.True(frame >= 512, "input-mismatch frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_ConfigStatusStrip_SavePhase7dConfigStripPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            main.Config.ApplyLoadedFile(SamplePath("sample_patients.csv"));

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 0; // Simulation
            window.UpdateLayout();

            var strip = window.GetVisualDescendants().OfType<TextBlock>()
                .First(t => t.Text?.StartsWith("Data source: ", StringComparison.Ordinal) == true);
            Assert.True(strip.IsEffectivelyVisible, "the status strip must be visible on the Simulation tab");

            var frame = Capture(window, "phase-7d-config-strip.png");
            Assert.True(frame >= 512, "config-strip frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    private static long Capture(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        return new FileInfo(shotPath).Length;
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
