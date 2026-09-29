using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OpdSimulator.App.Services;

namespace OpdSimulator.App.Controls;

/// <summary>
/// The "View calculations" window (Phase 8N, D-165).
/// </summary>
/// <remarks>
/// <para>
/// Phase 8M rendered <see cref="CalculationsTextBuilder.Build"/>'s flat string inside a
/// single monospace <c>TextBlock</c> with <c>TextWrapping.NoWrap</c> inside a 440-pixel
/// window. At that width the longest line is about 74 monospace characters, so every
/// value past column ~57 was cut off and the footer buttons were pushed out of view.
/// </para>
/// <para>
/// The fix is structural rather than cosmetic: the body arrives as
/// <see cref="CalculationRow"/> items and is laid out in a <c>Grid</c> whose columns are
/// <c>Auto</c> (labels, natural width) and <c>*</c> (values, everything else). A value
/// too long for its column wraps instead of being truncated, and the whole body scrolls
/// inside a bounded window so the buttons are always reachable.
/// </para>
/// <para>
/// This is a separate Window rather than a reuse of <see cref="ThemedDialog"/>, whose
/// 440-pixel width is correct for a one-paragraph confirmation and is relied on by four
/// other call sites. It also does not inherit from it — sharing a base would couple the
/// two layouts again.
/// </para>
/// </remarks>
public partial class CalculationsDialog : Window
{
    /// <summary>Indentation, in multiples of <see cref="SpaceIndent"/>, for nested rows.</summary>
    private const double SpaceIndent = 16d;

    /// <summary>Creates the dialog. Rows and copy text are set afterwards, before showing.</summary>
    public CalculationsDialog()
    {
        InitializeComponent();

        // Build the body for the initial (empty) row set straight away, so a dialog
        // whose Rows are never assigned explains itself instead of showing a blank
        // box. ResultsPanel always assigns them; this is the safe default.
        RowsContainer.Content = BuildBody(_rows);
    }

    /// <summary>
    /// The rows to display, in document order. Assigning rebuilds the two-column grid.
    /// </summary>
    /// <remarks>
    /// An empty list is legitimate — it is what <c>BuildRows</c> returns before a run
    /// exists — and renders <see cref="CalculationsTextBuilder.NoRunMessage"/> rather than
    /// an empty box, so the reader is told why there is nothing to see.
    /// </remarks>
    public IReadOnlyList<CalculationRow> Rows
    {
        get => _rows;
        set
        {
            _rows = value ?? Array.Empty<CalculationRow>();
            RowsContainer.Content = BuildBody(_rows);
        }
    }

    private IReadOnlyList<CalculationRow> _rows = Array.Empty<CalculationRow>();

    /// <summary>Text placed on the clipboard by the Copy button; null hides the button.</summary>
    public string? CopyText
    {
        get => _copyText;
        set
        {
            _copyText = value;
            CopyButton.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    private string? _copyText;

    /// <summary>
    /// Builds the body grid: an <c>Auto</c> label column and a <c>*</c> value column.
    /// Section headings span both.
    /// </summary>
    /// <remarks>
    /// Built in code rather than XAML because the row count is decided by the run (two
    /// stages print two service blocks, an eleven-bar run prints eleven utilisation
    /// rows) and because the column span varies by row kind. The window and the button
    /// row stay declarative; only the data-driven body is imperative.
    /// </remarks>
    private static Control BuildBody(IReadOnlyList<CalculationRow> rows)
    {
        if (rows.Count == 0)
        {
            return new TextBlock
            {
                Text = CalculationsTextBuilder.NoRunMessage,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray,
            };
        }

        var grid = new Grid
        {
            // The label column is sized to its content so a long stage name takes the
            // room it needs; the value column takes everything that is left.
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        var rowIndex = 0;
        foreach (var row in rows)
        {
            if (row.Kind == CalculationRowKind.Section)
            {
                var heading = new TextBlock
                {
                    Text = row.Label,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, SpaceIndent, 0, 4),
                };

                // Section headings take the full width; the two columns exist to line
                // labels up against their values, which a heading has none of.
                Grid.SetRow(heading, rowIndex);
                Grid.SetColumn(heading, 0);
                Grid.SetColumnSpan(heading, 2);
                grid.Children.Add(heading);
                rowIndex++;
                continue;
            }

            var label = new TextBlock
            {
                Text = row.Label,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(row.IndentLevel * SpaceIndent, 0, 8, 2),
            };

            // TextWrapping.Wrap, never NoWrap and never a MaxWidth: a long value moves
            // onto the next line. Truncating a figure would be worse than the bug this
            // phase fixes — the reader cannot tell a clipped number from a real one.
            var value = new TextBlock
            {
                Text = row.Value,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 2),
            };

            Grid.SetRow(label, rowIndex);
            Grid.SetColumn(label, 0);
            Grid.SetRow(value, rowIndex);
            Grid.SetColumn(value, 1);
            grid.Children.Add(label);
            grid.Children.Add(value);
            rowIndex++;
        }

        // One star row per content row: the grid grows with the run, and the
        // ScrollViewer's MaxHeight is what bounds the window.
        var rowDefinitions = new RowDefinitions();
        for (var i = 0; i < rowIndex; i++)
        {
            rowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        }

        grid.RowDefinitions = rowDefinitions;
        return grid;
    }

    /// <summary>Runs before the window appears, so focus lands on the default action.</summary>
    /// <param name="e">The opened event.</param>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        CloseButton.Focus();
    }

    /// <summary>Escape closes the dialog, matching every other modal in the app (§16.7).</summary>
    /// <param name="e">The key event; set handled when Escape is consumed.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Copies <see cref="CopyText"/> to the clipboard. Clipboard access can be refused by
    /// the platform, so failure is logged rather than swallowed (AGENTS §12.4).
    /// </summary>
    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(CopyText))
        {
            return;
        }

        try
        {
            var clipboard = TopLevel.GetTopLevel(this) as Avalonia.Input.Platform.IClipboard;
            if (clipboard is null)
            {
                Serilog.Log.Warning("Clipboard unavailable; calculations copy skipped");
                return;
            }

            await clipboard.SetTextAsync(CopyText);
            Serilog.Log.Information("Calculations text copied to clipboard ({Length} chars)", CopyText!.Length);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Copying the calculations text to the clipboard failed");
        }
    }
}
