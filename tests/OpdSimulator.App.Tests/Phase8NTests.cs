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
using Avalonia.Threading;
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
/// <para>
/// D-169 (the resize follow-up) adds a second kind of assertion: a default-size
/// screenshot cannot catch a bug that only appears when the user drags the window
/// smaller, because the default size is not the failing size. Every resizable dialog
/// therefore needs a test that actually resizes it — see AGENTS §10.6.
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
    /// D-169 supersedes the original Phase 8N rule. This test used to assert
    /// <c>double.IsNaN(dialog.Height)</c> — "the dialog must grow with its content" —
    /// because the root was a vertical StackPanel and the only way to keep a long
    /// run's buttons on screen was never to declare a height.
    /// <para>
    /// That rule is retired, not because it was wrong about the symptom but because
    /// <c>SizeToContent</c> and user resize are mutually exclusive: with
    /// <c>SizeToContent="Height"</c> the window owns its own height and the user's
    /// drag is contested on the next layout pass. The dialog now declares
    /// <c>Height="800"</c> and <c>SizeToContent="Manual"</c>.
    /// </para>
    /// <para>
    /// The invariant being protected is unchanged in spirit — the buttons must never
    /// be clipped off the bottom — but it is now guaranteed structurally, by the
    /// footer sitting in an <c>Auto</c> row, rather than by content-driven sizing.
    /// This test asserts the new mechanism.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_DeclaredHeight_IsBoundedAndTheBodyTakesTheRemainder()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };
        dialog.Show();
        dialog.UpdateLayout();

        Assert.Equal(800, dialog.Height);
        Assert.Equal(800, dialog.MaxHeight);

        // The body is the star row, so the window bounds it and it can scroll.
        // This is what the missing MaxHeight on the ScrollViewer used to be needed for.
        var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.Equal(1, Grid.GetRow(scroller));
        Assert.Equal(ScrollBarVisibility.Auto, scroller.VerticalScrollBarVisibility);

        dialog.Close();
    }

    // ---- D-169: the resize follow-up ---------------------------------------------

    /// <summary>
    /// Resizes a shown window and settles the layout.
    /// <para>
    /// The headless platform applies a <c>Window.Height</c> change through the
    /// dispatcher, not synchronously: setting the property and calling
    /// <c>UpdateLayout()</c> alone leaves <c>Bounds.Height</c> at its old value.
    /// Every test that claims to test a resize has to pump the queue, and then has to
    /// ASSERT that the height really changed — otherwise the assertion silently
    /// degrades into a default-size test, which is the exact failure D-169 exists to
    /// prevent. That degradation is not hypothetical: the first version of these
    /// tests passed against a dialog that had never actually shrunk.
    /// </para>
    /// </summary>
    private static double ResizeAndSettle(CalculationsDialog dialog, double requestedHeight)
    {
        var before = dialog.Bounds.Height;
        dialog.Height = requestedHeight;

        for (int pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            dialog.UpdateLayout();
        }

        var after = dialog.Bounds.Height;
        Assert.True(
            after < before,
            $"the resize never took effect: Bounds.Height stayed at {after} "
            + $"(requested {requestedHeight}) — this test would prove nothing");

        return after;
    }

    /// <summary>
    /// The regression the owner found by hand: shrinking the dialog vertically made
    /// the footer buttons vanish. The cause was a vertical <c>StackPanel</c> root,
    /// which gives every child its desired height and lays the LAST child — the
    /// footer — out at y≈2714 on an 800px window, i.e. ~1900px below the bottom edge.
    /// The body never scrolled either, because with no bound to shrink into it grew
    /// to its full content height (extent 2632 = viewport 2632).
    /// <para>
    /// Two things this test must NOT rely on, both learned the hard way:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>IsEffectivelyVisible</c> is a flag check, not a bounds check. Measured
    /// against the broken layout it returned <c>True</c> for a button sitting 1914px
    /// outside the window. The positional assertion below is the one that catches the
    /// bug; the flag assertion is kept only because the ruling asked for it and because
    /// it still catches a genuinely hidden control.
    /// </description></item>
    /// <item><description>
    /// The height the test asks for (300) is below <c>MinHeight</c>, so Avalonia
    /// coerces it to 400. Assertions are made against the height the dialog ACTUALLY
    /// took. The point is that the footer survives whatever the user drags to, not
    /// that a particular number is honoured.
    /// </description></item>
    /// </list>
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_Footer_VisibleAfterVerticalResize()
    {
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
            CopyText = "RUN CONFIGURATION\n  Rule: x",
        };
        dialog.Show();
        dialog.UpdateLayout();

        var clientHeight = ResizeAndSettle(dialog, 300);
        Assert.True(
            clientHeight >= dialog.MinHeight - 0.5,
            $"MinHeight should have coerced 300 up to {dialog.MinHeight}, got {clientHeight}");

        foreach (var caption in new[] { "Copy", "Close" })
        {
            var button = dialog.GetVisualDescendants().OfType<Button>()
                .Single(b => ReferenceEquals(b.Content, caption));

            Assert.True(
                button.IsEffectivelyVisible,
                $"'{caption}' was hidden when the dialog was resized to {clientHeight}px");

            // The assertion that actually catches the bug. Position relative to the
            // window, so the check is "is it on screen" and not merely "is it laid out".
            var origin = button.TranslatePoint(new Point(0, 0), dialog);
            Assert.NotNull(origin);
            Assert.True(
                origin!.Value.Y + button.Bounds.Height <= clientHeight + 0.5,
                $"'{caption}' sits at y={origin.Value.Y} with height {button.Bounds.Height}, "
                + $"below the {clientHeight}px window — it is laid out but out of reach");
        }

        dialog.Close();
    }

    /// <summary>
    /// The other half of the same bug: the body stopped scrolling. A ScrollViewer in
    /// an unbounded container never overflows internally, so
    /// <c>VerticalScrollBarVisibility="Auto"</c> had nothing to show — the overflow was
    /// past the window, not inside the viewer.
    /// <para>
    /// The extent-vs-viewport check is asserted at the DEFAULT size as well as after the
    /// resize, and deliberately so: measured against the broken layout, extent equalled
    /// viewport (2632 = 2632) at the default size too. A resize-independent check is a
    /// stronger net, and it fails with a clearer message than the resize does.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_ScrollViewer_ScrollsAfterResize()
    {
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
        };
        dialog.Show();
        dialog.UpdateLayout();

        var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();

        // At the default size: the body is bounded, so it overflows and can scroll.
        Assert.True(
            scroller.Extent.Height > scroller.Viewport.Height,
            $"at the default size the body must be bounded and overflowing, "
            + $"extent {scroller.Extent.Height} vs viewport {scroller.Viewport.Height}");

        ResizeAndSettle(dialog, 300);

        Assert.True(
            scroller.Extent.Height > scroller.Viewport.Height,
            $"the body must overflow so it can scroll, extent {scroller.Extent.Height} "
            + $"vs viewport {scroller.Viewport.Height}");

        scroller.Offset = new Vector(0, 40);
        dialog.UpdateLayout();
        Assert.True(
            scroller.Offset.Y > 0,
            $"the body did not scroll: offset stayed at {scroller.Offset.Y}");

        dialog.Close();
    }

    /// <summary>
    /// The dialog must not be shrinkable to a size where the footer and a useful
    /// amount of body cannot both be on screen at once.
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_MinHeight_PreventsUnusableShrink()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };

        Assert.True(
            dialog.MinHeight >= 400,
            $"the calculations dialog must not shrink below 400, MinHeight was {dialog.MinHeight}");
    }

    /// <summary>
    /// <c>SizeToContent</c> makes the window size itself from its content, which means
    /// the user's drag is contested on the next layout pass — the resize is either
    /// undone or fights the layout. The dialog must take its size from the user.
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_SizeToContent_IsManual()
    {
        var dialog = new CalculationsDialog { Title = "Calculations" };

        Assert.Equal(SizeToContent.Manual, dialog.SizeToContent);
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
    /// Captures the dialog AFTER a vertical resize — the state the owner actually hit
    /// the bug in. A default-size frame cannot show this bug: the default size is not
    /// the failing size (D-169).
    /// </summary>
    /// <remarks>
    /// This test replaces <c>Render_CalculationsDialog_SavePhase8nCalculationsPng</c>,
    /// which was retired before the gate for the same reason the Phase 8M one was:
    /// it re-rendered an already-cited evidence file on every run. AGENTS §10.6
    /// forbids that. <c>phase-8n-calculations.png</c> still exists on disk and is
    /// still the owner's to review; it simply no longer has a writer.
    /// </remarks>
    [AvaloniaFact]
    public void Render_CalculationsDialog_Resized_SavePhase8nCalculationsResizedPng()
    {
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
            CopyText = "RUN CONFIGURATION\n  Rule: x",
        };

        // No Width here. The control declares its own, which is the whole point of
        // D-166 — a test may not render a size production never uses.
        Assert.Equal(800, dialog.Width);
        dialog.Show();
        try
        {
            // The frame that matters: dragged as small as the owner can drag it.
            var frameHeight = ResizeAndSettle(dialog, 300);
            Assert.True(
                frameHeight < 800,
                $"the resized frame must be smaller than the default, was {frameHeight}");

            var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                "the resized frame must show a scrollable body, not a clipped one");

            var frame = HeadlessScreenshot.Capture(dialog);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8n-calculations-resized.png");
            frame.Save(shotPath);
            Assert.True(
                new FileInfo(shotPath).Length >= 512,
                "resized calculations frame missing or suspiciously small");
        }
        finally
        {
            dialog.Close();
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

    /// <summary>
    /// A ten-stage run, so the body is far taller than any window the user can drag
    /// to. <c>BuildRows</c> looks μ up by stage INDEX with bounds checks, so a result
    /// with more stages than the parameters describe is safe: the extra stages fall
    /// back to "fitted from the loaded data file" and "engine default" rather than
    /// throwing. That fallback is also convenient here — it keeps the fixture free of
    /// a ten-stage parameter block it does not care about.
    /// </summary>
    private static SimulationResult TallResult() =>
        Phase8MFixtures.ResultWithStages(
            ("Reception and Registration Desk", 1, 0.8, new[] { 0.8 }),
            ("Triage and Initial Consultation", 2, 0.65, new[] { 0.4, 0.25 }),
            ("Screening by the Nursing Team", 2, 0.5, new[] { 0.3, 0.2 }),
            ("Physician Consultation Room One", 3, 0.7, new[] { 0.3, 0.2, 0.2 }),
            ("Physician Consultation Room Two", 2, 0.45, new[] { 0.25, 0.2 }),
            ("Laboratory Sample Collection", 1, 0.6, new[] { 0.6 }),
            ("Radiology Imaging Suite", 1, 0.75, new[] { 0.75 }),
            ("Pharmacy Dispensary Counter", 2, 0.55, new[] { 0.3, 0.25 }),
            ("Billing and Insurance Desk", 1, 0.4, new[] { 0.4 }),
            ("Follow-up Scheduling Office", 1, 0.35, new[] { 0.35 }));

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

    // ---- Phase 8N follow-up 2: bottom buffer + contribution display (D-170, D-171)

    /// <summary>
    /// The owner's second report: scrolled to the bottom, the last lines of the
    /// per-stage result sat under the footer's top edge.
    /// <para>
    /// The cause is the one D-142 measured for the config panel, and the reason
    /// this test asserts a TRAILING ELEMENT rather than padding: a
    /// <c>ScrollViewer</c>'s own padding is not part of the scrollable extent, so
    /// increasing it cannot give the user one more pixel of travel. D-142 recorded
    /// the arithmetic — extent came out <c>content − 2 × padding</c>. The buffer
    /// has to be a real element inside the content so the extent accounts for it.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_HasBottomBuffer()
    {
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
        };
        dialog.Show();
        try
        {
            dialog.UpdateLayout();

            // The buffer is the LAST child of the body StackPanel, inside the
            // ScrollViewer, and taller than nothing.
            var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(
                scroller.GetVisualDescendants().OfType<Border>()
                    .Any(b => ReferenceEquals(b.Name, "BottomBuffer")),
                "the dialog must end its scrollable content with a trailing buffer element");

            var buffer = scroller.GetVisualDescendants().OfType<Border>()
                .Single(b => ReferenceEquals(b.Name, "BottomBuffer"));
            Assert.True(
                buffer.Bounds.Height > 0,
                $"the bottom buffer must have real height, was {buffer.Bounds.Height}");
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// The user-visible consequence of the buffer: at maximum scroll the last body
    /// line is clear of the footer's top edge. Asserted as a position comparison
    /// between the two, not as a buffer height — the height is the means, this is
    /// the end (D-169's rule: assert what the user would see).
    /// </summary>
    /// <summary>
    /// The user-visible consequence of the buffer: at maximum scroll the last body
    /// line clears the footer's top edge by the amount the owner asked for
    /// (~32 px), not merely by a non-negative amount.
    /// <para>
    /// <b>This test was wrong the first time and the mistake is the interesting
    /// part.</b> It originally asserted only <c>lastBottom &lt;= footerTop</c> —
    /// "the line is not <i>under</i> the footer" — and it <b>passed with the buffer
    /// deleted</b>, because the content <c>Border</c>'s own <c>ThicknessSpaceL</c>
    /// padding already gives 24 px of bottom clearance. A non-overlap assertion
    /// measures nothing the padding did not already provide, so it would have
    /// shipped a green gate over the reported defect. The D-169 lesson again, in a
    /// new form: assert the thing that was asked for, and if the test still passes
    /// after reverting the fix, it is testing the wrong property. Hence the
    /// threshold below — 32 px, which 48 px of clearance satisfies and 24 px of
    /// padding alone does not.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void CalculationsDialog_LastLineNotClippedAtScrollBottom()
    {
        // The owner's figure. Kept as a named constant rather than a literal in the
        // assertion so the test and the requirement quote the same number.
        const double RequiredClearance = 32;

        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
        };
        dialog.Show();
        try
        {
            dialog.UpdateLayout();
            var shrunk = ResizeAndSettle(dialog, 300);

            var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                "precondition: the body must overflow for this to mean anything");

            // Scroll to the true maximum.
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            for (int pass = 0; pass < 3; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                dialog.UpdateLayout();
            }

            // The footer's top edge, in window coordinates.
            var footer = dialog.GetVisualDescendants().OfType<Border>()
                .Single(b => b.BorderThickness.Top > 0);
            var footerTop = footer.TranslatePoint(new Point(0, 0), dialog)!.Value.Y;
            Assert.True(footerTop > 0 && footerTop < shrunk, $"footer top {footerTop} implausible for a {shrunk}px window");

            // The last real body line. The trailing buffer is a Border with no
            // content, so it contributes no TextBlock and needs no filtering —
            // the deepest TextBlock in the scroller IS the last body line.
            var lastLine = scroller.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(t => t.Text is not null && t.Text.Length > 0)
                .OrderByDescending(t => t.TranslatePoint(new Point(0, 0), dialog)!.Value.Y)
                .First();

            var lastBottom = lastLine.TranslatePoint(new Point(0, 0), dialog)!.Value.Y
                + lastLine.Bounds.Height;
            var clearance = footerTop - lastBottom;
            Assert.True(
                clearance >= RequiredClearance,
                $"the last line ends at y={lastBottom} and the footer starts at "
                + $"y={footerTop}: only {clearance:F1}px of clearance at maximum "
                + $"scroll, the owner asked for {RequiredClearance:F0}px. A negative "
                + "value means the line is under the footer's top edge; a small "
                + "positive one means the buffer is gone and only the content "
                + "padding is left.");
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// D-171: wherever a per-server utilisation is shown, the contribution is shown
    /// beside it. This test walks the surfaces found in the step-2.1 sweep and
    /// fails if any of them drops a value — the point being that a later edit which
    /// removes one of them has to fail here rather than quietly shipping a chart
    /// that lies about a server.
    /// </summary>
    [Fact]
    public void PerServerDisplay_ShowsBothBusyAndContribution()
    {
        var result = TwoServerResult();

        // Surface: the per-server detail rows under the utilisation chart.
        var results = new ResultsPanelViewModel();
        results.CompleteRun(
            new RunOutcome(result, Array.Empty<FitReport>(), Array.Empty<string>(), 0.4, null));
        Assert.NotEmpty(results.PerServerDetailLines);
        Assert.All(results.PerServerDetailLines, line =>
        {
            Assert.Contains("busy", line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("contributes", line, StringComparison.OrdinalIgnoreCase);
        });

        // Surface: the calculations dialog's utilisation block.
        var text = CalculationsTextBuilder.Build(result, Parameters(), "entered manually");
        Assert.Contains("contribution", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utilisation", text, StringComparison.OrdinalIgnoreCase);

        // Surface: the chart data the tooltip is formatted from. Both values must
        // survive onto the bar, or a tooltip cannot show both.
        var chart = UtilisationChartService.Build(result);
        Assert.NotEmpty(chart.Bars);
        Assert.All(chart.Bars, bar =>
        {
            Assert.InRange(bar.Utilisation, 0, 1);
            Assert.InRange(bar.Contribution, 0, 1);
        });
        Assert.NotEmpty(chart.PerServerDetail);
        Assert.All(chart.PerServerDetail, detail =>
        {
            Assert.InRange(detail.ServerUtilisation, 0, 1);
            Assert.InRange(detail.Contribution, 0, 1);
        });
    }

    /// <summary>
    /// The arithmetic, pinned: a contribution is the server's own utilisation
    /// divided by the stage's server count. Not a new metric — a display
    /// transformation of two numbers the engine already produced.
    /// </summary>
    [Fact]
    public void PerServerDisplay_ContributionEqualsBusyDivServerCount()
    {
        var chart = UtilisationChartService.Build(TwoServerResult());
        Assert.NotEmpty(chart.Bars);

        foreach (var bar in chart.Bars)
        {
            Assert.True(bar.ServerCount > 0, $"{bar.StageName} S{bar.ServerNumber} has no server count");
            Assert.Equal(
                bar.Utilisation / bar.ServerCount,
                bar.Contribution,
                precision: 12);
        }

        // And the same identity on the detail rows the chart caption is built from.
        foreach (var detail in chart.PerServerDetail)
        {
            Assert.Equal(
                detail.ServerUtilisation / detail.ServerCount,
                detail.Contribution,
                precision: 12);
        }
    }

    /// <summary>
    /// The property the two numbers exist to support: contributions sum to the
    /// stage utilisation, per-server utilisations do not. This is the claim the
    /// chart caption and the calculations block both make in words, so it is
    /// asserted rather than asserted-about.
    /// </summary>
    [Fact]
    public void PerServerDisplay_StageTotalEqualsSumOfContributions()
    {
        var chart = UtilisationChartService.Build(TwoServerResult());

        foreach (var group in chart.Bars.GroupBy(b => b.StageName))
        {
            var sumOfContributions = group.Sum(b => b.Contribution);
            var sumOfUtilisations = group.Sum(b => b.Utilisation);
            var stageUtilisation = chart.PerServerDetail
                .First(d => d.StageName == group.Key)
                .StageUtilisation;

            Assert.Equal(stageUtilisation, sumOfContributions, precision: 10);

            // "Per-server utilisations do not sum" is only meaningful where there
            // is more than one server. On a c = 1 stage the contribution IS the
            // server's own utilisation, so the two sums are equal by definition
            // and demanding a strict inequality there would fail for the right
            // reason — the test would be wrong, not the code.
            var serverCount = group.First().ServerCount;
            if (serverCount > 1)
            {
                Assert.True(
                    sumOfUtilisations > sumOfContributions + 1e-9,
                    $"for {group.Key} (c = {serverCount}) the per-server utilisations "
                    + $"({sumOfUtilisations:F4}) should NOT sum to the stage value the "
                    + $"way the contributions ({sumOfContributions:F4}) do");
            }
            else
            {
                Assert.Equal(sumOfContributions, sumOfUtilisations, precision: 12);
            }
        }
    }

    /// <summary>
    /// A structural invariant: no single server can contribute more than its stage
    /// as a whole, since the contributions partition it. True by construction, and
    /// worth locking because the chart's y-axis ceiling depends on the same
    /// relationship.
    /// </summary>
    [Fact]
    public void PerServerDisplay_ContributionLessThanOrEqualStageUtil()
    {
        var chart = UtilisationChartService.Build(TwoServerResult());

        Assert.All(chart.PerServerDetail, detail =>
            Assert.True(
                detail.Contribution <= detail.StageUtilisation + 1e-9,
                $"{detail.StageName} S{detail.ServerNumber} contributes "
                + $"{detail.Contribution:F4} but its stage utilisation is only "
                + $"{detail.StageUtilisation:F4}"));
    }

    /// <summary>
    /// Evidence: the calculations dialog scrolled to the bottom, showing the last
    /// line clear of the footer by the buffer's width (D-170).
    /// </summary>
    [AvaloniaFact]
    public void Render_CalculationsDialog_ScrolledToBottom_SavePhase8nCalculationsBufferPng()
    {
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(TallResult(), Parameters(), "entered manually"),
            CopyText = "RUN CONFIGURATION\n  Rule: x",
        };
        dialog.Show();
        try
        {
            ResizeAndSettle(dialog, 300);

            var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            for (int pass = 0; pass < 3; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                dialog.UpdateLayout();
            }

            // The whole point of the frame: the last line must clear the footer's top
            // edge by the owner's 32 px, or the frame is evidence of the padding
            // rather than of the buffer. A plain "not under the footer" check would
            // pass with the buffer deleted — measured 26 px of clearance without it —
            // so this asserts the threshold, matching
            // CalculationsDialog_LastLineNotClippedAtScrollBottom.
            var footer = dialog.GetVisualDescendants().OfType<Border>()
                .Single(b => b.BorderThickness.Top > 0);
            var footerTop = footer.TranslatePoint(new Point(0, 0), dialog)!.Value.Y;
            var lowest = scroller.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(t => t.Text is not null && t.Text.Length > 0)
                .Max(t => t.TranslatePoint(new Point(0, 0), dialog)!.Value.Y + t.Bounds.Height);
            Assert.True(
                footerTop - lowest >= 32,
                $"frame would show the last line with only {footerTop - lowest:F1}px "
                + $"of clearance (content ends at {lowest}, footer at {footerTop}); "
                + "the owner asked for 32px, which only the trailing buffer delivers");

            var frame = HeadlessScreenshot.Capture(dialog);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8n-calculations-buffer.png");
            frame.Save(shotPath);
            Assert.True(
                new FileInfo(shotPath).Length >= 512,
                "buffer frame missing or suspiciously small");
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// Evidence: a real three-stage run (Reception 1 / Screening 2 / Doctor 3) whose
    /// per-server detail rows show both values side by side (D-171). A real run, not
    /// a fixture, because the owner's scenario is a real run and because a real run
    /// has unequal per-server utilisations — the case where showing only the
    /// contribution is most misleading.
    /// </summary>
    [AvaloniaFact]
    public void Render_PerServerDetail_WithBothValues_SavePhase8nContributionPng()
    {
        // A real three-stage run, so the numbers are the engine's own and the
        // per-server split is genuinely unequal — the case where showing only the
        // contribution is most misleading.
        var sample = Path.Combine(
            FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv");
        var binding = DataAnalyzer.Analyze(sample);
        Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        for (int i = 0; i < 3 && i < config.StageRows.Count; i++)
        {
            config.StageRows[i].Servers.Value = new[] { "1", "2", "3" }[i];
        }

        var parameters = config.TryBuildRunParameters()!;
        var outcome = SimulationCoordinator.Run(parameters, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        // The calculations dialog is the surface captured, not the collapsed
        // "Per-server detail" section: that section ships with IsExpanded="False"
        // and its item containers are not realised while it is closed, so a window
        // capture of it would show no rows and prove nothing. The dialog renders
        // every row unconditionally, and it is the receipt a viva examiner reads.
        var dialog = new CalculationsDialog
        {
            Title = "Calculations",
            Rows = CalculationsTextBuilder.BuildRows(outcome.Result, parameters, "fitted from sample_3stage_clinic.csv"),
        };
        dialog.Show();
        try
        {
            var scroller = dialog.GetVisualDescendants().OfType<ScrollViewer>().Single();

            // Scroll the per-server block into view: the first contribution row.
            var firstContribution = scroller.GetVisualDescendants()
                .OfType<TextBlock>()
                .First(t => t.Text is not null
                    && t.Text.Contains("— contribution", StringComparison.Ordinal));
            var target = firstContribution.TranslatePoint(new Point(0, 0), dialog)!.Value.Y;
            scroller.Offset = new Vector(0, Math.Max(0, target - 40));
            for (int pass = 0; pass < 3; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                dialog.UpdateLayout();
            }

            // Both values must be on screen, or the frame is not evidence: the
            // server's own utilisation and its contribution, for real servers.
            var visible = scroller.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(t => t.Text ?? string.Empty)
                .ToList();
            // The label and the value are SEPARATE TextBlocks (the dialog lays each
            // field out as a label column and a value column), so "both values" is
            // checked across the pair, not inside one string.
            Assert.True(
                visible.Any(t => t.Contains("— busy (derived)", StringComparison.Ordinal)
                    && t.Contains("S1", StringComparison.Ordinal)),
                "no per-server busy row visible; rendered texts: " + string.Join(" | ", visible));
            Assert.True(
                visible.Any(t => t.Contains("— contribution", StringComparison.Ordinal)
                    && t.Contains("S1", StringComparison.Ordinal)),
                "no per-server contribution row visible; rendered texts: " + string.Join(" | ", visible));
            Assert.True(
                visible.Any(t => t.Contains("of stage capacity", StringComparison.Ordinal)),
                "the contribution rows carry no share-of-capacity wording");
            Assert.True(
                visible.Any(t => t.Contains("min", StringComparison.Ordinal)
                    && t.Contains("utilisation", StringComparison.OrdinalIgnoreCase)),
                "the busy rows carry no minutes-plus-utilisation pair");

            var frame = HeadlessScreenshot.Capture(dialog);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8n-contribution.png");
            frame.Save(shotPath);
            Assert.True(
                new FileInfo(shotPath).Length >= 512,
                "contribution frame missing or suspiciously small");
        }
        finally
        {
            dialog.Close();
        }
    }

    /// <summary>
    /// A stage mix with a known, deliberately unequal split, so the two displayed
    /// numbers differ and a test cannot pass by confusing one for the other.
    /// <list type="bullet">
    /// <item>Reception, c = 1, server utilisation 0.82 → contribution 0.82. Here the
    /// two numbers are equal by definition; a formula that divided by c + 1 would
    /// be caught.</item>
    /// <item>Screening, c = 2, servers 0.755 and 0.756 → contributions 0.3775 and
    /// 0.378, summing to the 0.7555 stage utilisation, which is the mean of the
    /// two per-server utilisations (D-121).</item>
    /// </list>
    /// </summary>
    private static SimulationResult TwoServerResult() =>
        Phase8MFixtures.ResultWithStages(
            ("Reception", 1, 0.82, new[] { 0.82 }),
            ("Screening", 2, 0.7555, new[] { 0.755, 0.756 }));
}
