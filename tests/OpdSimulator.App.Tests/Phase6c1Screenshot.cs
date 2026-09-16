using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.1 gate evidence (feat/milestone-6c-input-analysis-charts): the real
/// <see cref="MainWindow"/> with the Input Analysis tab selected, showing the new
/// tab scaffold — the themed empty state ("Load a data file to see fit
/// analysis.") — saved as <c>logs/screenshots/phase-6c1-empty.png</c>.
/// </summary>
public class Phase6c1Screenshot
{
    [AvaloniaFact]
    public void Render_InputAnalysisTabEmptyState_SavePhase6c1Screenshot()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input Analysis
            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-6c1-empty.png");
            frame.Save(shotPath);

            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 256,
                "empty-state frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
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