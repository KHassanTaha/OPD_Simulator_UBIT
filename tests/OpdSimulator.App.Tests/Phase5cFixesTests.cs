using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
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
/// Phase 5c fixes (feat/gui-rebuild gate). Verification intent: the results
/// column scrolls while the customise row stays pinned (5c.1), and the benign
/// Wayland "com.canonical.AppMenu.Registrar" DBus error is never treated as a
/// crash (5c.2, D-107). The 5c.3 trace-in-all-modes test lands with the Core
/// decision (frozen-Core conflict, flagged to the owner).
/// </summary>
public class Phase5cFixesTests
{
    [AvaloniaFact]
    public void WelcomeCard_VisibleOnFreshLaunch_AndHiddenAfterRun()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var vm = (MainViewModel)window.DataContext!;

            // FR-UI-5: on a fresh launch the welcome card shows the course
            // identity before any calculation is attempted. Guarded by the
            // Content binding on ResultsPanel's ContentControl — a template
            // without a Content element renders nothing.
            var welcome = window.GetVisualDescendants().OfType<WelcomeCard>().SingleOrDefault();
            Assert.NotNull(welcome);
            Assert.True(welcome!.IsEffectivelyVisible, "FR-UI-5: welcome card must be visible on fresh launch");

            window.UpdateLayout();
            Assert.True(welcome.IsEffectivelyVisible);

            // The moment a calculation starts it is replaced by the results.
            vm.Results.StartRun();
            window.UpdateLayout();
            Assert.False(welcome.IsEffectivelyVisible, "welcome card must hide once a run starts");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsPanel_ScrollViewer_ContainsAllWidgets()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var vm = (MainViewModel)window.DataContext!;
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();

            vm.Results.StartRun();
            vm.Results.CompleteRun(new RunOutcome(null, Array.Empty<Models.FitReport>(),
                new[] { "t=0 ARRIVAL patient=1 queueLen=1" }, SimulationCoordinator.DefaultExitProbability, null));
            window.UpdateLayout();

// The outer ScrollViewer is the one whose content carries BOTH the
            // metrics and the trace widgets (the trace widget's own nested
            // ScrollViewer only holds the trace text).
            var outer = panel.GetVisualDescendants().OfType<ScrollViewer>()
                .Where(s => s.Content is StackPanel)
                .OrderBy(s => s.GetVisualDescendants().Count())
                .Single(s =>
                    s.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "System totals")
                    && s.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Event trace"));

            // All three widgets live inside the scrolling area.
            var headers = outer.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text)
                .Where(t => t is not null)
                .ToList();
            Assert.Contains(headers, t => t == "System totals");
            Assert.Contains(headers, t => t!.StartsWith("Chi-square goodness-of-fit"));
            Assert.Contains(headers, t => t == "Event trace");

            // The customise toggle is pinned above the ScrollViewer, not inside it.
            var toggle = panel.GetVisualDescendants().OfType<ToggleButton>()
                .Single(t => t.Content?.ToString() == "Customise results");
            Assert.False(outer.IsVisualAncestorOf(toggle));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void TraceViewer_PopulatesAfterClinicDayRun()
    {
        // 5c.3 (D-110): the calendar Engine.Run overload forwards an optional
        // ITraceSink, so a ClinicDay run collects a trace exactly like the
        // DiagnosticTrace mode instead of leaving the widget empty.
        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.4";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4"; // one μ per default stage (3)
        var vm = new ResultsPanelViewModel(null);

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding: null);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.Equal(RunMode.ClinicDay, config.TryBuildRunParameters()!.RunMode);
        Assert.NotEmpty(outcome.TraceLines);
        Assert.Contains(outcome.TraceLines, line => line.Contains("ARRIVAL"));

        vm.StartRun();
        vm.CompleteRun(outcome);
        Assert.True(vm.TraceText.Contains("ARRIVAL"), "trace widget must show ClinicDay events");
    }

    [Fact]
    public void CrashReporter_IgnoresAppMenuRegistrarDBusError()
    {
        // The literal Wayland error (owner-reproduced on this Ubuntu box, D-107).
        var quirk = new Exception(
            "org.freedesktop.DBus.Error.ServiceUnknown: The name com.canonical.AppMenu.Registrar was not provided by any .service files");
        Assert.True(CrashReporter.IsIgnorableWaylandQuirk(quirk));

        // Wrapped in an AggregateException just like TaskScheduler.Unobserved
        // TaskException delivers it — the filter walks the inner chain.
        Assert.True(CrashReporter.IsIgnorableWaylandQuirk(new AggregateException("background task failed", quirk)));

        // The marker alone is NOT enough: an exception whose class is not the
        // DBus ServiceUnknown error stays a real crash.
        Assert.False(CrashReporter.IsIgnorableWaylandQuirk(
            new Exception("com.canonical.AppMenu.Registrar mentioned in unrelated code")));
        Assert.False(CrashReporter.IsIgnorableWaylandQuirk(new InvalidOperationException("real failure")));
    }

    [Fact]
    public void ClearAll_ResetsResultsPanel_ToWelcomeState()
    {
        // Phase 5c.4: Clear All is a FULL reset, not just a field clear. After
        // a completed run the results panel must return to its fresh-launch
        // state — welcome card back, every widget and the banner empty.
        var main = new MainViewModel();
        var config = main.Config;
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.4";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding: null);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome);
        Assert.True(main.Results.HasRun);
        Assert.NotEmpty(main.Results.TraceText);

        main.ResetAll();

        Assert.True(main.Results.IsWelcomeVisible, "welcome card must be back after Clear All");
        Assert.False(main.Results.HasRun);
        Assert.Equal(string.Empty, main.Results.TraceText);
        Assert.Null(main.Results.RunError);
        Assert.Empty(main.Results.SystemMetrics);
        Assert.Empty(main.Results.ChiSquareRows);
        // FR-UI-21: widget VISIBILITY is a persisted preference (it survives
        // Clear All); the reset contract is that widget CONTENT is dropped.
        // Phase 7D: the data preview now lives on the Input tab.
        Assert.Null(main.InputTab.Preview.Rows);
        Assert.Null(main.InputTab.Preview.ColumnTitles);
        Assert.Null(main.InputTab.Preview.LoadErrorSummary);
        Assert.False(main.InputTab.HasFile);

        // The config side also lands at factory ground.
        Assert.False(config.ParametersIsOptionalEnabled);
        Assert.Equal("No file loaded", config.DataStatus);
        Assert.Null(config.Binding);
    }

    [AvaloniaFact]
    public void ClearAll_UnloadsUploadedFile()
    {
        // Phase 5c.4: Clear All must drop the uploaded file and its fitted
        // parameters so the Data section returns to "No file loaded".
        var main = new MainViewModel();
        var root = FindRepoRoot(AppContext.BaseDirectory);
        var csv = Path.Combine(root, "samples", "sample_patients.csv");
        Assert.True(File.Exists(csv), $"sample missing: {csv}");

        main.Config.ApplyLoadedFile(csv);
        Assert.NotNull(main.Config.Binding);
        Assert.StartsWith("Loaded", main.Config.DataStatus);

        main.ResetAll();

        Assert.Equal("No file loaded", main.Config.DataStatus);
        Assert.Null(main.Config.LoadedFileName);
        Assert.Null(main.Config.Binding);
    }

    [AvaloniaFact]
    public void ClearAll_KeepsPinnedFooterVisible()
    {
        // Regression guard: the pinned footer carrying the reset button is
        // part of the panel shell, not run output, so it must survive a reset.
        var main = new MainViewModel();
        var host = new Window
        {
            Width = 900,
            Height = 700,
            Content = new ConfigPanel { DataContext = main.Config },
        };
        host.Show();

        try
        {
            host.UpdateLayout();
            var footer = host.GetVisualDescendants().OfType<PinnedFooterBar>().Single();
            Assert.True(footer.IsEffectivelyVisible, "pinned footer must be visible");

            main.ResetAll();
            host.UpdateLayout();

            Assert.True(footer.IsEffectivelyVisible, "pinned footer must stay visible after Clear All");
            Assert.True(host.GetVisualDescendants().OfType<Button>()
                .Any(b => b.Content?.ToString() == "Clear All"), "Clear All button must remain");
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