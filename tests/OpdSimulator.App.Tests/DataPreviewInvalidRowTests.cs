using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.6 — FR-UI-17 invalid-row treatment on <see cref="DataPreviewTable"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these tests cannot read the view model.</b> The defect was that
/// <see cref="PreviewRow"/> computed <c>RowBackground</c>, <c>RowBorderBrush</c>
/// and <c>RowBorderThickness</c> correctly all along (D-080) and the
/// <c>DataTemplate</c> bound none of them. So an assertion like
/// <c>Assert.Equal(3, rows[1].RowBorderThickness.Left)</c> is green against the
/// broken control as well as the fixed one — it tests the arithmetic, not the
/// promise. Every assertion below therefore reads the <i>realised element</i>: the
/// <c>Border</c> that the template produced, its actual brush and thickness, and
/// its actual tooltip. That is the location the treatment has to land in
/// (D-186).
/// </para>
/// <para>
/// The negative case is asserted as explicitly as the positive one. A template
/// that painted every row red would satisfy "invalid rows have a red border" and
/// still be useless, so <c>ValidRow_NoRedBorder</c> holds the gate on the other
/// side.
/// </para>
/// </remarks>
public class DataPreviewInvalidRowTests
{
    private const string Reason = "Service time must be positive (row 2 of the file).";

    /// <summary>An invalid row reaches the screen wearing the FR-UI-17 treatment.</summary>
    [AvaloniaFact]
    public void DataPreviewTable_InvalidRow_HasRedBorder()
    {
        var window = Host(out var table);
        try
        {
            var (items, rows) = RealisedRows(window, table);

            var invalid = items[1];
            var model = rows[1];
            var border = RowBorderOf(invalid, model);

            // The thickness the model computed is the thickness on screen, and
            // FR-UI-17 asks for at least 2 px so the state is not carried by hue
            // alone. 3 px on the leading edge is what the model specifies.
            Assert.Equal(model.RowBorderThickness, border.BorderThickness);
            Assert.Equal(new Thickness(3, 0, 0, 0), border.BorderThickness);
            Assert.True(
                border.BorderThickness.Left >= 2,
                $"FR-UI-17 asks for a >= 2 px border; the row rendered {border.BorderThickness.Left} px.");

            // Same for the colour: bound, not merely present on the model.
            Assert.Same(model.RowBorderBrush, border.BorderBrush);
            Assert.NotEqual(Brushes.Transparent, border.BorderBrush);

            // And the tinted background, which is what carries the row's state when
            // the accent is scrolled out of view.
            Assert.Same(model.RowBackground, border.Background);
            Assert.NotSame(
                rows[0].RowBackground,
                border.Background);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The invalid row's icon and tooltip carry the validator's own words.</summary>
    /// <remarks>
    /// A generic "invalid row" message would pass this too, so the assertion is on
    /// the exact reason string the validator produced — the FR-UI-17 requirement
    /// that the tooltip name the specific failure.
    /// </remarks>
    [AvaloniaFact]
    public void DataPreviewTable_InvalidRow_TooltipShowsReason()
    {
        var window = Host(out var table);
        try
        {
            var (items, rows) = RealisedRows(window, table);

            var border = RowBorderOf(items[1], rows[1]);

            // Hovering anywhere on the row explains it, not only the 20 px icon.
            Assert.Equal(Reason, ToolTip.GetTip(border));

            var icon = items[1].GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(t => t.Text == "\u26A0");
            Assert.Equal(Reason, ToolTip.GetTip(icon));

            // Not colour-only: FR-UI-17 pairs the red with an icon and a message.
            Assert.NotEqual(
                string.Empty,
                rows[1].IssueGlyph);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A valid row is left alone — no accent, no tint, no icon.</summary>
    [AvaloniaFact]
    public void DataPreviewTable_ValidRow_NoRedBorder()
    {
        var window = Host(out var table);
        try
        {
            var (items, rows) = RealisedRows(window, table);

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].IsInvalid)
                {
                    continue;
                }

                var border = RowBorderOf(items[i], rows[i]);

                Assert.Equal(new Thickness(0), border.BorderThickness);
                Assert.Equal(Brushes.Transparent, border.BorderBrush);
                Assert.Null(ToolTip.GetTip(border));

                // The gutter still occupies its width, so a valid row's cells start
                // where an invalid row's do.
                Assert.DoesNotContain(
                    items[i].GetVisualDescendants().OfType<TextBlock>(),
                    t => t.Text == "\u26A0");
            }
        }
        finally
        {
            window.Close();
        }
    }

    // ----------------------------------------------------------------- harness

    /// <summary>
    /// Hosts a three-row table whose middle row the validator rejected, and returns
    /// the window. The invalid row is in the middle on purpose: a control that only
    /// treated the first or last row correctly would still pass if it were at
    /// either end.
    /// </summary>
    private static Window Host(out DataPreviewTable table)
    {
        table = new DataPreviewTable
        {
            ColumnTitles = new[] { "Patient", "Service", "Doctor" },
            Rows = new[]
            {
                new[] { "P-001", "4.2", "Ali" },
                new[] { "P-002", "-1.0", "Sara" },
                new[] { "P-003", "6.1", "Ali" },
            },
            InvalidRows = new Dictionary<int, string?> { [1] = Reason },
        };

        var window = new Window { Width = 800, Height = 600, Content = table };
        window.Show();
        Settle(window);
        return window;
    }

    /// <summary>
    /// The realised row containers and the models behind them, paired by position,
    /// after asserting that all three rows really exist. A virtualised list that
    /// realised nothing would make every assertion below vacuously true.
    /// </summary>
    /// <summary>
    /// The realised row containers, paired by position with the models the control
    /// built for them.
    /// </summary>
    /// <remarks>
    /// Two preconditions are asserted before anything else, because a virtualised
    /// list that realised nothing, or one that lost the invalid row, would make
    /// every assertion below quietly true. The models are read from the control's
    /// own <c>ItemsSource</c> rather than rebuilt here: a test that reconstructed
    /// them would be asserting against its own fiction.
    /// </remarks>
    private static (ListBoxItem[] Items, PreviewRow[] Rows) RealisedRows(
        Window window, DataPreviewTable table)
    {
        var list = table.GetVisualDescendants().OfType<ListBox>().Single();
        var rows = ((System.Collections.IEnumerable)list.ItemsSource!).Cast<PreviewRow>().ToArray();
        var items = list.GetVisualDescendants().OfType<ListBoxItem>().ToArray();

        Assert.Equal(3, rows.Length);
        Assert.Equal(3, items.Length);
        Assert.True(rows[1].IsInvalid, "precondition: the middle row is the rejected one");
        Assert.False(rows[0].IsInvalid, "precondition: the first row is valid");
        Assert.False(rows[2].IsInvalid, "precondition: the last row is valid");
        Assert.Equal(Reason, rows[1].InvalidReason);

        return (items, rows);
    }

    /// <summary>
    /// The <c>Border</c> the row template produced for this row, located by the
    /// brush and thickness its model specifies.
    /// </summary>
    /// <remarks>
    /// Matching on the model's own values is the point: the lookup can only succeed
    /// if the bindings actually put them on the element. Searching for "any Border"
    /// would pass against the broken control, whose row template contained none at
    /// all — which is exactly the shape of the bug.
    /// </remarks>
    private static Border RowBorderOf(ListBoxItem item, PreviewRow model)
    {
        var matches = item.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => ReferenceEquals(b.BorderBrush, model.RowBorderBrush)
                     && b.BorderThickness == model.RowBorderThickness)
            .ToArray();

        Assert.True(
            matches.Length == 1,
            $"expected exactly one rendered Border carrying this row's treatment; found {matches.Length}");

        return matches[0];
    }

    private static void Settle(Window window)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }
}
