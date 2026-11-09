namespace OpdSimulator.App.Tests;

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.ViewModels;
using Xunit;

/// <summary>
/// Phase 8T.5 — the results panel is one inline sequence (FR-UI-37, D-200).
/// </summary>
/// <remarks>
/// The Phase 8S tests in this file measured the withdrawn <c>Auto,*,Auto</c>
/// rows: a 120 px floor on the scrolling middle, exact tiling between three
/// pinned rows, and a 200 px ceiling on the trace. FR-UI-37 withdraws all
/// three, so these tests are replaced rather than repaired — a test that
/// measured the ceiling would now pass against the very layout the phase
/// removes.
/// </remarks>
public sealed class Phase8SLayoutTests
{
    private static ScrollViewer Scroller(Window w) =>
        w.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Name == "ResultsScroller");

    private static Border Trace(Window w) =>
        w.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("results-trace-panel"));

    private static TextBlock Header(Window w, ResultsPanelViewModel vm) =>
        w.GetVisualDescendants().OfType<TextBlock>()
            .First(t => t.Text == vm.RunSummary);

    /// <summary>A control's top edge in the scroller's content coordinates.</summary>
    private static double TopInContent(Control c, ScrollViewer s) =>
        c.TranslatePoint(new Point(0, 0), s)!.Value.Y + s.Offset.Y;

    [AvaloniaTheory]
    [InlineData(760)]
    [InlineData(560)]
    [InlineData(420)]
    public void ResultsSequence_IsOrderedHeaderThenWidgetsThenTrace(int height)
    {
        var vm = ResultsPanelBufferTraceTests.CompletedRun();
        var window = new Window { Width = 1200, Height = height, Content = new Views.ResultsPanel { DataContext = vm } };
        window.Show();
        window.UpdateLayout();
        try
        {
            var scroller = Scroller(window);
            var trace = Trace(window);
            var header = Header(window, vm);

            // One scroll region: the trace owns no ScrollViewer of its own, so it
            // cannot scroll independently of the widgets above it (FR-UI-37).
            Assert.Empty(trace.GetVisualDescendants().OfType<ScrollViewer>());

            // Measured in the scroller's content coordinates: the claim is about
            // how the sequence is built, not about where the current scroll
            // offset happens to put each item on screen.
            double headerTop = TopInContent(header, scroller);
            double traceTop = TopInContent(trace, scroller);
            var metrics = window.GetVisualDescendants().OfType<TextBlock>()
                .First(t => t.Text == "Performance Measures");
            double metricsTop = TopInContent(metrics, scroller);

            Assert.True(
                headerTop < metricsTop,
                $"at {height}px the header sits at {headerTop:F0} and the widgets at {metricsTop:F0} — the header is not first");
            Assert.True(
                metricsTop < traceTop,
                $"at {height}px the widgets sit at {metricsTop:F0} and the trace at {traceTop:F0} — the trace is not last");

            // A short window scrolls the whole sequence rather than crushing one
            // region: there is no region left to crush.
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                $"at {height}px the content ({scroller.Extent.Height:F0}px) fits the viewport "
                    + $"({scroller.Viewport.Height:F0}px) — nothing to scroll");

            // The trace is its content, not a fixed-height window into it. At
            // 420 px the old pinned box could only show 200 px; the inline table
            // is hundreds of pixels tall and the body scrolls through it.
            Assert.True(
                trace.Bounds.Height > 208.5,
                $"at {height}px the trace is {trace.Bounds.Height:F0}px tall — it is still clamped to the withdrawn ceiling");

            // The sequence ends with the real bottom buffer, and the end of the
            // content can travel clear of the panel's bottom edge (D-198).
            var buffer = window.GetVisualDescendants().OfType<Border>()
                .Single(b => b.Name == "ResultsBottomBuffer");
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            window.UpdateLayout();

            double bufferBottom = buffer
                .TranslatePoint(new Point(0, buffer.Bounds.Height), window)!.Value.Y;
            Assert.True(
                bufferBottom <= window.Bounds.Height + 0.5,
                $"at {height}px the bottom buffer ends at y={bufferBottom:F0} in a "
                    + $"{window.Bounds.Height:F0}px window");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsSequence_TraceIsNotClampedToTheOldCeilingAtAShortWindow()
    {
        // The specific starvation the owner saw: the pinned trace had a 200 px
        // ceiling at 420 px. Phase 8T.5 deletes the ceiling along with the row;
        // this asserts the deletion, and that the table is still inside the one
        // scroller rather than outside it.
        var vm = ResultsPanelBufferTraceTests.CompletedRun();
        var window = new Window { Width = 1200, Height = 420, Content = new Views.ResultsPanel { DataContext = vm } };
        window.Show();
        window.UpdateLayout();
        try
        {
            var scroller = Scroller(window);
            var trace = Trace(window);

            Assert.True(
                trace.Bounds.Height > 208.5,
                $"the trace panel is {trace.Bounds.Height:F0}px tall — the withdrawn ceiling is still in force");
            Assert.Contains(
                scroller,
                trace.GetVisualAncestors().OfType<ScrollViewer>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OverviewMetrics_ValuesSitBesideTheirLabels()
    {
        // Issue 1. System totals is a label:value list, so each value belongs beside
        // its label. It was a * column with Right alignment, which pushed every value
        // to the panel's far right edge — roughly 1,000 px from the label it belongs
        // to. D-187 right-aligns numeric columns so decimals line up, but that only
        // pays off in a shared column; these rows are independent Grids, so the values
        // never aligned with each other either. An Auto column keeps each value next
        // to its own label.
        var vm = ResultsPanelBufferTraceTests.CompletedRun();
        var window = new Window { Width = 1200, Height = 760, Content = new Views.ResultsPanel { DataContext = vm } };
        window.Show();
        window.UpdateLayout();
        try
        {
            var rows = MetricRows(window, vm);
            Assert.NotEmpty(rows);

            foreach (var (label, value) in rows)
            {
                // Where the app itself says the label column ends. Read from the live
                // layout rather than hardcoded, so this test cannot drift from the
                // value it is checking (D-166).
                var grid = (Grid)value.Parent!;
                var expected = grid.ColumnDefinitions[0].ActualWidth + grid.ColumnSpacing + 1;

                Assert.True(
                    RenderedTextStart(value) <= expected,
                    $"'{label.Text}': its value starts at x={RenderedTextStart(value):F0}, but the " +
                    $"label column ends at x={expected - 1:F0} — the value is not beside its label");

                // And the cell hugs its text. If it stretched, the alignment inside it
                // would decide where the digits land, which is the defect itself. The
                // slack is a few pixels of rounding, not a layout allowance: a
                // stretched cell measured ~990px for the same text.
                Assert.True(
                    value.Bounds.Width <= value.TextLayout.Width + 8,
                    $"'{label.Text}': the value cell is {value.Bounds.Width:F0}px wide for " +
                    $"{value.TextLayout.Width:F0}px of text — it is stretching across the panel");
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Where a TextBlock's glyphs actually begin, in its own coordinates.
    /// </summary>
    /// <remarks>
    /// <c>Bounds</c> is the element's box, not its text. A right-aligned TextBlock in
    /// a * column has an identical box either way — the digits move inside it — so
    /// measuring the box is precisely the mistake this test must not make. It is the
    /// whole of the defect: the value sat a thousand pixels from its label while every
    /// bound in the tree said the cells were adjacent (D-187, D-186).
    /// </remarks>
    private static double RenderedTextStart(TextBlock t)
    {
        var slack = Math.Max(0, t.Bounds.Width - t.TextLayout.Width);

        var offset = t.TextAlignment switch
        {
            Avalonia.Media.TextAlignment.Right => slack,
            Avalonia.Media.TextAlignment.Center => slack / 2,
            _ => 0,
        };

        return t.Bounds.X + offset;
    }

    /// <summary>
    /// The System totals label:value pairs, found via the ItemsControl that is
    /// actually bound to <c>vm.SystemMetrics</c>. Anchoring on the data source rather
    /// than on a column definition keeps this test honest if the layout changes for a
    /// reason that has nothing to do with alignment.
    /// </summary>
    private static List<(TextBlock Label, TextBlock Value)> MetricRows(Window w, ResultsPanelViewModel vm)
    {
        var host = w.GetVisualDescendants().OfType<ItemsControl>()
            .Single(c => ReferenceEquals(c.ItemsSource, vm.SystemMetrics));

        var list = new List<(TextBlock, TextBlock)>();
        foreach (var grid in host.GetVisualDescendants().OfType<Grid>())
        {
            var cells = grid.Children.OfType<TextBlock>().ToList();
            if (cells.Count == 2)
            {
                list.Add((cells[0], cells[1]));
            }
        }

        return list;
    }
}
