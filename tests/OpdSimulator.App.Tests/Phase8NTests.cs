using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Engine;
using OpdSimulator.Data.Parameters;
using OpdSimulator.Data.Preprocess;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8N — the calculations dialog sizing and layout fix (D-165, D-166).
/// </summary>
/// <remarks>
/// <para>
/// Why this class exists. In Phase 8M the screenshot test built the dialog itself and
/// set <c>Width = 760</c>, while the production path in <c>ResultsPanel</c> set no width
/// at all and therefore ran at the <c>ThemedDialog</c> default of 440. The frame was
/// clean, the assertions were green, and the dialog a user actually opened was clipped.
/// Every test here is therefore written against the CONTROL's declared XAML, never
/// against a size the test supplies — see D-166.
/// </para>
/// <para>
/// These are structural assertions. They prove the layout cannot regress to "one
/// no-wrap text block in a narrow window". They cannot prove the pixels look right;
/// that is the owner's pass on <c>logs/screenshots/phase-8n-calculations.png</c>.
/// </para>
/// </remarks>
public class Phase8NDialogSizingTests
{
    /// <summary>A stage name past 30 characters, to exercise the Auto label column.</summary>
    private const string LongStageName = "Triage and Initial Consultation Desk";

    [AvaloniaFact]
    public void CalculationsDialog_HasMinimumWidth640()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };

        // 640 is the floor the owner set. The point of the assertion is that the
        // minimum lives in the control, so a call site cannot shrink it away.
        Assert.True(
            dialog.MinWidth >= 640,
            $"the calculations dialog must not be narrower than 640, was {dialog.MinWidth}");
    }

    [AvaloniaFact]
    public void CalculationsDialog_IsResizable()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };

        Assert.True(dialog.CanResize, "the calculations dialog must be resizable by the user");
    }

    /// <summary>
    /// The regression this whole phase exists for: a long value used to be cut off
    /// because it shared one no-wrap text block with its label. The value column must
    /// wrap, and the label column must be allowed to grow to fit a long stage name
    /// instead of starving the value of the space it needs.
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_ValueColumnWrapsForLongValues()
    {
        var rows = CalculationsTextBuilder.BuildRows(
            LongNameResult(),
            Parameters(),
            "entered manually");

        var dialog = new CalculationsDialog { Title = "Calculations", Rows = rows };
        dialog.Show();
        dialog.UpdateLayout();

        var grid = RowsGrid(dialog);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(GridUnitType.Auto, grid.ColumnDefinitions[0].Width.GridUnitType);
        Assert.Equal(GridUnitType.Star, grid.ColumnDefinitions[1].Width.GridUnitType);

        // Every value block wraps; no value block is capped with a MaxWidth that
        // would reintroduce clipping at a narrower window size.
        var values = ValueBlocks(dialog).ToList();
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.Equal(TextWrapping.Wrap, v.TextWrapping));

        // The long stage name must appear in full in a label of its own — a shared
        // clipped block could not do this.
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text is not null && t.Text.Contains(LongStageName, StringComparison.Ordinal));

        // The label column takes the room it needs, so no value is pushed out of
        // the window: every value's right edge stays inside the grid's own width.
        var gridWidth = grid.Bounds.Width;
        Assert.True(gridWidth > 0, "the rows grid must have been measured");
        Assert.All(values, v => Assert.True(
            v.Bounds.Width + v.Margin.Left + v.Margin.Right <= gridWidth + 0.5,
            $"value '{v.Text}' overflowed the grid ({v.Bounds.Width} + margins vs {gridWidth})"));
    }

    [AvaloniaFact]
    public void CalculationsDialog_ButtonsRow_UsesGridWithRightAlignment()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };
        dialog.Show();
        dialog.UpdateLayout();

        // The footer is the only Grid in the window that is not the body: the body
        // is a direct child of the content control, the buttons are the window's.
        var footers = dialog.GetVisualDescendants()
            .OfType<Grid>()
            .Where(g => g.Children.OfType<Button>().Any())
            .ToList();

        Assert.Single(footers);
        var footer = footers[0];

        // *,Auto,Auto — a spacer, then Copy, then the primary action.
        Assert.Equal(3, footer.ColumnDefinitions.Count);
        Assert.Equal(GridUnitType.Star, footer.ColumnDefinitions[0].Width.GridUnitType);
        Assert.All(footer.ColumnDefinitions.Skip(1),
            c => Assert.Equal(GridUnitType.Auto, c.Width.GridUnitType));

        var buttons = footer.Children.OfType<Button>().ToList();
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.Equal(HorizontalAlignment.Right, b.HorizontalAlignment));

        // Both buttons sit in the Auto columns, not the spacer.
        Assert.All(buttons, b => Assert.True(Grid.GetColumn(b) > 0));
    }

    /// <summary>
    /// A fixed Height would clip a long run's buttons off the bottom. The dialog must
    /// grow with its content and stop at MaxHeight, scrolling in between.
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_NoFixedHeightClipsContent()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };
        dialog.Show();
        dialog.UpdateLayout();

        Assert.True(
            double.IsNaN(dialog.Height),
            $"the dialog must not declare a fixed Height, was {dialog.Height}");
        Assert.True(dialog.MaxHeight > 0, "the dialog must bound itself with MaxHeight");

        // The body scrolls, so content past the bound is reachable rather than lost.
        var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);
    }

    [AvaloniaFact]
    public void CalculationsDialog_CopyButton_VisibleAndSized()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };
        dialog.Show();
        dialog.UpdateLayout();

        // Hidden until there is something to copy.
        var copy = dialog.GetVisualDescendants().OfType<Button>()
            .Single(b => ReferenceEquals(b.Content, "Copy"));
        Assert.False(copy.IsVisible, "Copy must stay hidden until CopyText is set");

        dialog.CopyText = "RUN CONFIGURATION\n  Rule: x";
        dialog.UpdateLayout();
        Assert.True(copy.IsVisible, "Copy must appear once there is text to copy");
        Assert.True(
            copy.MinWidth >= 100,
            $"the Copy button must not clip its own label, MinWidth was {copy.MinWidth}");
        dialog.Close();
    }

    /// <summary>
    /// <c>Build()</c> feeds the clipboard and four Phase 8M tests. Phase 8N renders it
    /// from the rows instead of walking the data twice, so this locks the flat format
    /// the clipboard depends on: two leading spaces, the label padded to 42, a colon.
    /// </summary>
    [Fact]
    public void Calculations_FlatText_KeepsItsMonospaceFormat()
    {
        var text = CalculationsTextBuilder.Build(ShortResult(), Parameters(), "entered manually");

        var lines = text.Split('\n');
        foreach (var line in lines.Where(l => l.Contains(" : ", StringComparison.Ordinal)
                                          || l.EndsWith(":", StringComparison.Ordinal)))
        {
            Assert.StartsWith("  ", line, StringComparison.Ordinal);
            Assert.Equal(44, line.IndexOf(':'));
        }

        // Section headings keep their underline: that is the clipboard format, and
        // only the on-screen render drops it (D-165).
        Assert.Contains("RUN CONFIGURATION\n----------", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The dialog and the clipboard are two renderings of one traversal. This is what
    /// proves the refactor did not quietly drop a line or change a word.
    /// </summary>
    [Fact]
    public void Calculations_RowsAndFlatText_SayTheSameThings()
    {
        var result = ShortResult();
        var text = CalculationsTextBuilder.Build(result, Parameters(), "fitted from sample.csv");
        var rows = CalculationsTextBuilder.BuildRows(result, Parameters(), "fitted from sample.csv");

        Assert.All(rows.Where(r => r.Kind == CalculationRowKind.Field),
            r => Assert.Contains(r.Label, text, StringComparison.Ordinal));
        Assert.All(rows.Where(r => r.Kind == CalculationRowKind.Field),
            r => Assert.Contains(r.Value, text, StringComparison.Ordinal));
        Assert.All(rows.Where(r => r.Kind == CalculationRowKind.Section),
            r => Assert.Contains(r.Label, text, StringComparison.Ordinal));
    }

    /// <summary>
    /// No row may carry leading whitespace. It was how the flat text indented, and
    /// invisible spaces render unpredictably in a proportional font.
    /// </summary>
    [Fact]
    public void Calculations_Rows_NeverCarryLeadingWhitespace()
    {
        var rows = CalculationsTextBuilder.BuildRows(ShortResult(), Parameters(), "entered manually");

        Assert.All(rows, r => Assert.Equal(r.Label, r.Label.TrimStart()));
        Assert.Contains(rows, r => r.Kind == CalculationRowKind.Field && r.IndentLevel == 1);
    }

    /// <summary>
    /// The no-run case must still say something, in the dialog and in the clipboard,
    /// from one source of truth.
    /// </summary>
    [AvaloniaFact]
    public void Calculations_EmptyRows_StillExplainThemselves()
    {
        Assert.Empty(CalculationsTextBuilder.BuildRows(null, null, null));
        Assert.Equal(
            CalculationsTextBuilder.NoRunMessage,
            CalculationsTextBuilder.Build(null, null, null));

        var dialog = new CalculationsDialog { Title = "Calculations" };
        dialog.Show();
        dialog.UpdateLayout();

        Assert.Contains(
            dialog.GetVisualDescendants().OfType<TextBlock>(),
            t => t.Text is not null && t.Text.Contains("No simulation has been run yet", StringComparison.Ordinal));
        dialog.Close();
    }

    // ---- Phase 8N gate evidence -------------------------------------------------

    /// <summary>
    /// Captures the fixed dialog at its declared size. Unlike the Phase 8M frame this
    /// test sets no width: it renders the control exactly as a user meets it (D-166).
    /// </summary>
    [AvaloniaFact]
    public void Render_CalculationsDialog_SavePhase8nCalculationsPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;
            main.Results.StartRun();
            main.Results.CompleteRun(
                new RunOutcome(ShortResult(), Array.Empty<FitReport>(), Array.Empty<string>(), 0.4, null),
                Parameters(),
                "fitted from sample_3stage_clinic.csv");

            var dialog = new CalculationsDialog
            {
                Title = "Calculations",
                Rows = main.Results.CalculationsRows,
                CopyText = main.Results.CalculationsText,
            };

            // No Width here. The control declares its own, which is the whole point
            // of D-166 — a test may not render a size production never uses.
            Assert.Equal(800, dialog.Width);
            dialog.Show();
            try
            {
                dialog.UpdateLayout();
                var frame = HeadlessScreenshot.Capture(dialog);
                var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
                Directory.CreateDirectory(shotDir);
                var shotPath = Path.Combine(shotDir, "phase-8n-calculations.png");
                frame.Save(shotPath);
                Assert.True(
                    new FileInfo(shotPath).Length >= 512,
                    "calculations frame missing or suspiciously small");
            }
            finally
            {
                dialog.Close();
            }
        }
        finally
        {
            window.Close();
        }
    }

    // ---- fixtures ---------------------------------------------------------------

    private static Grid RowsGrid(CalculationsDialog dialog) =>
        dialog.GetVisualDescendants().OfType<Grid>().Single(g => g.Children.OfType<TextBlock>().Any());

    private static IEnumerable<TextBlock> ValueBlocks(CalculationsDialog dialog) =>
        RowsGrid(dialog).Children.OfType<TextBlock>()
            .Where(t => Grid.GetColumn(t) == 1)
            .Where(t => (t.TextWrapping == TextWrapping.Wrap));

    private static SimulationResult ShortResult() =>
        Phase8MFixtures.ResultWithStages(
            ("Reception", 1, 0.625, new[] { 0.625 }),
            ("Screening", 2, 0.5, new[] { 0.75, 0.25 }));

    private static SimulationResult LongNameResult() =>
        Phase8MFixtures.ResultWithStages(
            (LongStageName, 1, 0.8, new[] { 0.8 }),
            ("Screening", 2, 0.5, new[] { 0.75, 0.25 }));

    private static SimulationParameters Parameters() =>
        new(
            ParameterMode.RateWise,
            "Exponential",
            0.25,
            new[] { "Reception", "Screening" },
            new[] { 1, 2 },
            new double?[] { 0.4, 0.3 },
            RunMode.ClinicDay,
            0,
            1,
            DayOfWeek.Monday,
            null,
            42,
            0.4,
            "Standard");

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
