using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.2 gate evidence (feat/milestone-6c-input-analysis-charts): the real
/// <see cref="MainWindow"/> with the Input Analysis tab selected, fed the real
/// sample dataset through the Data / fit pipeline, showing the Inter-arrival and
/// Screening histogram cards — observed columns plus the fitted-density overlay —
/// saved as <c>logs/screenshots/phase-6c2-histograms.png</c>.
/// </summary>
public class Phase6c2Screenshot
{
    [AvaloniaFact]
    public void Render_InputAnalysisHistograms_SavePhase6c2Screenshot()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
            Assert.True(binding.IsUsable, "the sample CSV must analyse cleanly for the screenshot");
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            main.InputAnalysis.Apply(binding, "Exponential", "Exponential", 0.05);
            Assert.Equal(2, main.InputAnalysis.Charts.Count);
            Assert.False(main.InputAnalysis.IsEmpty);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 1; // Input Analysis
            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-6c2-histograms.png");
            frame.Save(shotPath);

            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "histogram frame missing or suspiciously small");
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