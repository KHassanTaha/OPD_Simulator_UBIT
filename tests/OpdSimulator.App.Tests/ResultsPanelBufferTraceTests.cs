using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.4 gate — the Results panel's bottom buffer and the always-visible event
/// trace (FR-UI-35, D-184).
/// </summary>
/// <remarks>
/// <para>
/// The trace was moved OUT of the panel's <c>ScrollViewer</c> into grid row 2 as a
/// sibling, the same shape D-170 gave the calculations dialog's footer. So the
/// tests here are mostly about <b>position</b>, not about visibility flags:
/// <c>IsVisible</c> is a flag check that returns <c>True</c> for a control sitting
/// outside the window (the D-169 miss), which is exactly the failure mode a
/// "pinned" panel can have while every flag says it is fine. Where a position
/// matters, the assertion measures it.
/// </para>
/// <para>
/// The buffer is a trailing <c>Border</c> inside the scrollable content rather than
/// <c>ScrollViewer.Padding</c>, because padding on a ScrollViewer sits outside the
/// scroll extent (D-142): it shrinks the viewport instead of extending the
/// content, so the last widget still cannot travel clear.
/// </para>
/// </remarks>
public class ResultsPanelBufferTraceTests
{
    /// <summary>Hosts a Results panel in a plain window at the app's declared size.</summary>
    /// <remarks>
    /// Deliberately NOT <c>MainWindow</c>/<c>MainViewModel</c>: that constructor calls
    /// <c>WidgetPreferences.Load()</c> with no argument, which resolves to the real
    /// per-user <c>ui.json</c>. A structural test that read — and then wrote, via any
    /// visibility change — the developer's own preferences file would be a test that
    /// depends on and corrupts machine state.
    /// </remarks>
    private static Window Host(ResultsPanelViewModel results)
    {
        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = new ResultsPanel { DataContext = results },
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    /// <summary>Builds a real completed run so the panel has metrics and trace lines.</summary>
    /// <summary>
    /// A real completed run. Internal rather than private so the Phase 8S layout
    /// tests can measure a populated panel without duplicating the run setup.
    /// </summary>
    internal static ResultsPanelViewModel CompletedRun()
    {
        var results = new ResultsPanelViewModel(ThrowawayPreferences());
        results.StartRun();
        results.CompleteRun(CompletedOutcome());
        return results;
    }

    /// <summary>
    /// A throwaway preferences path, so the FR-UI-14 defaults are the ones under test
    /// rather than whatever the machine last saved (D-186: never touch real state).
    /// </summary>
    private static WidgetPreferences ThrowawayPreferences() =>
        new(Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json"));

    /// <summary>
    /// One real three-stage run through the coordinator, with a throwaway
    /// preferences file so the defaults are the ones under test.
    /// </summary>
    private static RunOutcome CompletedOutcome()
    {
        var sample = Path.Combine(
            FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv");
        var binding = DataAnalyzer.Analyze(sample);
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.5, 0.25, 0.2";
        for (int i = 0; i < 3 && i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = new[] { "1", "2", "2" }[i];
        }

        var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        return outcome;
    }

    /// <summary>
    /// The trace body as the user sees it: the rendered TextBlock inside the pinned
    /// panel. Reading <c>vm.TraceBody</c> instead would pass under the D-194 defect,
    /// because the property is correct while the binding is stale.
    /// </summary>
    private static string RenderedTraceText(Window window) =>
        window.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Single(t => t.Name == "TraceBodyText")
            .Text ?? string.Empty;

    [AvaloniaFact]
    public void ResultsPanel_BottomBuffer_LastWidgetFullyVisible()
    {
        var vm = CompletedRun();
        var window = Host(vm);
        try
        {

            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            var buffer = panel.GetVisualDescendants().OfType<Border>()
                .Single(b => b.Name == "ResultsBottomBuffer");

            // The buffer is a trailing element of the scrollable content, so the
            // extent it reports must include it. If this fails, the buffer is being
            // expressed as padding or a margin and the last widget cannot clear.
            var scroller = WidgetScroller(window);
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                "precondition: the widget area must overflow for the buffer to mean anything");

            // It must be the LAST child of the content, or widgets render below it.
            var content = buffer.GetVisualParent() as Panel;
            Assert.NotNull(content);
            Assert.Same(buffer, content!.Children[^1]);

            // Its height is the SpaceXl token (32), so it restyles with the theme.
            Assert.Equal(32d, buffer.Bounds.Height);

            // Scroll to the true maximum, letting the layout settle (D-169: setting
            // Offset and asserting immediately measures the pre-scroll layout).
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            for (int pass = 0; pass < 3; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            Assert.True(
                scroller.Offset.Y > 0,
                $"the body did not scroll: offset stayed at {scroller.Offset.Y}");

            // The real claim: the widget immediately above the buffer is FULLY inside
            // the viewport once scrolled to the end. Measured, not inferred from a
            // visibility flag.
            var lastWidget = (Control)content.Children[^2];
            var top = lastWidget.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
            var bottom = lastWidget.TranslatePoint(
                new Point(0, lastWidget.Bounds.Height), scroller)!.Value.Y;

            Assert.True(
                top >= 0 && bottom <= scroller.Viewport.Height + 0.5,
                $"the last widget spans {top:F1}..{bottom:F1} in a viewport of "
                    + $"{scroller.Viewport.Height:F1} — the buffer is too small to clear it");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The pinned trace panel, on screen, showing the run that has just finished.
    /// </summary>
    /// <remarks>
    /// This test was reversed as part of Phase 8S. It used to build a completed run
    /// and only then show the window, which meant the trace binding was first
    /// evaluated against populated state — a state the app never reaches, because the
    /// window is on screen from launch and the run arrives afterwards. It passed
    /// against a real defect: the trace recorded 9,471 characters and the screen kept
    /// saying "No trace was recorded for this run." forever (D-194, D-166's family).
    /// It now shows the window empty and runs afterwards.
    /// </remarks>
    [AvaloniaFact]
    public void ResultsPanel_EventTrace_PopulatesAfterRunShownWindow()
    {
        var vm = new ResultsPanelViewModel(new WidgetPreferences(Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json")));
        var window = Host(vm);
        try
        {
            // Before any run: the panel says why it is empty. Asserted so the test
            // cannot pass by the binding being absent altogether.
            Assert.Equal("No trace was recorded for this run.", vm.TraceBody);

            vm.CompleteRun(CompletedOutcome());
            window.UpdateLayout();

            // "Always visible" has to mean ON SCREEN, not merely bound. A pinned
            // panel can be laid out below the window's bottom edge while every
            // IsVisible flag reads True — the D-169 failure, verbatim.
            var trace = TracePanel(window);
            Assert.True(trace.IsEffectivelyVisible, "the trace panel must be visible after a run");

            var host = window.Bounds.Height;
            var top = trace.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            var bottom = trace.TranslatePoint(
                new Point(0, trace.Bounds.Height), window)!.Value.Y;

            Assert.True(top >= 0, $"the trace panel starts at y={top:F1}, above the window top");
            Assert.True(
                bottom <= host + 0.5,
                $"the trace panel ends at y={bottom:F1} in a {host:F1}px window — it is laid out off-screen");

            // And it is not inside the scrolling body: a trace that scrolls away is
            // not a trace you can read while reading anything else (D-170).
            var scroller = WidgetScroller(window);
            Assert.False(
                trace.GetVisualAncestors().OfType<ScrollViewer>().Contains(scroller),
                "the trace must be a sibling of the widget ScrollViewer, not a child of it");

            // Its RENDERED content is the run's own trace. Asserted on the TextBlock
            // because that is what the user reads: the view-model property can be
            // correct while the binding never re-evaluates, which is exactly the bug
            // this test now exists to catch (D-194).
            Assert.Contains("ARRIVAL", RenderedTraceText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EventTrace_FirstRow_IsTheFirstArrival()
    {
        // The recorder's first line is the RNG seed row at Standard and below, so the
        // first ARRIVAL is the first patient through the door. Asserted on the
        // rendered panel, in simulation order, because "the trace has content" is not
        // the requirement — the trace STARTS at the arrival is.
        var vm = new ResultsPanelViewModel(ThrowawayPreferences());
        var window = Host(vm);
        try
        {
            vm.CompleteRun(CompletedOutcome());
            window.UpdateLayout();

            var rows = RenderedTraceRows(window);
            Assert.NotEmpty(rows);

            var firstArrival = rows.First(r => r.Contains("ARRIVAL"));
            Assert.Equal("P1", PatientId(firstArrival));
            Assert.Contains("Reception", firstArrival, StringComparison.Ordinal);
            Assert.Contains("08:15:00", firstArrival, StringComparison.Ordinal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EventTrace_LastRow_IsTheLastServiceCompletion()
    {
        // The trace must END on a service completion, not on a queue join or a
        // routing row: the last thing the clinic does is finish serving someone.
        var vm = new ResultsPanelViewModel(ThrowawayPreferences());
        var window = Host(vm);
        try
        {
            vm.CompleteRun(CompletedOutcome());
            window.UpdateLayout();

            var rows = RenderedTraceRows(window);
            Assert.NotEmpty(rows);

            // The final non-blank row carries a completion marker for the stage that
            // finished last.
            var last = rows.Last(r => r.Trim().Length > 0);
            Assert.True(
                last.Contains("END") || last.Contains("EXIT"),
                $"the trace's last row should be a service completion or exit, but was: {last}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The trace body split into its rendered rows, in order.</summary>
    private static string[] RenderedTraceRows(Window window) =>
        RenderedTraceText(window).Split(
            new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The patient id column of a rendered trace row ("P12"), or null.</summary>
    private static string? PatientId(string row)
    {
        var parts = row.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(p => p.Length > 1 && p[0] == 'P' && char.IsDigit(p[1]));
    }

    [AvaloniaFact]
    public void ResultsPanel_EventTrace_NotInCustomiseSelector()
    {
        var vm = CompletedRun();
        var window = Host(vm);
        try
        {

            // Open the picker the way a user does: CustomiseToggle is a ToggleButton
            // and the panel opens the picker from its Click handler, not from
            // IsChecked. Setting the flag alone leaves the picker closed and the test
            // would assert against a hidden border.
            var toggle = window.GetVisualDescendants().OfType<ToggleButton>()
                .Single(c => c.Name == "CustomiseToggle");
            toggle.IsChecked = true;
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
            window.UpdateLayout();

            var picker = window.GetVisualDescendants().OfType<Border>()
                .Single(b => b.Name == "WidgetPicker");
            Assert.True(picker.IsVisible, "precondition: the picker must be open");

            var labels = picker.GetVisualDescendants().OfType<CheckBox>()
                .Select(c => c.Content?.ToString() ?? string.Empty)
                .ToArray();

            Assert.Equal(7, labels.Length);
            Assert.DoesNotContain(
                labels,
                l => l.Contains("trace", StringComparison.OrdinalIgnoreCase));

            // And the trace stays visible with the picker open — it is not one of
            // the things the picker controls.
            Assert.True(TracePanel(window).IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsPanel_EventTrace_HasFixedMaxHeight()
    {
        var vm = CompletedRun();
        var window = Host(vm);
        try
        {

            // Phase 8S (D-195): the ceiling is 208 px, not the original 240. At
            // 240 the pinned box left the scrolling middle only 101 px in a 420 px
            // window, which the owner reported as the header and trace crowding the
            // content. Asserted as a real bound: a MaxHeight that never bites leaves
            // the panel growing unbounded and pushing the widgets off.
            var trace = TracePanel(window);
            Assert.Equal(208d, trace.MaxHeight);

            // With a long trace the panel must clamp to it, not exceed it.
            var host = window.Bounds.Height;
            var bottom = trace.TranslatePoint(
                new Point(0, trace.Bounds.Height), window)!.Value.Y;
            Assert.True(
                trace.Bounds.Height <= 208.5,
                $"the trace panel rendered at {trace.Bounds.Height:F1}px, above its 208px ceiling");
            Assert.True(
                bottom <= host + 0.5,
                $"the trace panel ends at y={bottom:F1} in a {host:F1}px window");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsPanel_EventTrace_InternallyScrollable()
    {
        var vm = CompletedRun();
        var window = Host(vm);
        try
        {

            var trace = TracePanel(window);
            var inner = trace.GetVisualDescendants().OfType<ScrollViewer>().Single();

            // The trace must be able to scroll INSIDE its own box. Without this the
            // outer panel is the only scroller and a long trace pushes the widgets
            // away instead of scrolling in place.
            Assert.True(
                inner.Extent.Height > inner.Viewport.Height,
                $"the trace does not scroll in place: extent {inner.Extent.Height:F1} "
                    + $"vs viewport {inner.Viewport.Height:F1}");

            // Vertical scrolling is offered; the mono lines are long and need
            // horizontal too.
            Assert.Equal(ScrollBarVisibility.Auto, inner.VerticalScrollBarVisibility);
            Assert.Equal(ScrollBarVisibility.Auto, inner.HorizontalScrollBarVisibility);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void WidgetPreferences_Load_RebindsToTheFileItRead()
    {
        // Regression for D-185. JsonSerializer satisfies the public parameterless
        // constructor, which chains to the DEFAULT path, so a deserialised
        // preferences object used to write to ~/.config/OpdSimulator/ui.json no
        // matter which file was read. That silently overwrote the developer's real
        // settings from tests using a temp file, and made the caller's file look
        // unchanged when it had in fact been abandoned. The property that matters is
        // "writes land on the file this instance was loaded from".
        string dir = Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "ui.json");
        try
        {
            File.WriteAllText(file, """
                { "VisibleWidgets": [ "metrics", "chiSquare", "trace", "utilisation" ],
                  "CollapsedSections": [] }
                """);

            var prefs = WidgetPreferences.Load(file);
            Assert.Equal(4, prefs.VisibleWidgets.Count);

            prefs.VisibleWidgets.Remove("trace");
            prefs.Save();

            Assert.Equal(3, WidgetPreferences.Load(file).VisibleWidgets.Count);
            Assert.DoesNotContain("trace", WidgetPreferences.Load(file).VisibleWidgets);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void WidgetPreferences_OldUiJsonWithTraceKey_DoesNotThrow()
    {
        // An ui.json written before 8Q.4 still lists "trace". The honest property is
        // not "does not throw" — VisibleWidgets is a List<string>, so no string can
        // make deserialisation fail, and the owner's original framing (a "trace enum
        // key" that could throw) described a risk that never existed. The property
        // that matters is that the stale key is IGNORED: it must not keep the trace
        // hidden, and it must not come back as a toggleable widget.
        string dir = Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, "ui.json");
        try
        {
            File.WriteAllText(file, """
                {
                  "VisibleWidgets": [ "metrics", "chiSquare", "trace", "utilisation" ],
                  "CollapsedSections": []
                }
                """);

            var prefs = WidgetPreferences.Load(file);
            Assert.Equal(4, prefs.VisibleWidgets.Count);
            Assert.Contains("trace", prefs.VisibleWidgets);

            // The stale key survives the LOAD — that is deliberate, so nothing is lost
            // from the user's file — but it must not influence the panel.
            var panel = new ResultsPanelViewModel(prefs);
            Assert.True(
                panel.ShowTrace,
                "a ui.json naming \"trace\" must not leave the trace hidden (FR-UI-35)");

            // And the trace is not among the toggleable widgets, whatever the file says.
            Assert.DoesNotContain("trace", panel.VisibleWidgets);
            Assert.Equal(7, panel.VisibleWidgets.Count);

            // Toggling anything rewrites the list without the stale key, so an old
            // file heals instead of carrying dead data forever.
            // Toggling anything rewrites the list without the stale key, so an old
            // file heals instead of carrying dead data forever. Asserted against the
            // file on disk, not the in-memory object: D-185 is precisely the bug
            // where the object updated and the file did not.
            panel.ToggleWidget("chiSquare");
            Assert.DoesNotContain("trace", WidgetPreferences.Load(file).VisibleWidgets);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The panel's own scrolling body (grid row 1). Named in the XAML rather than
    /// guessed from the visual tree: there are now two ScrollViewers in this panel,
    /// and "the one containing the widgets" is a description that silently picks the
    /// wrong one the day the trace is restyled.
    /// </summary>
    private static ScrollViewer WidgetScroller(Window window) =>
        window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Name == "WidgetScroller");

    /// <summary>The pinned trace panel: the bordered container in grid row 2.</summary>
    private static Border TracePanel(Window window)
    {
        var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
        var scroller = WidgetScroller(window);

        // The sibling that sits AFTER the widget ScrollViewer is the trace panel.
        var host = scroller.GetVisualParent() as Panel;
        Assert.NotNull(host);
        var index = host!.Children.IndexOf(scroller);
        Assert.True(
            index >= 0 && index + 1 < host.Children.Count,
            "the trace panel must follow the widget ScrollViewer in grid row 2");

        var trace = host.Children[index + 1] as Border;
        Assert.NotNull(trace);
        Assert.NotNull(panel);
        return trace!;
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