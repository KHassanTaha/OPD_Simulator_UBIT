using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 9A frame evidence (D-089, D-166, AGENTS §18): the same MainWindow at
/// production size in each configuration path. Fit mode must show the data-source
/// strip; manual mode must not. The frames are new files — existing evidence is
/// append-only and is never overwritten.
/// </summary>
public class Phase9AScreenshots
{
    [AvaloniaFact]
    public void Render_FitMode_SavePhase9aFitModePng()
    {
        // No size override: MainWindow.axaml declares 1200×760, so the frame is
        // the layout the app actually renders (D-166).
        var window = new MainWindow();
        window.Show();
        try
        {
            var config = RequireMain(window).Config;
            config.ApplyLoadedFile(Sample("sample_3stage_variable.csv"));
            window.UpdateLayout();

            // Structural assertion behind the frame: the strip is present.
            var manage = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Manage input →");
            Assert.True(manage.IsEffectivelyVisible);

            Save(window, "phase-9a-fit-mode.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_ManualMode_SavePhase9aManualModePng()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var config = RequireMain(window).Config;
            config.SourceMode = DataSourceMode.EnterManually;
            window.UpdateLayout();

            // The inverse: the strip leaves the visible set on the manual path.
            var manage = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Manage input →");
            Assert.False(manage.IsEffectivelyVisible);

            Save(window, "phase-9a-manual-mode.png");
        }
        finally
        {
            window.Close();
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static MainViewModel RequireMain(Window window)
        => window.DataContext as MainViewModel
           ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var root = FindRepoRoot(AppContext.BaseDirectory);
        var shotDir = Path.Combine(root, "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var path = Path.Combine(shotDir, fileName);
        frame.Save(path);

        Assert.True(File.Exists(path), $"screenshot missing: {path}");
        Assert.True(new FileInfo(path).Length >= 256,
            $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
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

    private static string Sample(string fileName)
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
