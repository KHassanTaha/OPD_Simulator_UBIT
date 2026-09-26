using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8F gate — three UI fixes.
/// </summary>
/// <remarks>
/// <para><b>8F.1 (theming, critical).</b> Avalonia's Fluent theme asks the OS
/// for an accent colour, so the CollapsibleSection header rendered blue on one
/// machine and orange on another. The accent must be pinned to the brand green
/// so the UI is identical everywhere. The fix belongs on
/// <see cref="FluentTheme.Palettes"/>, not on loose <c>SystemAccentColor</c>
/// resources: in Avalonia 11.3.3 no such key is consulted, which the tests
/// below assert rather than assume.</para>
///
/// <para><b>8F.2 (layout).</b> The last config section could not scroll clear
/// of the pinned footer, so its bottom edge was clipped. Scrollable content
/// needs trailing room, and <c>ScrollViewer.Padding</c> does not extend the
/// scrollable extent — hence a real spacer element inside the content.</para>
///
/// <para><b>8F.3 (behaviour).</b> Changing a stage's server count left the
/// Model notation showing a stale server count. The two fields must agree in
/// both directions without bouncing re-entrantly between each other.</para>
/// </remarks>
public class Phase8FTests
{
    // ── 8F.1 — the accent must be brand green, not the OS accent ───────────

    /// <summary>
    /// Why it matters: this is the resource every Fluent highlight brush is
    /// derived from. If it is not the brand green the whole app inherits the
    /// machine's accent, which is the reported bug. The second assertion is
    /// the regression guard: <c>SystemAccentColor</c> does not exist in 11.3.3,
    /// so a fix that only defines those loose keys would leave this brush blue
    /// and still "pass" a key-existence test.
    /// </summary>
    [AvaloniaFact]
    public void ThemeAccent_IsBrandGreen_NotSystemDefault()
    {
        var app = Application.Current!;
        Assert.True(app.Styles.OfType<FluentTheme>().Any(), "the app must still use the Fluent theme");

        var brand = (Color)Theme("ColorBrandGreen");

        // The loose SystemAccent* keys Fluent used in older versions are not a
        // thing in 11.3.3 — assert that, so nobody re-attempts that fix.
        Assert.False(app.Resources.TryGetResource("SystemAccentColor", null, out _),
            "Avalonia 11.3.3 has no 'SystemAccentColor' resource; overriding it cannot work (see D-141)");

        var host = new Window { Width = 300, Height = 150, Content = new Button { Content = "probe" } };
        host.Show();
        try
        {
            host.UpdateLayout();
            var button = (Button)host.Content!;

            Assert.True(
                button.TryFindResource("SystemControlHighlightAccentBrush", button.ActualThemeVariant, out var accent),
                "Fluent must expose SystemControlHighlightAccentBrush to controls");
            var accentBrush = Assert.IsAssignableFrom<ISolidColorBrush>(accent);

            Assert.Equal(brand, accentBrush.Color);
            Assert.NotEqual(Color.FromRgb(0x00, 0x78, 0xD7), accentBrush.Color);

            // Every variant the app can request must be pinned, not just Light.
            // (Palettes rejects Default — only Light and Dark are supported.)
            var fluent = app.Styles.OfType<FluentTheme>().First();
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                Assert.True(fluent.Palettes.ContainsKey(variant), $"palette for {variant} must be pinned");
                Assert.Equal(brand, fluent.Palettes[variant].Accent);
            }
        }
        finally
        {
            host.Close();
        }
    }

    /// <summary>
    /// Why it matters: the reported symptom is on the section header when it is
    /// focused, and the header is a Fluent-styled ToggleButton — so its focus
    /// visuals are drawn from the accent resource. The title and chevron must
    /// stay on the brand-green bar, and the accent the header resolves while
    /// focused must still be the brand green.
    /// </summary>
    [AvaloniaFact]
    public void CollapsibleSectionHeader_Focused_StaysBrandGreen()
    {
        var brand = (Color)Theme("ColorBrandGreen");
        var onBrand = Assert.IsAssignableFrom<ISolidColorBrush>(Theme("BrushTextOnBrand"));

        var host = new Window
        {
            Width = 420,
            Height = 220,
            Content = new CollapsibleSection { Title = "Advanced", IsExpanded = true },
        };
        host.Show();

        try
        {
            host.UpdateLayout();
            var section = (CollapsibleSection)host.Content!;
            var header = section.GetVisualDescendants().OfType<ToggleButton>().First();

            // Tab in from the window so the keyboard-focus path is genuinely
            // exercised (programmatic Focus() alone does not engage
            // :focus-visible, which is why an earlier probe saw no difference).
            host.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "Tab");
            host.UpdateLayout();
            Assert.True(header.IsKeyboardFocusWithin,
                "the header toggle must be reachable by keyboard — it is the collapse control");

            // The title and chevron sit on the green bar: on-brand, never accent.
            var title = section.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Advanced");
            var titleBrush = Assert.IsAssignableFrom<ISolidColorBrush>(title.Foreground);
            Assert.Equal(onBrand.Color, titleBrush.Color);

            var chevron = section.GetVisualDescendants()
                .OfType<global::Avalonia.Controls.Shapes.Path>().First();
            Assert.Equal(onBrand.Color, Assert.IsAssignableFrom<ISolidColorBrush>(chevron.Stroke).Color);

            // The bar itself is the brand green regardless of focus.
            var bar = section.GetVisualDescendants()
                .OfType<Border>().First(b => B(b.Background) == brand.ToString());
            Assert.NotNull(bar);

            // And the accent the focused header would draw any highlight from
            // is the brand green, not the OS accent.
            Assert.True(header.TryFindResource("SystemControlHighlightAccentBrush", header.ActualThemeVariant, out var accent));
            Assert.Equal(brand, Assert.IsAssignableFrom<ISolidColorBrush>(accent).Color);

            // Nothing on the header may resolve to the machine accent.
            foreach (var brush in new IBrush?[]
                     {
                         header.Background, header.BorderBrush, header.Foreground,
                         title.Foreground, chevron.Stroke,
                     })
            {
                if (brush is ISolidColorBrush solid)
                {
                    Assert.NotEqual(Color.FromRgb(0x00, 0x78, 0xD7), solid.Color);
                }
            }
        }
        finally
        {
            host.Close();
        }
    }

    // ── 8F.2 — the last section must scroll clear of the pinned footer ──────

    /// <summary>
    /// Why it matters: the user could not see the whole of the last section
    /// (including the Random seed field) because the footer clipped it. This
    /// scrolls to the very bottom and asserts the last section's bottom edge is
    /// inside the scroll viewport, i.e. above the pinned footer.
    /// </summary>
    [AvaloniaFact]
    public void ConfigPanel_ScrollToBottom_LastSectionFullyVisible()
    {
        var spaceL = (double)Theme("SpaceL");

        var host = new Window
        {
            Width = 520,
            Height = 380, // deliberately short so the panel must scroll
            Content = new ConfigPanel { DataContext = new ConfigPanelViewModel() },
        };
        host.Show();

        try
        {
            host.UpdateLayout();

            var scroller = host.GetVisualDescendants().OfType<ScrollViewer>().First();
            var sections = host.GetVisualDescendants().OfType<CollapsibleSection>().ToList();
            var last = sections.Last();

            // Precondition: the content really is taller than the viewport,
            // otherwise this test would pass without exercising scrolling.
            Assert.True(scroller.Extent.Height > scroller.Viewport.Height,
                $"config content ({scroller.Extent.Height}) must exceed the viewport ({scroller.Viewport.Height})");

            scroller.Offset = new Vector(0, scroller.Extent.Height);
            host.UpdateLayout();

            // The footer is not inside the scroller, so the scroller's bottom
            // edge in window coordinates is where the footer starts.
            var viewportPoint = scroller.TranslatePoint(new Point(0, scroller.Viewport.Height), host);
            var footerPoint = host.GetVisualDescendants().OfType<PinnedFooterBar>().Single()
                .TranslatePoint(new Point(0, 0), host);
            Assert.NotNull(viewportPoint);
            Assert.NotNull(footerPoint);
            var viewportBottom = viewportPoint!.Value.Y;
            var footerTop = footerPoint!.Value.Y;

            Assert.True(viewportBottom <= footerTop + 0.5,
                $"scroll viewport must end at or above the footer (viewport {viewportBottom}, footer {footerTop})");

            var lastPoint = last.TranslatePoint(new Point(0, last.Bounds.Height), host);
            Assert.NotNull(lastPoint);
            var lastBottom = lastPoint!.Value.Y;
            Assert.True(lastBottom <= viewportBottom + 0.5,
                $"last section bottom ({lastBottom}) must be fully visible above the footer ({viewportBottom})");

            // The buffer is token-driven, not a hard-coded number.
            var spacers = scroller.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Bounds.Height > 0 && B(b.Background) is null or "Transparent")
                .ToList();
            Assert.Contains(spacers, b => Math.Abs(b.Bounds.Height - spaceL) < 0.5);
        }
        finally
        {
            host.Close();
        }
    }

    // ── 8F.3 — model notation and server count must agree ───────────────────

    /// <summary>
    /// Why it matters: this is the reported bug. Typing 3 servers must make the
    /// notation read M/M/3, not leave M/M/2 selected — otherwise the two fields
    /// disagree and the user cannot tell which one the run will use.
    /// </summary>
    [Fact]
    public void StageRow_ServersChanged_UpdatesModelNotation()
    {
        var row = new StageRow { SelectedModel = "M/M/2" };
        Assert.Equal("2", row.Servers.Value);

        row.Servers.Value = "3";

        Assert.Equal("M/M/3", row.SelectedModel);
    }

    /// <summary>
    /// The forward direction must keep working: the notation is the authority
    /// when the user picks it, and it must still drive the server count (plus
    /// the two distribution families).
    /// </summary>
    [Fact]
    public void StageRow_ModelChanged_UpdatesServers()
    {
        var row = new StageRow { SelectedModel = "M/M/1" };
        Assert.Equal("1", row.Servers.Value);

        row.SelectedModel = "M/M/4";

        Assert.Equal("4", row.Servers.Value);
        Assert.Equal("Exponential", row.ArrivalFamily);
        Assert.Equal("Exponential", row.ServiceFamily);

        // A different family must survive the round trip too.
        row.SelectedModel = "M/D/2";
        Assert.Equal("2", row.Servers.Value);
        Assert.Equal("Deterministic", row.ServiceFamily);
    }

    /// <summary>
    /// Why it matters: the two fields notify each other. Without a re-entrancy
    /// guard the write to one re-enters the other, which either recurses or
    /// rewrites the field the user is still typing in. A single user action must
    /// produce exactly one round of cross-updates and then settle, and the guard
    /// must be released even if a parse throws.
    /// </summary>
    [Fact]
    public void StageRow_SyncIsNotReentrant()
    {
        var row = new StageRow { SelectedModel = "M/M/1" };

        var modelChanges = new System.Collections.Generic.List<string?>();
        var valueChanges = 0;
        row.PropertyChanged += (_, e) => modelChanges.Add(e.PropertyName);
        row.Servers.ValueChanged += (_, _) => valueChanges++;

        // One user edit → exactly one cross-update to the notation. The reverse
        // write-back is a NO-OP because the value already agrees, so the field
        // raises exactly one ValueChanged (the user's own edit). A missing guard
        // makes the two bounce and these counts run away.
        row.Servers.Value = "5";
        Assert.Equal("M/M/5", row.SelectedModel);
        Assert.Equal("5", row.Servers.Value);
        Assert.Single(modelChanges);
        Assert.Equal(nameof(StageRow.SelectedModel), modelChanges[0]);
        Assert.Equal(1, valueChanges);

        // Settles: re-applying identical values must not write anything again.
        modelChanges.Clear();
        valueChanges = 0;
        row.SelectedModel = "M/M/5";
        row.Servers.Value = "5";
        Assert.Empty(modelChanges);
        Assert.Equal(0, valueChanges);
        Assert.Equal("M/M/5", row.SelectedModel);
        Assert.Equal("5", row.Servers.Value);

        // An unparseable notation must not throw out of a property setter, and
        // must leave the row usable (this is the crash the naive sync caused).
        row.SelectedModel = "M/M/99";
        Assert.Equal("5", row.Servers.Value);
    }

    /// <summary>
    /// Why it matters: the notation dropdown only offers 1–5 servers, and
    /// <c>ModelNotationParser.Parse</c> throws outside that range. A naive sync
    /// that appends the raw number would fabricate "M/M/7", which is not in the
    /// list and would throw on the next model change. Out-of-range and
    /// half-typed values must leave the notation untouched — the inline field
    /// error owns that case — while still keeping the row consistent.
    /// </summary>
    [Fact]
    public void StageRow_ServersOutsideNotationRange_LeavesModelAlone()
    {
        var row = new StageRow { SelectedModel = "M/M/2" };

        foreach (var bad in new[] { "", "0", "-1", "abc", "9" })
        {
            row.Servers.Value = bad;
            Assert.Equal("M/M/2", row.SelectedModel);
        }

        // In-range values still sync after all that.
        row.Servers.Value = "4";
        Assert.Equal("M/M/4", row.SelectedModel);
        Assert.Equal("4", row.Servers.Value);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static object Theme(string key)
    {
        var app = Application.Current!;
        Assert.True(app.Resources.TryGetResource(key, null, out var value), $"theme resource '{key}' must resolve");
        return value!;
    }

    private static string B(IBrush? brush) => brush switch
    {
        null => "<null>",
        ISolidColorBrush s => s.Color.ToString(),
        _ => brush.GetType().Name,
    };
}
