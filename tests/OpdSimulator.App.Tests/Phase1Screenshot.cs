using System;
using System.IO;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 1 evidence: renders the real MainWindow (XAML + Theme.axaml applied)
/// through Avalonia's headless pipeline and saves the frame to
/// logs/screenshots/phase-1-window.png (AGENTS §18 screenshot evidence).
/// The path is gitignored; the frame is also asserted non-empty as a
/// render-regression check.
/// </summary>
public class Phase1Screenshot
{
    [AvaloniaFact]
    public void Render_MainWindow_SavesPhase1Screenshot()
    {
        var window = new MainWindow();
        window.Show();

        var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("headless pipeline produced no frame");

        var root = FindRepoRoot(AppContext.BaseDirectory);
        var shotDir = Path.Combine(root, "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var path = Path.Combine(shotDir, "phase-1-window.png");

        frame.Save(path);

        const int minBytes = 256;
        Assert.True(File.Exists(path), $"screenshot missing: {path}");
        Assert.True(new FileInfo(path).Length >= minBytes,
            $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");

        window.Close();
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