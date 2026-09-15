using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
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
}