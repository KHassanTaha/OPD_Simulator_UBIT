using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 5c evidence: the results column scrolls while the customise row stays
/// pinned (5c.1). Hosts a real <see cref="ResultsPanel"/> driven by its view
/// model with explicitly all-on widget preferences, so the frame is
/// deterministic regardless of the per-user ui.json on the machine (FR-UI-14).
/// A completed diagnostic run provides a long State trace — enough lines that
/// the content overflows its viewport and the outer ScrollViewer scrolls —
/// then saves the frame to logs/screenshots/phase-5c-results.png so the owner
/// can see the scrollbar and the populated trace widget (AGENTS §18).
/// </summary>
public class Phase5cScreenshot
{
    [AvaloniaFact]
    public void Render_ScrolledResults_SavesPhase5cScreenshot()
    {
        var config = new ConfigPanelViewModel();
        ConfigFixture(config);

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding: null);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.True(outcome.TraceLines.Count > 100, "a 1500-min State trace must be long");

        var prefs = new WidgetPreferences(Path.Combine(Path.GetTempPath(), $"opdsim-screenshot-{Guid.NewGuid():N}.json"));
        prefs.VisibleWidgets = new() { "metrics", "chiSquare", "trace" };
        var vm = new ResultsPanelViewModel(prefs);

        var host = new Window
        {
            Width = 920,
            Height = 660,
            Content = new ResultsPanel { DataContext = vm },
        };
        host.Show();

        try
        {
            vm.StartRun();
            vm.CompleteRun(outcome);
            host.UpdateLayout();

            // The outer ScrollViewer is the one whose content carries BOTH the
            // metrics and the trace widgets (the trace widget's own nested
            // ScrollViewer only holds the trace text).
            var outer = host.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Content is StackPanel
                    && s.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "System totals")
                    && s.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Event trace"));

            Assert.True(outer.Extent.Height > outer.Viewport.Height + 1,
                $"widget content must overflow the viewport (extent {outer.Extent.Height:0} > viewport {outer.Viewport.Height:0})");

            var frame = host.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-5c-results.png");
            frame.Save(path);

            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= 256,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
        }
        finally
        {
            host.Close();
        }
    }

    [AvaloniaFact]
    public void Render_FreshLaunchWelcome_SavesWelcomeSnapshot()
    {
        // FR-UI-5 evidence: on a fresh launch the results column shows the
        // welcome card (logos + course identity) before any run. Hosts the
        // ResultsPanelViewModel directly with all-on preferences so the frame
        // is deterministic regardless of the per-user ui.json (FR-UI-14).
        var prefs = new WidgetPreferences(Path.Combine(Path.GetTempPath(), $"opdsim-welcome-{Guid.NewGuid():N}.json"));
        prefs.VisibleWidgets = new() { "metrics", "chiSquare", "trace" };
        var welcomeVm = new WelcomeCardViewModel();

        var host = new Window
        {
            Width = 920,
            Height = 660,
            Content = new ResultsPanel { DataContext = new ResultsPanelViewModel(prefs) },
        };
        host.Show();

        try
        {
            host.UpdateLayout();

            var welcome = host.GetVisualDescendants().OfType<WelcomeCard>().SingleOrDefault();
            Assert.NotNull(welcome);
            Assert.True(welcome!.IsEffectivelyVisible, "FR-UI-5: welcome card must render on fresh launch");

            // The template fully instantiated: the members card with its
            // "Group members" heading is part of WelcomeCard.axaml.
            Assert.Contains(
                host.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text),
                t => t == "Group members");

            var frame = host.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-5c-welcome.png");
            frame.Save(path);

            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= 256,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>Same diagnostic-run configuration the Phase 5 gate screenshot used.</summary>
    internal static void ConfigFixture(ConfigPanelViewModel config)
    {
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        config.AdvancedIsOptionalEnabled = true;
        config.TraceLevel = "State";
        config.IsDiagnosticTrace = true;
        config.HorizonMinutes.Value = "1500";
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