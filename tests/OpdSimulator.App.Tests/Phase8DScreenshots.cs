using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8D gate evidence (feat/milestone-7-model-driven) — the final-layout
/// walkthrough, run headlessly through the real <see cref="MainWindow"/> per the
/// D-089 method. The walkthrough covered by this test:
/// <list type="bullet">
/// <item>(a) launch the Simulation tab and run a manual diagnostic configuration
/// so the results panel is populated and shows the new group headings;</item>
/// <item>(b) confirm the trace-level dropdown offers the renamed levels
/// Minimal / Standard / Detailed / Debug (no None / Events / State / Rng);</item>
/// <item>(c) Collapse All — every configuration section collapses to its
/// header;</item>
/// <item>(d) Expand All — every section is restored, then the whole window is
/// saved to <c>logs/screenshots/phase-8d-final-layout.png</c>.</item>
/// </list>
/// </summary>
public class Phase8DScreenshots
{
    private static readonly string[] ExpectedHeadings =
    {
        "Overview",
        "Server Performance",
        "Charts",
        "Statistical Validation",
        "Simulation Verification",
        "Analytical Validation",
        "Event Trace",
    };

    [AvaloniaFact]
    public void Render_FinalLayout_SavePhase8dFinalLayoutPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 3200; // tall enough to lay out every grouped widget at once
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            // (a) A manual diagnostic run with a Standard trace, so the panel is
            // populated: metrics, utilisation, chi-square, verification,
            // analytical (guarded empty for a transient 1500-min run), charts,
            // and a non-empty event trace.
            main.Config.ParametersIsOptionalEnabled = true;
            main.Config.ManualLambda.Value = "0.1";
            main.Config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
            main.Config.AdvancedIsOptionalEnabled = true;
            main.Config.IsDiagnosticTrace = true;
            main.Config.TraceLevel = "Standard";
            main.Config.HorizonMinutes.Value = "1500";

            var outcome = SimulationCoordinator.Run(main.Config.TryBuildRunParameters()!, binding: null);
            Assert.Null(outcome.Error);
            Assert.NotNull(outcome.Result);
            main.Results.StartRun();
            main.Results.CompleteRun(outcome);

            // (b) The renamed trace levels are the only options.
            Assert.Equal(
                new[] { "Minimal", "Standard", "Detailed", "Debug" },
                main.Config.TraceLevels);

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
            tabs.SelectedIndex = 0; // Simulation
            window.UpdateLayout();

            // (c) Collapse All then (d) Expand All through the real commands.
            main.Config.CollapseAllCommand.Execute(null);
            window.UpdateLayout();
            Assert.False(main.Config.IsModelSectionExpanded);
            Assert.False(main.Config.IsParametersSectionExpanded);
            Assert.False(main.Config.IsStagesSectionExpanded);
            Assert.False(main.Config.IsHorizonSectionExpanded);
            Assert.False(main.Config.IsAdvancedSectionExpanded);

            main.Config.ExpandAllCommand.Execute(null);
            window.UpdateLayout();
            Assert.True(main.Config.IsModelSectionExpanded);
            Assert.True(main.Config.IsParametersSectionExpanded);
            Assert.True(main.Config.IsStagesSectionExpanded);
            Assert.True(main.Config.IsHorizonSectionExpanded);
            Assert.True(main.Config.IsAdvancedSectionExpanded);

            // The grouped results headings are present in the rendered frame.
            var rendered = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string heading in ExpectedHeadings)
            {
                Assert.Contains(heading, rendered);
            }

            var frame = HeadlessScreenshot.Capture(window);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8d-final-layout.png");
            frame.Save(shotPath);
            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "final-layout frame missing or suspiciously small");
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
