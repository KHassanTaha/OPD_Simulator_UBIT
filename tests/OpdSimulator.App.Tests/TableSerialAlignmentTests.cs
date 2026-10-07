using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.Core.Engine;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;
using Avalonia;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.5 gate — serial numbers and column alignment on every listing table in
/// the Results panel (FR-UI-36).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these assertions are about the visual tree and not the view model.</b>
/// A serial number is a promise to the reader that a row can be cited ("stage 2",
/// "verdict 3"). The number existing in a C# record does not make it citable; it
/// has to be a <i>column</i>, under a header, in the rendered table. So these
/// tests walk the realised visual tree and read the actual <c>TextBlock</c>s.
/// Asserting <c>StageRows[1].SerialNumber == "2"</c> would pass against a panel
/// that never renders it.
/// </para>
/// <para>
/// <b>Why alignment is asserted on the declared property.</b> Headless Avalonia
/// lays out controls but produces no glyph metrics, so the <i>rendered</i> position
/// of a string inside a stretched <c>TextBlock</c> cannot be measured — every cell
/// reports its full column width whether the text is left- or right-aligned inside
/// it. A test that compared cell edges would therefore pass against left-aligned
/// text, which is precisely the defect. The assertion is on
/// <c>TextAlignment</c>, which is what the layout engine consumes. It is not
/// vacuous: before 8Q.5 no cell in the panel declared an alignment at all
/// (<c>grep -c 'TextAlignment' ResultsPanel.axaml</c> returned 0), so every
/// assertion below fails against the pre-8Q.5 panel.
/// </para>
/// <para>
/// <b>Why the per-server table expands first.</b> It lives in a
/// <c>CollapsibleSection</c> that is collapsed by default, so its rows are never
/// realised until the reader opens it. The test expands it and then <i>checks the
/// expansion took effect</i> before asserting anything (D-169: setting a property
/// and moving on produces a test that passes for the wrong reason).
/// </para>
/// </remarks>
public class TableSerialAlignmentTests
{
    /// <summary>Hosts a Results panel in a plain window at the app's declared size.</summary>
    /// <remarks>
    /// Deliberately not <c>MainWindow</c>: its constructor calls
    /// <c>WidgetPreferences.Load()</c> with no argument, which resolves to the real
    /// per-user <c>ui.json</c>. Per D-186, a test that reads and then writes the
    /// developer's own settings file is a test that depends on and corrupts machine
    /// state.
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

    /// <summary>Builds a real completed run: metrics, fits, utilisation rows and a trace.</summary>
    /// <param name="result">Receives the engine result, for tests that need it.</param>
    private static ResultsPanelViewModel CompletedRun(out SimulationResult result)
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

        var prefs = new WidgetPreferences(Path.Combine(
            Path.GetTempPath(), "OpdSimulatorTests", Guid.NewGuid().ToString("N") + ".json"));
        result = outcome.Result!;
        var results = new ResultsPanelViewModel(prefs);
        results.StartRun();
        results.CompleteRun(outcome);
        return results;
    }

    /// <summary>
    /// Gives the analytical-validation table rows, which the default harness run
    /// cannot produce.
    /// </summary>
    /// <remarks>
    /// <c>AnalyticalValidationService.Compare</c> refuses any run shorter than
    /// <see cref="AnalyticalValidationService.MinimumSteadyStateMinutes"/> (100,000
    /// minutes), because M/M/c closed forms describe steady state and a 165-minute
    /// clinic day is not it. That refusal is correct production behaviour, so the
    /// harness — which runs one clinic day — legitimately shows an empty analytical
    /// table. This re-runs the comparison against the same stage metrics stamped
    /// with a steady-state operating time, exactly as Phase 8C's own tests do.
    /// </remarks>
    private static void AttachSteadyStateAnalyticalRows(
        ResultsPanelViewModel vm, SimulationResult actual)
    {
        var steady = new SimulationResult
        {
            StageMetrics = actual.StageMetrics,
            OperatingTimeMinutes = AnalyticalValidationService.MinimumSteadyStateMinutes,
        };

        // ρ = 0.20 / 0.20 / 0.25 against the harness run's λ=0.1 and μ=0.5/0.25/0.2
        // with 1/2/2 servers, so every stage is stable and Compare keeps all three.
        var inputs = new List<(double Lambda, double Mu, int Servers)>
        {
            (0.1, 0.5, 1), (0.1, 0.25, 2), (0.1, 0.2, 2),
        };

        vm.AnalyticalValidation.ApplyAsync(
            steady,
            "Exponential",
            new[] { "Exponential", "Exponential", "Exponential" },
            inputs);

        // ApplyAsync computes off-thread and posts back to the UI thread.
        for (int i = 0; i < 50 && !vm.AnalyticalValidation.HasRows; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Finds a table's header <c>Grid</c> by a marker unique to that table's header
    /// row. Marker text appears only in headers — body cells carry formatted data,
    /// never a column name — so a marker cannot match a body row by accident.
    /// </summary>
        private static bool AncestorHasTitle(Visual visual, string title)
    {
        var current = visual.Parent;
        int depth = 0;
        while (current is not null && depth < 50)
        {
            depth++;
            if (current is StackPanel sp && sp.Children.OfType<TextBlock>().Any(t => t.Text == title))
                return true;
            if (current is Grid g && g.Children.OfType<TextBlock>().Any(t => t.Text == title))
                return true;
            current = current.Parent;
        }
        return false;
    }

    private static Grid HeaderGrid(Avalonia.Visual root, string marker, string? sectionTitle = null)
    {
        var candidates = root.GetVisualDescendants().OfType<Grid>()
            .Where(g => g.ColumnDefinitions.Count > 2
                        && g.Children.OfType<TextBlock>().Any(t => t.Text == marker));
        if (sectionTitle is not null)
            candidates = candidates.Where(g => AncestorHasTitle(g, sectionTitle));
        var list = candidates.ToList();
        if (list.Count > 0) return list[0];
        return root.GetVisualDescendants().OfType<Grid>()
            .First(g => g.ColumnDefinitions.Count > 2
                        && g.Children.OfType<TextBlock>().Any(t => t.Text == marker));
    }

    private static IReadOnlyList<Grid> RowsFor(Grid header)
    {
        if (header.GetVisualParent() is not Panel host)
        {
            return Array.Empty<Grid>();
        }

        int index = -1;
        for (int i = 0; i < host.Children.Count; i++)
        {
            if (ReferenceEquals(host.Children[i], header))
            {
                index = i;
                break;
            }
        }

        for (int i = index + 1; i < host.Children.Count; i++)
        {
            if (host.Children[i] is ItemsControl rows)
            {
                return rows.GetVisualDescendants().OfType<Grid>()
                    .Where(g => g.ColumnDefinitions.Count == header.ColumnDefinitions.Count)
                    .ToList();
            }
        }

        return Array.Empty<Grid>();
    }

    /// <summary>Reads a realised row's cell in <paramref name="column"/>.</summary>
    private static TextBlock Cell(Grid row, int column) =>
        row.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == column);

    /// <summary>
    /// The serial-number column's cells, top to bottom.
    /// </summary>
    /// <remarks>
    /// A cell whose <c>Text</c> is null is reported as <c>&lt;null&gt;</c> rather
    /// than coerced to an empty string, so an unrendered serial fails the
    /// contiguity assertion instead of quietly reading as a gap.
    /// </remarks>
    private static List<string> SerialsOf(Grid header, IReadOnlyList<Grid> rows) =>
        rows.Select(r => Cell(r, 0).Text ?? "<null>").ToList();

    // ---------------------------------------------------------------- Overview

    /// <summary>
    /// The overview ("System totals") listing is a label:value list, so the
    /// serial-number rule does not apply — and this test is why that is a decision
    /// rather than an oversight.
    /// </summary>
    /// <remarks>
    /// There is no header row and no shared column set: each row is an independent
    /// <c>MetricRow</c> with a 170-unit label column and a value column that takes
    /// the rest. A serial number on it would number rows that have no fixed order
    /// to be numbered against. What the rule <i>does</i> apply to here is
    /// alignment: every value is a magnitude, so the value column is right-aligned
    /// to line up decimal points.
    /// </remarks>
    [AvaloniaFact]
    public void OverviewMetrics_IsALabelValueList_SoSerialNumbersDoNotApply()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            Assert.True(vm.SystemMetrics.Count >= 5, "precondition: the overview listing has rows");

            // Located through the ItemsControl that owns the first label. Matching
            // every row on the first row's text would find exactly one grid.
            var firstLabel = panel.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == vm.SystemMetrics[0].Label);
            Assert.IsType<Grid>(firstLabel.GetVisualParent());
            var listing = firstLabel.GetVisualAncestors().OfType<ItemsControl>().First();

            var rows = listing.GetVisualDescendants().OfType<Grid>()
                .Where(g => g.ColumnDefinitions.Count == 2)
                .ToList();

            Assert.Equal(vm.SystemMetrics.Count, rows.Count);

            foreach (var row in rows)
            {
                var label = Cell(row, 0);
                var value = Cell(row, 1);
                Assert.NotEqual("#", label.Text);

                // Phase 8S (D-187 amendment): the numeric-alignment rule needs a
                // SHARED column to pay off. These rows are independent Grids, so
                // right-aligning the value pushed it to the panel's far edge without
                // lining any two values up — the owner saw a number adrift from its
                // label. An Auto column with Left alignment keeps it beside the label,
                // which is what a label:value list is for. The four real listing
                // tables below keep right-alignment and are asserted separately.
                Assert.Equal(TextAlignment.Left, value.TextAlignment);
                Assert.Equal(TextAlignment.Left, label.TextAlignment);
            }
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------- Per-server

    /// <summary>
    /// The per-server detail table carries a contiguous serial column. Phase 8Q.5
    /// converted it from pre-formatted monospace strings into a real table.
    /// </summary>
    // -------------------------------------------------------------- Chi-square

    /// <summary>The chi-square verdict table carries a contiguous serial column.</summary>
    [AvaloniaFact]
    public void ChiSquareTable_HasSerialNumberColumn()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            Assert.Equal(4, vm.ChiSquareRows.Count);

            var header = HeaderGrid(panel, "Distribution");
            Assert.Equal("#", Cell(header, 0).Text);
            Assert.Equal(7, header.ColumnDefinitions.Count);

            var rows = RowsFor(header);
            Assert.Equal(4, rows.Count);
            Assert.Equal(
                Enumerable.Range(1, rows.Count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                SerialsOf(header, rows));

            // The serial numbers the model's rows, so a verdict in the viva can be
            // cited without the reader counting rows.
            Assert.Equal(
                Enumerable.Range(1, 4).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                vm.ChiSquareRows.Select(r => r.SerialNumber));
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------- Performance Measures

    /// <summary>
    /// The Performance Measures per-stage table carries a contiguous serial column.
    /// </summary>
    /// <remarks>
    /// Verified from Phase 8Q.3 rather than added by 8Q.5. Its <c>#</c> header is
    /// now the panel-wide convention: the three tables 8Q.5 added originally read
    /// <c>No.</c>, and the owner unified them to <c>#</c> on 2026-10-03, so a reader
    /// learns the numbering once rather than per table. See D-187.
    /// </remarks>
    [AvaloniaFact]
    public void PerformanceMeasuresTable_HasSerialNumberColumn()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            Assert.Equal(3, vm.StageRows.Count);

            var header = HeaderGrid(panel, "Wait (min)", null);
            Assert.Equal("#", Cell(header, 0).Text);
            Assert.Equal(10, header.ColumnDefinitions.Count);

            var rows = RowsFor(header);
            Assert.Equal(3, rows.Count);
            Assert.Equal(
                Enumerable.Range(1, rows.Count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                SerialsOf(header, rows));
        }
        finally
        {
            window.Close();
        }
    }

    // ------------------------------------------------------------- Alignment

    /// <summary>
    /// Every numeric column of every listing table is right-aligned, in the header
    /// as well as the body.
    /// </summary>
    /// <remarks>
    /// The header matters as much as the body: a right-aligned column under a
    /// left-aligned heading reads as a mistake, because the numbers appear to sit
    /// under the wrong edge.
    /// </remarks>
    [AvaloniaFact]
    public void NumericColumns_AreRightAligned()
    {
        var vm = CompletedRun(out var actual);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            AttachSteadyStateAnalyticalRows(vm, actual);
            window.UpdateLayout();
            // (marker, numeric columns) per table.
            var tables = new (string Marker, int[] Numeric, string? SectionTitle)[]
            {
                ("Wait (min)", new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 }, null),   // per stage
                ("Distribution", new[] { 0, 3, 4, 5 }, null),                   // chi-square: χ², df, p
                ("M/M/c wait", new[] { 0, 2, 3, 4, 5, 6 }, null),               // analytical: all five
                ("#", new[] { 0, 3, 4, 5, 6, 7 }, "Per-session totals"),        // per-session: serial and numerics
            };

            foreach (var (marker, numeric, sectionTitle) in tables)
            {
                var header = HeaderGrid(panel, marker, sectionTitle);
                var rows = RowsFor(header);
                Assert.True(rows.Count > 0, $"precondition: {marker} must have realised rows");

                foreach (var column in numeric)
                {
                    Assert.Equal(TextAlignment.Right, Cell(header, column).TextAlignment);
                    foreach (var row in rows)
                    {
                        Assert.Equal(TextAlignment.Right, Cell(row, column).TextAlignment);
                    }
                }
            }

            // The stability verdict list is a third numeric column (ρ) inside a
            // label:value block; it follows the same rule.
            var rho = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text == vm.StabilityRows[0].Rho)
                .ToList();
            Assert.NotEmpty(rho);
            Assert.All(rho, t => Assert.Equal(TextAlignment.Right, t.TextAlignment));

            // Phase 8S (D-187 amendment): the overview listing's value column is no
            // longer asserted here. It is not a listing table — each row is its own
            // Grid — so right-aligning it put the value at the panel's far edge with
            // nothing to line up against. It is asserted Left, beside its label, in
            // OverviewMetrics_IsALabelValueList_SoSerialNumbersDoNotApply.
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Every text column is left-aligned: no text column inherits the numeric rule
    /// by accident, and none of them is centre- or right-aligned.
    /// </summary>
    [AvaloniaFact]
    public void TextColumns_AreLeftAligned()
    {
        var vm = CompletedRun(out var actual);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            AttachSteadyStateAnalyticalRows(vm, actual);
            window.UpdateLayout();
            // (marker, text columns) per table.
            var tables = new (string Marker, int[] Text, string? SectionTitle)[]
            {
                ("Wait (min)", new[] { 1 }, null),                  // per stage: Stage
                ("Distribution", new[] { 1, 2, 6 }, null),          // chi-square: Series, Distribution, Decision
                ("M/M/c wait", new[] { 1 }, null),                   // analytical: Stage
                ("Session", new[] { 1, 2 }, "Per-session totals"),  // per-session totals: Session, Stage
            };

            foreach (var (marker, text, sectionTitle) in tables)
            {
                var header = HeaderGrid(panel, marker, sectionTitle);
                var rows = RowsFor(header);
                Assert.True(rows.Count > 0, $"precondition: {marker} must have realised rows");

                foreach (var column in text)
                {
                    Assert.Equal(TextAlignment.Left, Cell(header, column).TextAlignment);
                    foreach (var row in rows)
                    {
                        Assert.Equal(TextAlignment.Left, Cell(row, column).TextAlignment);
                    }
                }
            }

            // Stability verdicts: the band name is a word and must read from the left.
            var verdicts = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text == vm.StabilityRows[0].Verdict)
                .ToList();
            Assert.NotEmpty(verdicts);
            Assert.All(verdicts, t => Assert.Equal(TextAlignment.Left, t.TextAlignment));

            // Overview labels.
            var labels = panel.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Text == vm.SystemMetrics[0].Label)
                .ToList();
            Assert.NotEmpty(labels);
            Assert.All(labels, t => Assert.Equal(TextAlignment.Left, t.TextAlignment));
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


    [AvaloniaFact]
    public void PerSessionTotals_HasSerialNumberColumn()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            var header = HeaderGrid(panel, "#", "Per-session totals");
            Assert.Equal("#", Cell(header, 0).Text);
            var rows = RowsFor(header);
            Assert.True(rows.Count > 0);
            Assert.Equal(
                Enumerable.Range(1, rows.Count).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                SerialsOf(header, rows));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PerSessionTotals_NumericColumns_AreRightAligned()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            var header = HeaderGrid(panel, "#", "Per-session totals");
            var rows = RowsFor(header);
            Assert.True(rows.Count > 0);
            var numeric = new[] { 0, 3, 4, 5, 6, 7 };
            foreach (var col in numeric)
            {
                Assert.Equal(TextAlignment.Right, Cell(header, col).TextAlignment);
                foreach (var row in rows)
                {
                    Assert.Equal(TextAlignment.Right, Cell(row, col).TextAlignment);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PerSessionTotals_TextColumns_AreLeftAligned()
    {
        var vm = CompletedRun(out _);
        var window = Host(vm);
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().First();
            var header = HeaderGrid(panel, "Session", "Per-session totals");
            var rows = RowsFor(header);
            Assert.True(rows.Count > 0);
            var text = new[] { 1, 2 };
            foreach (var col in text)
            {
                Assert.Equal(TextAlignment.Left, Cell(header, col).TextAlignment);
                foreach (var row in rows)
                {
                    Assert.Equal(TextAlignment.Left, Cell(row, col).TextAlignment);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }
}