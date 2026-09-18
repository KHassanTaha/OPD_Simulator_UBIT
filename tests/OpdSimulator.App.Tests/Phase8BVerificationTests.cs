using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8B — simulation-output chi-square verification. The Results panel's
/// "Simulation verification" widget chi-square tests the samples the engine
/// itself generated against the configured distribution (Banks / Law &amp;
/// Kelton model verification). These tests cover the pure verification service
/// (the five <c>VerifyAll_*</c> cases) and the widget lifecycle (empty before a
/// run, populated on completion, cleared by Clear All) plus the FR-UI-14 widget
/// picker count now that the widget is the seventh.
/// </summary>
public class Phase8BVerificationTests
{
    private const double Alpha = 0.05;

    /// <summary>Builds <paramref name="count"/> Exponential(rate) variates through the Core inverse-CDF sampler.</summary>
    private static IReadOnlyList<double> ExponentialSamples(int count, double rate, int seed)
    {
        var sampler = new ExponentialSampler(new SeededRandomSource(seed));
        return Enumerable.Range(0, count).Select(_ => sampler.Sample(rate)).ToList();
    }

    private static SimulationResult ResultWith(
        IReadOnlyList<double> interArrivals,
        IReadOnlyList<IReadOnlyList<double>>? serviceByStage = null,
        IReadOnlyList<StageMetrics>? stages = null)
        => new()
        {
            GeneratedInterArrivalSamples = interArrivals,
            GeneratedServiceSamplesByStage = serviceByStage ?? Array.Empty<IReadOnlyList<double>>(),
            StageMetrics = stages ?? Array.Empty<StageMetrics>(),
        };

    [Fact]
    public void VerifyAll_ExponentialSamples_PassesChiSquare()
    {
        // Genuine Exponential(λ=0.5) output must pass its own goodness-of-fit —
        // the whole point of output-side verification. If the engine or the
        // service accidentally changed the family, p would collapse and this
        // test would fail.
        var result = ResultWith(ExponentialSamples(1000, 0.5, 42));

        var reports = SimulationVerificationService.VerifyAll(result, "Exponential", Array.Empty<string>(), Alpha);

        var report = Assert.Single(reports);
        Assert.Equal(1000, report.SampleCount);
        Assert.NotNull(report.ChiSquare);
        Assert.True(
            report.ChiSquare!.PValue > Alpha,
            $"genuine Exponential output must clear α={Alpha}; observed p={report.ChiSquare.PValue}");
        Assert.True(report.Histogram.HasSeries);
        Assert.Null(report.Note);
    }

    [Fact]
    public void VerifyAll_DeterministicFamily_SkipsChiSquare()
    {
        // A constant stream has no distribution to fit, so verification must
        // skip the test and explain why rather than inventing a verdict.
        var result = ResultWith(Enumerable.Repeat(2.0, 50).ToList());

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "Deterministic", Array.Empty<string>(), Alpha));

        Assert.Null(report.ChiSquare);
        Assert.NotNull(report.Note);
        Assert.Contains("Deterministic", report.Note!);
    }

    [Fact]
    public void VerifyAll_GeneralFamily_TreatedAsExponential()
    {
        // D-126: the engine samples every stage exponentially, so "General"
        // output is verified as Exponential and the note records the flattening.
        var result = ResultWith(ExponentialSamples(1000, 0.5, 7));

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "General", Array.Empty<string>(), Alpha));

        Assert.NotNull(report.ChiSquare);
        Assert.NotNull(report.Note);
        Assert.Contains("General treated as Exponential", report.Note!);
    }

    [Fact]
    public void VerifyAll_ReturnsOneReportPerSeries()
    {
        // A three-stage run must verify four series: inter-arrival plus one per
        // stage, in stage order — the report count drives the widget's card count.
        var stages = new[]
        {
            new StageMetrics { StageName = "Reception" },
            new StageMetrics { StageName = "Screening" },
            new StageMetrics { StageName = "Doctor" },
        };
        var services = new IReadOnlyList<double>[]
        {
            ExponentialSamples(500, 0.8, 1),
            ExponentialSamples(500, 0.6, 2),
            ExponentialSamples(500, 0.4, 3),
        };
        var result = ResultWith(ExponentialSamples(500, 0.4, 9), services, stages);

        var reports = SimulationVerificationService.VerifyAll(
            result, "Exponential", new[] { "Exponential", "Exponential", "Exponential" }, Alpha);

        Assert.Equal(4, reports.Count);
        Assert.Equal(
            new[] { "Inter-arrival", "Reception service", "Screening service", "Doctor service" },
            reports.Select(r => r.Label));
    }

    [Fact]
    public void VerifyAll_InsufficientSamples_ReturnsNullChiSquare()
    {
        // Fewer than two samples cannot fill a chi-square bin; honesty over a
        // fabricated verdict (AGENTS §12 — fail loud, never silently skip).
        var result = ResultWith(new[] { 1.0 });

        var report = Assert.Single(
            SimulationVerificationService.VerifyAll(result, "Exponential", Array.Empty<string>(), Alpha));

        Assert.Null(report.ChiSquare);
        Assert.NotNull(report.Note);
        Assert.Contains("Insufficient", report.Note!);
    }

    [AvaloniaFact]
    public void VerificationWidget_EmptyState_BeforeRun()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;

            Assert.True(main.SimulationVerification.IsEmpty);
            Assert.Equal("Run a simulation to verify its output.", main.SimulationVerification.EmptyMessage);
            Assert.Empty(main.SimulationVerification.Charts);

            // Once a run starts, the Results stack replaces the welcome card and
            // the widget renders its empty state.
            main.Results.StartRun();
            window.UpdateLayout();

            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var empty = panel.GetVisualDescendants().OfType<TextBlock>()
                .SingleOrDefault(t => t.Text == main.SimulationVerification.EmptyMessage);
            Assert.NotNull(empty);
            Assert.True(empty!.IsVisible, "the verification widget must show its empty state before data exists");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task VerificationWidget_PopulatesOnRunCompletion()
    {
        var window = new MainWindow();
        window.Width = 1100;
        window.Height = 2000;
        window.Show();

        var main = (MainViewModel)window.DataContext!;
        var originalVisible = main.Results.VisibleWidgets.ToArray();
        try
        {
            // Gate configuration (Phase 8B): manual three-stage clinic
            // Reception 1 / Screening 2 / Doctor 3, λ=0.5/min, μ=0.8/0.6/0.4,
            // p_exit=0.4, seed 42.
            main.Config.SourceMode = OpdSimulator.App.Models.DataSourceMode.EnterManually;
            main.Config.ManualLambda.Value = "0.5";
            // EnterManually sources μ from the per-stage field, not the comma
            // list (AGENTS §19.4).
            main.Config.StageRows[0].MuValue = "0.8";
            main.Config.StageRows[1].MuValue = "0.6";
            main.Config.StageRows[2].MuValue = "0.4";
            main.Config.StageRows[0].Servers.Value = "1";
            main.Config.StageRows[1].Servers.Value = "2";
            main.Config.StageRows[2].Servers.Value = "3";
            main.Config.PExit.Value = "0.4";
            Assert.True(main.Config.StartCalculationCommand.CanExecute(null), main.Config.StartBlockedMessage);

            // Drive the real MainViewModel run path (Start button → background
            // run → posted completion) so this proves the completion wiring, not
            // just the service.
            main.Config.StartCalculationCommand.Execute(null);
            await WaitUntilAsync(() => !main.SimulationVerification.IsEmpty, TimeSpan.FromSeconds(15));

            Assert.False(main.SimulationVerification.IsEmpty, "run completion must populate the verification widget");
            Assert.Equal(4, main.SimulationVerification.Charts.Count);

            // The gate's high arrival rate must leave every series with enough
            // service samples to fit: output-side chi-square is only meaningful
            // when each card carries real content.
            Assert.All(main.SimulationVerification.Charts, c => Assert.NotNull(c.ChartContent));
            Assert.All(
                main.SimulationVerification.Charts,
                c => Assert.False(string.IsNullOrWhiteSpace(c.Caption), "every verification card must caption its verdict"));

            main.Results.ShowSimulationVerification = true;
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            Assert.Contains(
                panel.GetVisualDescendants().OfType<TextBlock>(),
                t => t.Text == "Simulation verification");

            // Phase 8B gate evidence (D-089 headless): isolate the verification
            // widget, then save the frame the gate references.
            main.Results.ShowMetrics = false;
            main.Results.ShowChiSquare = false;
            main.Results.ShowTrace = false;
            main.Results.ShowUtilisation = false;
            main.Results.ShowQueueLength = false;
            main.Results.ShowWaitHistogram = false;
            window.UpdateLayout();

            var frame = HeadlessScreenshot.Capture(window);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8b-verification.png");
            frame.Save(shotPath);
            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "verification frame missing or suspiciously small");
        }
        finally
        {
            RestoreVisibility(main.Results, originalVisible);
            window.Close();
        }
    }

    private static void RestoreVisibility(ResultsPanelViewModel results, IReadOnlyList<string> originalVisible)
    {
        results.ShowMetrics = originalVisible.Contains("metrics");
        results.ShowChiSquare = originalVisible.Contains("chiSquare");
        results.ShowTrace = originalVisible.Contains("trace");
        results.ShowUtilisation = originalVisible.Contains("utilisation");
        results.ShowQueueLength = originalVisible.Contains("queueLength");
        results.ShowWaitHistogram = originalVisible.Contains("waitHistogram");
        results.ShowSimulationVerification = originalVisible.Contains("simulationVerification");
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
    public void VerificationWidget_ClearedByClearAll()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;
            var stages = new[]
            {
                new StageMetrics { StageName = "Reception" },
                new StageMetrics { StageName = "Screening" },
                new StageMetrics { StageName = "Doctor" },
            };
            var services = new IReadOnlyList<double>[]
            {
                ExponentialSamples(300, 0.8, 1),
                ExponentialSamples(300, 0.6, 2),
                ExponentialSamples(300, 0.4, 3),
            };
            main.SimulationVerification.Apply(
                ResultWith(ExponentialSamples(300, 0.4, 9), services, stages),
                "Exponential",
                new[] { "Exponential", "Exponential", "Exponential" },
                Alpha);
            Assert.False(main.SimulationVerification.IsEmpty);
            Assert.Equal(4, main.SimulationVerification.Charts.Count);

            main.ResetAll();

            Assert.True(main.SimulationVerification.IsEmpty, "Clear All must return the widget to its empty state");
            Assert.Empty(main.SimulationVerification.Charts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void WidgetSelector_ListsExactlySevenWidgets_After8B()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var picker = panel.GetVisualDescendants().OfType<Border>()
                .Single(b => string.Equals(b.Name, "WidgetPicker", StringComparison.Ordinal));
            picker.IsVisible = true;
            window.UpdateLayout();

            string[] contents = picker.GetVisualDescendants().OfType<CheckBox>()
                .Select(c => c.Content?.ToString() ?? string.Empty)
                .ToArray();

            Assert.Equal(7, contents.Length);
            Assert.Contains("Simulation verification", contents);
            Assert.Equal(7, new ResultsPanelViewModel().VisibleWidgets.Count);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }
}