using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpdSimulator.App.Services;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.6 — NFR-6 verification: chart-data preparation must never block the
/// UI thread for more than 100 ms, even on a machine-long run with thousands of
/// recorded queue samples. The production seam is the same one the view models
/// use: <see cref="QueueLengthChartService.Build"/> does the O(n) number
/// crunching and downsampling (≤ <see cref="QueueLengthChartService.MaxPoints"/>
/// points per stage), and the callers wrap it in <c>Task.Run</c> so it runs on
/// the thread pool, not the UI thread.
/// </summary>
public class Phase6c6Nfr6Tests
{
    /// <summary>
    /// Prep of a 10,000-sample series completes on a background (thread-pool)
    /// thread, takes a small fraction of the 100 ms UI-block budget, and is
    /// deterministic — the background result equals the synchronous result, so
    /// a fast-running background prep is not skipping work.
    /// </summary>
    [Fact]
    public void QueueChartPrep_TenThousandSamples_RunsOffTheCallingThread_Under100ms_Deterministic()
    {
        var result = new SimulationResult
        {
            TotalPatientsServed = 10_000,
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Reception",
                    QueueLengthSeries = SyntheticQueueSeries(10_000),
                },
            },
        };

        bool ranOnThreadPoolThread = false;
        var stopwatch = Stopwatch.StartNew();
        QueueLengthChartData data = Task.Run(() =>
            {
                ranOnThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread;
                return QueueLengthChartService.Build(result);
            })
            .GetAwaiter()
            .GetResult();
        stopwatch.Stop();

        // The prep ran off-thread: whatever thread called Run (the UI thread in
        // production), the actual number crunching happened on the thread pool.
        Assert.True(ranOnThreadPoolThread, "chart prep must run on a background thread, never block the UI thread");

        // NFR-6 bound: the whole prep of the largest realistic series is far
        // below the 100 ms UI-block cap. 100 ms is deliberately generous — this
        // O(n) decimation normally takes well under a millisecond — so the
        // assertion catches a regression (e.g. an accidental O(n²) rebuild), not
        // scheduler noise.
        Assert.True(stopwatch.ElapsedMilliseconds < 100,
            $"prep of a 10,000-sample series took {stopwatch.ElapsedMilliseconds} ms; NFR-6 requires < 100 ms");

        // Determinism (G5: the background path may not differ from the sync
        // path): recomputing synchronously yields byte-identical points.
        Assert.True(data.HasSeries);
        QueueLengthChartData sync = QueueLengthChartService.Build(result);
        Assert.Equal(sync.Series.Count, data.Series.Count);
        Assert.Equal(sync.Series[0].Points, data.Series[0].Points);

        // And the downsampling cap itself still holds (NFR-6, D-122).
        Assert.Single(data.Series);
        Assert.True(data.Series[0].Points.Count <= QueueLengthChartService.MaxPoints);
    }

    private static QueueSample[] SyntheticQueueSeries(int count)
    {
        // A plausible queue: starts empty, rises to a long-tail peak, returns to
        // zero. The exact shape does not matter — only that the decimator works
        // over a large realistic series.
        var samples = new QueueSample[count];
        var random = new Random(0); // fixed seed: the test is reproducible
        int length = 0;
        for (int i = 0; i < count; i++)
        {
            length = Math.Max(0, length + random.Next(-1, 2));
            samples[i] = new QueueSample(i, length);
        }

        return samples;
    }
}