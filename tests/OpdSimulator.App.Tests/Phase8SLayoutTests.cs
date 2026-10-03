namespace OpdSimulator.App.Tests;

using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.ViewModels;
using Xunit;

/// <summary>Phase 8S issue 2 — the pinned rows must not starve the scrolling middle.</summary>
public sealed class Phase8SLayoutTests
{
    private static (ScrollViewer Scroller, Border Trace, Control Header) Parts(Window w)
    {
        var scroller = w.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "WidgetScroller");
        var host = (Panel)scroller.GetVisualParent()!;
        var trace = host.Children.OfType<Border>()
            .First(b => b.Classes.Contains("results-trace-panel"));

        // Row 0 is whatever else the pinned grid holds. Named by position rather than
        // by type, because the header's internal layout is not this test's business.
        var header = host.Children
            .First(c => !ReferenceEquals(c, scroller) && !ReferenceEquals(c, trace));

        return (scroller, trace, header);
    }

    /// <summary>A control's top edge in window coordinates — what the user actually sees.</summary>
    private static double TopInWindow(Control c, Window w) =>
        c.TranslatePoint(new Point(0, 0), w)!.Value.Y;

    /// <summary>A control's bottom edge in window coordinates.</summary>
    private static double BottomInWindow(Control c, Window w) =>
        c.TranslatePoint(new Point(0, c.Bounds.Height), w)!.Value.Y;

    [AvaloniaTheory]
    [InlineData(760)]
    [InlineData(560)]
    [InlineData(420)]
    public void ResultsLayout_MiddleRowKeepsAUsableHeightAtEveryWindowSize(int height)
    {
        var vm = ResultsPanelBufferTraceTests.CompletedRun();
        var window = new Window { Width = 1200, Height = height, Content = new Views.ResultsPanel { DataContext = vm } };
        window.Show();
        window.UpdateLayout();
        try
        {
            var (scroller, trace, header) = Parts(window);

            Assert.True(
                scroller.Bounds.Height >= 120,
                $"at {height}px the scrolling middle was {scroller.Bounds.Height:F0}px — under the 120px floor");

            // The owner's report was that the header and the trace overlay the middle.
            // Measured in window coordinates, the three pinned rows tile instead: each
            // one begins where the previous ended, and the trace stays inside the
            // window. Position, not visibility flags — a row laid out off-screen still
            // reports IsVisible=True (D-169).
            Assert.Equal(BottomInWindow(header, window), TopInWindow(scroller, window), 1);
            Assert.Equal(BottomInWindow(scroller, window), TopInWindow(trace, window), 1);
            Assert.True(
                BottomInWindow(trace, window) <= window.Bounds.Height + 0.5,
                $"at {height}px the trace ends at y={BottomInWindow(trace, window):F1} in a " +
                $"{window.Bounds.Height:F1}px window — it is laid out off-screen");

            // And the middle still scrolls rather than hiding its overflow.
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                $"at {height}px the content ({scroller.Extent.Height:F0}px) fits the viewport " +
                $"({scroller.Viewport.Height:F0}px) — nothing to scroll");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ResultsLayout_TracePanelCeilingLeavesTheMiddleRoomAtAShortWindow()
    {
        // The specific starvation the owner saw: a 240 px ceiling left 101 px of
        // middle at 420 px. 208 px is the new ceiling.
        var vm = ResultsPanelBufferTraceTests.CompletedRun();
        var window = new Window { Width = 1200, Height = 420, Content = new Views.ResultsPanel { DataContext = vm } };
        window.Show();
        window.UpdateLayout();
        try
        {
            var (_, trace, _) = Parts(window);

            Assert.True(
                trace.Bounds.Height <= 208.5,
                $"the trace panel is {trace.Bounds.Height:F0}px tall, over its 208px ceiling");
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
