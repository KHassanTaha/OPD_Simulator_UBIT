using System;
using System.IO;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 3 evidence: renders the shell MainWindow (header + TabControl with
/// Simulation / Input Analysis / Token Generator / Help) through Avalonia's
/// headless pipeline and saves the frame to logs/screenshots/phase-3-shell.png
/// (AGENTS §18 screenshot evidence).
/// </summary>
public class Phase3Screenshot
{
    [AvaloniaFact]
    public void Render_MainWindow_SavesPhase3ShellScreenshot()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-3-shell.png");
            frame.Save(path);

            const int minBytes = 256;
            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= minBytes,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
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