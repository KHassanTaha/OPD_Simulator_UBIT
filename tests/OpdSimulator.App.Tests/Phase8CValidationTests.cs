using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8C — analytical (closed-form M/M/c) validation of the simulated
/// results (FR-STAT-11). The pure-logic tests pin the Erlang-C formulas to
/// known values; the comparison tests pin the assumption guard (non-exponential
/// service or an unstable stage yields no rows); the widget tests pin the
/// Results-panel lifecycle; and the final test drives a real engine run and
/// asserts the simulated per-stage wait agrees with the analytical value within
/// 5% on a stable configuration.
/// </summary>
public class Phase8CValidationTests
{
    /// <summary>Builds one stage's metrics with the supplied simulated averages.</summary>
    private static StageMetrics Stage(
        string name,
        double arrivalRate,
        double serviceRate,
        int servers,
        double averageWait,
        double averageQueue) => new()
        {
            StageName = name,
            ArrivalRate = arrivalRate,
            ServiceRate = serviceRate,
            ServerCount = servers,
            AverageWaitMinutes = averageWait,
            AverageQueueLength = averageQueue,
        };

    /// <summary>
    /// Builds a minimal steady-state run result (operating time equals the
    /// <see cref="AnalyticalValidationService.MinimumSteadyStateMinutes"/>
    /// threshold) carrying the supplied stages. Used by the assumption-guard
    /// tests, where the horizon must not be the reason a comparison is refused.
    /// </summary>
    private static SimulationResult ResultWith(params StageMetrics[] stages)
        => new()
        {
            StageMetrics = stages,
            OperatingTimeMinutes = AnalyticalValidationService.MinimumSteadyStateMinutes,
        };

    /// <summary>
    /// Builds a transient run result — one 165-minute clinic day, far below the
    /// steady-state threshold — so the horizon guard is the only reason the
    /// comparison is refused.
    /// </summary>
    private static SimulationResult ShortResultWith(params StageMetrics[] stages)
        => new() { StageMetrics = stages, OperatingTimeMinutes = 165.0 };

    // ── 8C.1 closed-form M/M/c ────────────────────────────────────────────

    [Fact]
    public void ComputeForStage_MM1_MatchesKnownClosedForm()
    {
        // M/M/1 with λ=0.5, μ=1.0 → ρ=0.5. Textbook values: Lq=ρ²/(1−ρ)=0.5,
        // Wq=1.0, W=2.0, L=1.0, P0=0.5. If the Erlang-C summation or the P0
        // normalisation is wrong, every one of these drifts.
        var m = AnalyticalValidationService.ComputeForStage("Reception", 0.5, 1.0, 1);

        Assert.NotNull(m);
        Assert.Equal(0.5, m!.Rho, 4);
        Assert.Equal(0.5, m.Lq, 4);
        Assert.Equal(1.0, m.Wq, 4);
        Assert.Equal(1.0, m.L, 4);
        Assert.Equal(2.0, m.W, 4);
        Assert.Equal(0.5, m.P0, 4);
    }

    [Fact]
    public void ComputeForStage_MM2_MatchesKnownClosedForm()
    {
        // M/M/2 with λ=0.5, μ=0.5 → ρ=0.5. Known values: P0=1/3, Lq=1/3,
        // Wq=2/3. This exercises the c>1 branch of the P0 sum.
        var m = AnalyticalValidationService.ComputeForStage("Screening", 0.5, 0.5, 2);

        Assert.NotNull(m);
        Assert.Equal(0.5, m!.Rho, 4);
        Assert.Equal(1.0 / 3.0, m.Lq, 3);
        Assert.Equal(2.0 / 3.0, m.Wq, 3);
        Assert.Equal(1.0 / 3.0, m.P0, 3);
    }

    [Fact]
    public void ComputeForStage_Unstable_ReturnsNull()
    {
        // λ=1.0, μ=0.5, c=1 → ρ=2. An M/M/c queue with ρ ≥ 1 has no steady
        // state, so returning null is the only honest answer.
        Assert.Null(AnalyticalValidationService.ComputeForStage("Reception", 1.0, 0.5, 1));
    }

    [Fact]
    public void ComputeForStage_ZeroServers_ReturnsNull()
    {
        Assert.Null(AnalyticalValidationService.ComputeForStage("Reception", 0.5, 1.0, 0));
    }

    [Fact]
    public void ComputeForStage_ZeroMu_ReturnsNull()
    {
        Assert.Null(AnalyticalValidationService.ComputeForStage("Reception", 0.5, 0.0, 1));
    }

    // ── 8C.1 comparison guard ────────────────────────────────────────────

    [Fact]
    public void Compare_NonExponentialService_ReturnsEmpty()
    {
        // The closed form is M/M/c only: a Normal service family must suppress
        // the whole table rather than imply an M/M/c verdict that does not apply.
        var result = ResultWith(Stage("Reception", 0.5, 0.8, 1, 2.0, 1.2));

        var rows = AnalyticalValidationService.Compare(
            result,
            "Exponential",
            new[] { "Normal" },
            new (double, double, int)[] { (0.5, 0.8, 1) });

        Assert.Empty(rows);
    }

    [Fact]
    public void Compare_UnstableStage_ReturnsEmpty()
    {
        // Exponential families, but Stage 2 is unstable (ρ=2). One unstable
        // stage invalidates the comparison, so no rows at all are returned.
        var result = ResultWith(
            Stage("Reception", 0.5, 0.8, 1, 2.0, 1.2),
            Stage("Screening", 1.0, 0.5, 1, 9.0, 9.0));

        var rows = AnalyticalValidationService.Compare(
            result,
            "Exponential",
            new[] { "Exponential", "Exponential" },
            new (double, double, int)[] { (0.5, 0.8, 1), (1.0, 0.5, 1) });

        Assert.Empty(rows);
    }

    [Fact]
    public void Compare_ShortHorizon_ReturnsEmpty()
    {
        // Exponential arrivals and service, every stage stable — but the run is
        // one 165-minute clinic day. M/M/c is a steady-state result, so a
        // transient run must yield no rows regardless of the family and rho
        // checks passing (D-137).
        var result = ShortResultWith(
            Stage("Reception", 0.5, 1.0, 1, 4.73, 1.2),
            Stage("Screening", 0.3, 0.5, 2, 1.5, 0.33));

        var rows = AnalyticalValidationService.Compare(
            result,
            "Exponential",
            new[] { "Exponential", "Exponential" },
            new (double, double, int)[] { (0.5, 1.0, 1), (0.3, 0.5, 2) });

        Assert.Empty(rows);
    }

    [Fact]
    public void Compare_ValidConfig_ReturnsOneRowPerStage()
    {
        // All three conditions hold: exponential families, rho < 1 at every
        // stage, and a steady-state horizon (ResultWith defaults to the
        // MinimumSteadyStateMinutes threshold).
        var result = ResultWith(
            Stage("Reception", 0.5, 1.0, 1, 1.05, 0.5),
            Stage("Screening", 0.3, 0.5, 2, 0.66, 0.33));
        Assert.True(result.OperatingTimeMinutes >= AnalyticalValidationService.MinimumSteadyStateMinutes);

        var rows = AnalyticalValidationService.Compare(
            result,
            "Exponential",
            new[] { "Exponential", "Exponential" },
            new (double, double, int)[] { (0.5, 1.0, 1), (0.3, 0.5, 2) });

        Assert.Equal(2, rows.Count);
        Assert.Equal("Reception", rows[0].StageName);
        Assert.Equal("Screening", rows[1].StageName);
        Assert.Equal(1.0, rows[0].AnalyticalAvgWait, 3);
        // 5% above the analytical Wq of 1.0 → exactly 5.0%.
        Assert.Equal(5.0, rows[0].DeltaPercent, 3);
    }

    // ── 8C.3 widget lifecycle ────────────────────────────────────────────

    [AvaloniaFact]
    public void Widget_HiddenWhenNotApplicable()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;
            main.Results.StartRun();

            // Otherwise-valid config (exponential families, stable stages), but
            // a transient 165-minute clinic day. The steady-state guard refuses
            // the comparison, so the widget keeps its empty state and explains
            // why (FR-STAT-11, D-137).
            var result = ShortResultWith(Stage("Reception", 0.5, 1.0, 1, 4.73, 1.2));
            main.AnalyticalValidation.Apply(
                result,
                "Exponential",
                new[] { "Exponential" },
                new (double, double, int)[] { (0.5, 1.0, 1) });

            Assert.True(main.AnalyticalValidation.IsEmpty);
            Assert.False(main.AnalyticalValidation.HasRows);
            Assert.Empty(main.AnalyticalValidation.Rows);

            window.UpdateLayout();
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var empty = panel.GetVisualDescendants().OfType<TextBlock>()
                .SingleOrDefault(t => t.Text == main.AnalyticalValidation.EmptyMessage);
            Assert.NotNull(empty);
            Assert.True(empty!.IsVisible, "the widget must explain why no comparison is shown");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Widget_ShowsRows_WhenApplicable()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = (MainViewModel)window.DataContext!;
            main.Results.StartRun();

            var result = ResultWith(
                Stage("Reception", 0.5, 1.0, 1, 1.0, 0.5),
                Stage("Screening", 0.3, 0.5, 2, 0.66, 0.33));
            main.AnalyticalValidation.Apply(
                result,
                "Exponential",
                new[] { "Exponential", "Exponential" },
                new (double, double, int)[] { (0.5, 1.0, 1), (0.3, 0.5, 2) });

            Assert.False(main.AnalyticalValidation.IsEmpty);
            Assert.True(main.AnalyticalValidation.HasRows);
            Assert.Equal(2, main.AnalyticalValidation.Rows.Count);

            window.UpdateLayout();
            var panel = window.GetVisualDescendants().OfType<ResultsPanel>().Single();
            var text = panel.GetVisualDescendants().OfType<TextBlock>().ToList();
            Assert.Contains(text, t => t.Text == "Analytical validation (M/M/c)");
            Assert.Contains(text, t => t.Text == "Reception");
            var empty = text.SingleOrDefault(t => t.Text == main.AnalyticalValidation.EmptyMessage);
            Assert.NotNull(empty);
            Assert.False(empty!.IsVisible, "the explanation must be hidden once rows exist");
        }
        finally
        {
            window.Close();
        }
    }

    // ── 8C gate: a real run must agree with theory within 5% ─────────────

    [AvaloniaFact]
    public async Task Widget_DeltaPercent_Below5ForStableRun()
    {
        var window = new MainWindow();
        window.Width = 1100;
        window.Show();
        var main = (MainViewModel)window.DataContext!;
        var originalVisible = main.Results.VisibleWidgets.ToArray();
        try
        {
            // Gate configuration: manual three-stage clinic Reception 1 /
            // Screening 2 / Doctor 3, λ=0.5/min, μ=0.8/0.6/0.4, p_exit=0.4,
            // seed 42. The M/M/c comparison is a steady-state result, so the run
            // must be a long, uninterrupted horizon (DiagnosticTrace) rather than
            // the default one-day calendar window; tracing is switched off so the
            // long run stays cheap.
            main.Config.SourceMode = DataSourceMode.EnterManually;
            main.Config.IsDiagnosticTrace = true;
            main.Config.HorizonMinutes.Value = "200000";
            main.Config.AdvancedIsOptionalEnabled = true;
            main.Config.Seed.Value = "42";
            main.Config.TraceLevel = "None";
            main.Config.ManualLambda.Value = "0.5";
            main.Config.StageRows[0].MuValue = "0.8";
            main.Config.StageRows[1].MuValue = "0.6";
            main.Config.StageRows[2].MuValue = "0.4";
            main.Config.StageRows[0].Servers.Value = "1";
            main.Config.StageRows[1].Servers.Value = "2";
            main.Config.StageRows[2].Servers.Value = "3";
            main.Config.PExit.Value = "0.4";
            Assert.True(main.Config.StartCalculationCommand.CanExecute(null), main.Config.StartBlockedMessage);

            main.Config.StartCalculationCommand.Execute(null);
            await WaitUntilAsync(() => !main.AnalyticalValidation.IsEmpty, TimeSpan.FromSeconds(120));

            Assert.False(main.AnalyticalValidation.IsEmpty, "run completion must populate the analytical-validation widget");
            Assert.Equal(3, main.AnalyticalValidation.Rows.Count);
            Assert.All(
                main.AnalyticalValidation.Rows,
                row => Assert.True(
                    row.DeltaPercent < 5.0,
                    $"{row.StageName}: Δ={row.DeltaPercent:0.##}% (sim {row.SimulatedAvgWait:0.###} min vs M/M/c {row.AnalyticalAvgWait:0.###} min)"));

            // Phase 8C gate evidence (D-089 headless): isolate the analytical
            // widget, then save the frame the gate references.
            main.Results.ShowMetrics = false;
            main.Results.ShowChiSquare = false;
            main.Results.ShowTrace = false;
            main.Results.ShowUtilisation = false;
            main.Results.ShowQueueLength = false;
            main.Results.ShowWaitHistogram = false;
            main.Results.ShowSimulationVerification = false;
            window.UpdateLayout();

            var frame = HeadlessScreenshot.Capture(window);
            var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var shotPath = Path.Combine(shotDir, "phase-8c-analytical.png");
            frame.Save(shotPath);
            Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
                "analytical-validation frame missing or suspiciously small");
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
        results.ShowAnalyticalValidation = originalVisible.Contains("analyticalValidation");
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

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }
    }
}
