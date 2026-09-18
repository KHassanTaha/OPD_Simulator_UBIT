using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Shared headless capture helper for every screenshot test. Under full-suite
/// load the Avalonia headless renderer occasionally has not produced a
/// composited frame when <c>CaptureRenderedFrame</c> is called, which returned
/// null and flaked <see cref="Phase6c2Screenshot"/>, <see cref="ControlsDemoScreenshot"/>
/// and <see cref="Phase5Screenshot"/>.
///
/// The helper stays fully synchronous on purpose: an async <c>[AvaloniaFact]</c>
/// intermittently throws <c>System.PlatformNotSupportedException</c> from
/// <c>Avalonia.Threading.Dispatcher.PushFrame</c> via
/// <c>HeadlessUnitTestSession.DispatchCore</c> (verified on Avalonia.Headless
/// 11.3.3), so awaiting inside a headless test is not safe here. Instead the
/// helper forces the render timer to tick and drains the dispatcher queue,
/// retrying a bounded number of times before failing loud — no silent blank PNGs.
/// </summary>
internal static class HeadlessScreenshot
{
    private const int MaxAttempts = 10;

    /// <summary>
    /// Forces a layout pass, ticks the headless render timer, drains queued
    /// dispatcher jobs, and captures the window frame. Retries the tick+flush up
    /// to <see cref="MaxAttempts"/> times; fails the test if no frame is produced.
    /// </summary>
    /// <param name="window">The shown window to capture.</param>
    /// <returns>The captured frame (never null — asserted before returning).</returns>
    internal static WriteableBitmap Capture(Window window)
    {
        WriteableBitmap? frame = null;

        for (int attempt = 0; attempt < MaxAttempts && frame is null; attempt++)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            frame = window.CaptureRenderedFrame();

            if (frame is null)
            {
                // The headless rasteriser runs off the UI thread; release the UI
                // thread briefly so it can publish a composited frame.
                Thread.Sleep(25);
            }
        }

        Assert.NotNull(frame);
        return frame!;
    }
}
