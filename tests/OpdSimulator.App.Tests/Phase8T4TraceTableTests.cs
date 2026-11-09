using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Trace;
using Xunit;

using TraceEventType = OpdSimulator.Core.Trace.TraceEventType;
using TraceLevel = OpdSimulator.Core.Trace.TraceLevel;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8T.4 — the event trace becomes a virtualised table of structured
/// events and the refused-arrival note (FR-UI-39 + FR-STAT-10 + NFR-11, D-202).
/// </summary>
/// <remarks>
/// The rows are projected from <see cref="TraceEvent"/>; a test that asserted
/// only "the table has rows" would pass on a table built by splitting the
/// formatted text, which is the one implementation the requirement forbids. So
/// the tests pin the mapping of every column and pin virtualisation by counting
/// the realised row containers, not by trusting that a virtualising panel was
/// declared.
/// </remarks>
public class Phase8T4TraceTableTests
{
    private static WidgetPreferences ThrowawayPreferences() =>
        new(Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json"));

    private static RunOutcome Outcome(
        SimulationResult? result,
        IReadOnlyList<TraceEvent> events,
        params string[] lines) => new(
        result,
        Array.Empty<FitReport>(),
        lines,
        0.3,
        0.1,
        null)
        {
            TraceEvents = events,
        };

    /// <summary>The one scroll region for the whole results body (FR-UI-37).</summary>
    private static ScrollViewer ResultsScroller(Window window) =>
        window.GetVisualDescendants().OfType<ScrollViewer>()
            .Single(s => s.Name == "ResultsScroller");

    /// <summary>The trace table itself — an ItemsControl, never a nested scroller.</summary>
    private static ItemsControl TraceTable(Window window) =>
        window.GetVisualDescendants().OfType<ItemsControl>()
            .Single(c => c.Name == "TraceRowList");

    /// <summary>
    /// Scrolls the single results region so the trace's first row is in view.
    /// The trace is last in the sequence, so at offset 0 a virtualising panel
    /// has nothing to realise — the test must put the table on screen first.
    /// </summary>
    private static void ScrollToTrace(Window window)
    {
        var scroller = ResultsScroller(window);
        var trace = window.GetVisualDescendants().OfType<Border>()
            .First(b => b.Classes.Contains("results-trace-panel"));

        double topInContent =
            trace.TranslatePoint(new Point(0, 0), scroller)!.Value.Y + scroller.Offset.Y;
        scroller.Offset = new Vector(0, topInContent);
        window.UpdateLayout();
    }

    /// <summary>Rows the table actually built, identified by their data object.</summary>
    private static IReadOnlyList<ContentPresenter> RealisedRows(ItemsControl table) =>
        table.GetVisualDescendants().OfType<ContentPresenter>()
            .Where(p => p.Content is TraceRow)
            .ToList();

    [Fact]
    public void Rows_ProjectEveryStructuredField_AndNeverParseTheText()
    {
        var events = new[]
        {
            new TraceEvent(0, TraceEventType.Arrival, 1, "Reception", null, 1, null),
            new TraceEvent(0.25, TraceEventType.Rng, null, null, null, null, "seed=42"),
            new TraceEvent(0.5, TraceEventType.StartService, 1, "Reception", 0, 0, null),
        };
        var results = new ResultsPanelViewModel(ThrowawayPreferences());

        // Deliberately empty TraceLines: the table must be able to render from
        // the structured events alone. If it parsed the text, there is nothing
        // here to parse.
        results.CompleteRun(Outcome(new SimulationResult(), events));

        Assert.True(results.HasTraceRows);
        Assert.Collection(
            results.TraceRows,
            row =>
            {
                Assert.Equal("1", row.SerialNumber);
                Assert.Equal("0.000", row.Time);
                Assert.Equal("08:15:00", row.Clock);
                Assert.Equal("ARRIVAL", row.Type);
                Assert.Equal("P1", row.Patient);
                Assert.Equal("Reception", row.Stage);
                Assert.Equal("—", row.Server);
                Assert.Equal("1", row.Queue);
                Assert.Equal(string.Empty, row.Detail);
            },
            row =>
            {
                Assert.Equal("2", row.SerialNumber);
                Assert.Equal("RNG", row.Type);
                Assert.Equal("—", row.Patient);
                Assert.Equal("—", row.Stage);
                Assert.Equal("—", row.Server);
                Assert.Equal("—", row.Queue);
                Assert.Equal("seed=42", row.Detail);
            },
            row =>
            {
                Assert.Equal("3", row.SerialNumber);
                Assert.Equal("START_SVC", row.Type);
                Assert.Equal("0", row.Server);
                Assert.Equal("0", row.Queue);
            });
    }

    [Fact]
    public void NotAdmittedNote_NamesBothReasonsAndTheTotal()
    {
        var results = new ResultsPanelViewModel(ThrowawayPreferences());
        results.CompleteRun(Outcome(
            new SimulationResult { NotAdmittedArrivals = 3201 },
            Array.Empty<TraceEvent>()));

        Assert.True(results.HasNotAdmittedNote);
        Assert.Contains(3201.ToString("N0"), results.NotAdmittedNote, StringComparison.Ordinal);
        Assert.Contains("opening window", results.NotAdmittedNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("screening cap", results.NotAdmittedNote, StringComparison.OrdinalIgnoreCase);

        var none = new ResultsPanelViewModel(ThrowawayPreferences());
        none.CompleteRun(Outcome(new SimulationResult(), Array.Empty<TraceEvent>()));
        Assert.False(none.HasNotAdmittedNote);
    }

    [Fact]
    public void CollectionSink_KeepsEventsAndLinesInLockStep()
    {
        var sink = new CollectionTraceSink(TraceLevel.Standard);
        var arrival = new TraceEvent(0, TraceEventType.Arrival, 1, "Reception", null, 1, null);

        // RNG rows are suppressed below Debug, so they belong to neither list.
        sink.Write(arrival);
        sink.Write(new TraceEvent(0.1, TraceEventType.Rng, null, null, null, null, "u=0.5"));

        Assert.Single(sink.Lines);
        Assert.Single(sink.Events);
        Assert.Same(arrival, sink.Events[0]);
        Assert.Contains("ARRIVAL", sink.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TenThousandEvents_ProjectInUnderOneSecond()
    {
        var events = Enumerable.Range(0, 10_000)
            .Select(i => new TraceEvent(
                i * 0.1, TraceEventType.Arrival, i + 1, "Reception", null, i % 5, null))
            .ToArray();
        var results = new ResultsPanelViewModel(ThrowawayPreferences());

        var stopwatch = Stopwatch.StartNew();
        results.CompleteRun(Outcome(new SimulationResult(), events));
        stopwatch.Stop();

        Assert.Equal(10_000, results.TraceRows.Count);
        Assert.Equal("1", results.TraceRows[0].SerialNumber);
        Assert.Equal("10000", results.TraceRows[^1].SerialNumber);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 1000,
            $"projecting 10,000 trace events took {stopwatch.ElapsedMilliseconds} ms; NFR-11 allows 1 s");
    }

    [AvaloniaFact]
    public void ResultsPanel_EventTraceTable_PopulatesAfterRunShownWindow()
    {
        var vm = new ResultsPanelViewModel(ThrowawayPreferences());
        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = new ResultsPanel { DataContext = vm },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var events = Enumerable.Range(0, 10_000)
                .Select(i => new TraceEvent(
                    i * 0.1, TraceEventType.Arrival, i + 1, "Reception", null, i % 5, null))
                .ToArray();

            vm.CompleteRun(Outcome(new SimulationResult(), events));
            window.UpdateLayout();

            var table = TraceTable(window);
            Assert.True(table.IsEffectivelyVisible, "the trace table must be visible after a run");
            Assert.Equal(10_000, vm.TraceRows.Count);

            // The trace is last in the single sequence, so put it on screen before
            // asking a virtualising panel to realise anything.
            ScrollToTrace(window);

            // Rendered, not merely bound: realised containers are what the user
            // reads, and the D-194 defect proved a correct property can sit
            // behind a binding that never re-evaluated.
            var realised = RealisedRows(table);
            Assert.NotEmpty(realised);
            var rowText = string.Join(
                " ",
                realised[0].GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Select(t => t.Text ?? string.Empty));
            Assert.Contains("ARRIVAL", rowText, StringComparison.Ordinal);
            Assert.Contains("P1", rowText, StringComparison.Ordinal);

            // Header and row declare the same columns, so a column added to one
            // cannot silently misalign the other.
            var header = window.GetVisualDescendants().OfType<Grid>()
                .Single(g => g.Name == "TraceHeaderGrid");
            var rowGrid = realised[0].GetVisualDescendants().OfType<Grid>().First();
            Assert.Equal(header.ColumnDefinitions.Count, rowGrid.ColumnDefinitions.Count);

            // FR-UI-37: exactly one scrollbar for the results body. The trace
            // must not own a ScrollViewer of its own.
            Assert.Empty(table.GetVisualDescendants().OfType<ScrollViewer>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TenThousandRowTrace_RealisesOnlyTheVisibleRows()
    {
        var vm = new ResultsPanelViewModel(ThrowawayPreferences());
        var window = new Window
        {
            Width = 1200,
            Height = 760,
            Content = new ResultsPanel { DataContext = vm },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var events = Enumerable.Range(0, 10_000)
                .Select(i => new TraceEvent(
                    i * 0.1, TraceEventType.Arrival, i + 1, "Reception", null, i % 5, null))
                .ToArray();
            vm.CompleteRun(Outcome(new SimulationResult(), events));
            window.UpdateLayout();

            var table = TraceTable(window);
            ScrollToTrace(window);
            int realised = RealisedRows(table).Count;

            Assert.True(
                realised > 0,
                "no trace rows were realised after scrolling to the table — the test is not observing the table");
            Assert.True(
                realised < 200,
                $"the table realised {realised} of 10,000 rows — NFR-11 requires virtualisation, not one control per event");

            // And the single results region is the one that scrolls.
            var scroller = ResultsScroller(window);
            Assert.True(scroller.Extent.Height > scroller.Viewport.Height,
                "the trace must extend the body's scroll extent, not scroll inside itself");
        }
        finally
        {
            window.Close();
        }
    }
}
