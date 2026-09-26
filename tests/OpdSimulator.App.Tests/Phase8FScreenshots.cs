using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8F gate evidence — the two theme/layout fixes captured visually, run
/// headlessly per the D-089 method.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>(b) <c>phase-8f-config-bottom.png</c> — the config panel scrolled to
/// the very bottom, showing the last section (with its Random seed field) fully
/// visible above the pinned footer instead of clipped by it (8F.2).</item>
/// <item>(a) <c>phase-8f-focus-green.png</c> — a CollapsibleSection header with
/// keyboard focus, on the brand-green bar, with the accent-derived highlight
/// pinned to the brand green rather than the machine's OS accent (8F.1).</item>
/// </list>
///
/// The exact values are asserted in <see cref="Phase8FTests"/>; these captures
/// exist so a human can see both surfaces in their fixed state.
/// </remarks>
public class Phase8FScreenshots
{
    [AvaloniaFact]
    public void Render_ConfigScrolledToBottom_SavePhase8fConfigBottomPng()
    {
        var window = new Window
        {
            Width = 560,
            Height = 420,
            Content = new ConfigPanel { DataContext = new ConfigPanelViewModel() },
        };
        window.Show();

        try
        {
            window.UpdateLayout();
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();

            // Scroll to the true maximum, the state the user could not reach.
            scroller.Offset = new Vector(0, scroller.Extent.Height);

            var frame = HeadlessScreenshot.Capture(window);
            Save(frame, "phase-8f-config-bottom.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_FocusedHeaderStaysGreen_SavePhase8fFocusGreenPng()
    {
        var window = new Window
        {
            Width = 560,
            Height = 260,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Children =
                {
                    new TextBlock
                    {
                        Text = "8F.1 — the section header keeps the brand green when focused",
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 16),
                    },
                    new CollapsibleSection
                    {
                        Title = "Advanced",
                        IsExpanded = true,
                        Content = new TextBlock
                        {
                            Text = "Focused via Tab. The accent Fluent would normally take "
                                 + "from the OS is pinned to the brand green.",
                            Margin = new Thickness(16),
                        },
                    },
                },
            },
        };
        window.Show();

        try
        {
            // Keyboard focus, so the focus visuals are genuinely exercised.
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "Tab");
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "Tab");

            var header = window.GetVisualDescendants().OfType<ToggleButton>().First();
            Assert.True(header.IsKeyboardFocusWithin, "the header must hold keyboard focus for this capture");

            var frame = HeadlessScreenshot.Capture(window);
            Save(frame, "phase-8f-focus-green.png");
        }
        finally
        {
            window.Close();
        }
    }

    private static void Save(WriteableBitmap frame, string fileName)
    {
        var dir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        frame.Save(path);
        Assert.True(File.Exists(path) && new FileInfo(path).Length >= 512,
            $"{fileName} missing or suspiciously small");
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
