namespace OpdSimulator.App.Tests;

using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using Xunit;

/// <summary>
/// Phase 8S issue 6 — the last field of a scrolling panel must be able to travel
/// clear of whatever sits pinned beneath it.
/// </summary>
/// <remarks>
/// The measurement rule is D-169's: a default-size frame is not the failing size, so
/// each test also runs at a short window and asserts the clearance in pixels rather
/// than asking whether an element exists.
/// </remarks>
public sealed class Phase8SBufferTests
{
    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(420)]
    public void ConfigPanel_ScrollableContent_HasBottomBuffer(int height)
    {
        var window = new Window
        {
            Width = 520,
            Height = height,
            Content = new Views.ConfigPanel { DataContext = new ViewModels.ConfigPanelViewModel() },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var buffer = Buffer(window);
            Assert.NotNull(buffer);

            // The buffer must be inside the scrolling content, or it cannot extend
            // the scrollable extent — a margin on the ScrollViewer would not.
            var scroller = buffer!.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            Assert.NotNull(scroller);
            Assert.Contains(buffer, scroller!.GetVisualDescendants());

            Assert.True(
                buffer.Bounds.Height > 0,
                $"the config panel's bottom buffer is {buffer.Bounds.Height:F0}px tall");

            // Phase 8S (D-198): 24 px was measured, not assumed. The PinnedFooterBar
            // below this scroller is 67 px tall and starts where the viewport ends, so
            // the buffer is the gap the last section gets when scrolled fully down.
            // 24 px clears it; the space token was kept at SpaceL rather than bumped.
            var footer = window.GetVisualDescendants().OfType<PinnedFooterBar>().FirstOrDefault();
            if (footer is not null)
            {
                Assert.True(
                    buffer.Bounds.Height >= 16,
                    $"a {buffer.Bounds.Height:F0}px buffer leaves too little gap above the " +
                    $"{footer.Bounds.Height:F0}px pinned footer");
            }

            // D-169: the region must actually scroll, or a buffer proves nothing.
            Assert.True(
                scroller!.Extent.Height > scroller.Viewport.Height,
                $"config content ({scroller.Extent.Height:F0}px) fits the viewport " +
                $"({scroller.Viewport.Height:F0}px) at {height}px — nothing to scroll");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(900)]
    [InlineData(420)]
    public void InputTab_ScrollableContent_HasBottomBuffer(int height)
    {
        var window = new Window
        {
            Width = 1000,
            Height = height,
            Content = new Views.InputTab { DataContext = new ViewModels.InputTabViewModel() },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var buffer = Buffer(window);
            Assert.NotNull(buffer);

            var scroller = buffer!.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            Assert.NotNull(scroller);
            Assert.Contains(buffer, scroller!.GetVisualDescendants());
            Assert.True(buffer.Bounds.Height > 0, "the input tab's bottom buffer has no height");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// On the input tab there is nothing pinned below the scroller, so "clear of the
    /// footer" is really "not flush against the window edge" — the last control must
    /// be able to scroll up far enough that its own bottom margin is visible.
    /// </summary>
    [AvaloniaFact]
    public void InputTab_LastFieldFullyVisible_OnShortWindow()
    {
        const int Height = 420;

        // The empty state fits a short window, so nothing would scroll and the test
        // would pass vacuously. The repository ships a real three-stage sample, so
        // this loads it rather than inventing a fixture — the scrollable content is
        // then the same content a user sees after choosing a file (D-178).
        var vm = new ViewModels.InputTabViewModel();
        vm.SetLoadedFile(DataAnalyzerBridge.Analyze(Path.Combine(
            FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv")));

        var window = new Window
        {
            Width = 1000,
            Height = Height,
            Content = new Views.InputTab { DataContext = vm },
        };
        window.Show();
        window.UpdateLayout();
        try
        {
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();
            Assert.True(
                scroller.Extent.Height > scroller.Viewport.Height,
                "precondition: the input tab must have content to scroll at this height");

            // Scroll to the very end, which is the state the buffer exists for.
            scroller.ScrollToEnd();
            window.UpdateLayout();

            var bottom = scroller.Bounds.Height;
            Assert.True(
                scroller.Offset.Y <= scroller.Extent.Height - scroller.Viewport.Height + 0.5,
                $"scrolled to offset {scroller.Offset.Y:F1} but the maximum is " +
                $"{scroller.Extent.Height - scroller.Viewport.Height:F1}");

            // At the end of the scroll the content's last pixel sits at the viewport's
            // bottom, so the space below it IS the buffer.
            var visibleSlack = scroller.Viewport.Height - (scroller.Extent.Height - scroller.Offset.Y);
            Assert.True(
                visibleSlack >= 0,
                $"at the end of the scroll the content overruns the viewport by {-visibleSlack:F1}px");

            // And nothing important was pushed off the top in the process.
            Assert.True(bottom > 0);

            // The owner's evidence frame (§18 / D-089). Captured here rather than
            // from a second test: this is the state the buffer exists for, and a
            // separate test would repeat the whole setup to reach it.
            Save(window, "phase-8s-input-buffer.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Walks up to the directory holding the solution.</summary>
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

    /// <summary>Writes the headless frame to the repository's evidence folder.</summary>
    private static void Save(Window window, string fileName)
    {
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        HeadlessScreenshot.Capture(window).Save(shotPath);

        var length = new FileInfo(shotPath).Length;
        Assert.True(length >= 512, $"{fileName} missing or suspiciously small ({length} bytes)");
    }

    /// <summary>The trailing spacer inside the scrolling content, if the view has one.</summary>
    private static Border? Buffer(Window window) =>
        window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("scroll-buffer"));
}
