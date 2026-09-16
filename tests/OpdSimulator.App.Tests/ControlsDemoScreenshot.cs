using System;
using System.IO;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 2 evidence: renders the live ControlsDemo showroom (all nine reusable
/// controls) inside a host window and saves it to
/// logs/screenshots/controls-demo.png for the §18 manual-verification entry
/// (AGENTS §18 — screenshots alongside the written pass record). Hosted in its
/// own window since Phase 3 (MainWindow became the TabControl shell).
/// </summary>
public class ControlsDemoScreenshot
{
    [AvaloniaFact]
    public void Render_ControlsDemo_SavesControlsDemoScreenshot()
    {
        var host = new Avalonia.Controls.Window
        {
            Width = 1000,
            Height = 760,
            Content = new ControlsDemo(),
        };
        host.Show();

        try
        {
            var frame = host.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "controls-demo.png");
            frame.Save(path);

            const int minBytes = 256;
            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= minBytes,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
        }
        finally
        {
            host.Close();
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