using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8V — the arrival process is configurable (FR-STAT-5, D-211) and each
/// analytical model states its own applicability (D-212).
/// </summary>
/// <remarks>
/// The service tests assert the REFERENCE NUMBER, not merely that a row exists: a
/// G/G/c comparison that silently used Ca² = 1 would still produce a table, and
/// only the value can tell the two apart. The config tests assert the spec the
/// form builds, because a dropdown that changes a label without changing the
/// distribution is exactly the defect this phase exists to remove.
/// </remarks>
public class Phase8VGeneralArrivalTests
{
    // ── Analytical applicability and Ca² ─────────────────────────────────

    private static StageMetrics Stage(
        string name = "Reception",
        double arrivalRate = 0.5,
        double serviceRate = 0.8,
        int servers = 1,
        double averageWait = 2.0,
        double averageQueue = 1.0) => new()
        {
            StageName = name,
            ArrivalRate = arrivalRate,
            ServiceRate = serviceRate,
            ServerCount = servers,
            AverageWaitMinutes = averageWait,
            AverageQueueLength = averageQueue,
        };

    private static SimulationResult SteadyResult(params StageMetrics[] stages) => new()
    {
        StageMetrics = stages,
        OperatingTimeMinutes = AnalyticalValidationService.MinimumSteadyStateMinutes,
    };

    private static DistributionSpec Gamma(double mean, double shape)
        => new(DistributionFamily.Gamma, mean, Shape: shape, Scale: mean / shape);

    [Fact]
    public void TryCompare_Mmc_RefusesGammaArrivals_AndNamesGgc()
    {
        var result = SteadyResult(Stage());
        var arrival = Gamma(mean: 2.0, shape: 4.0); // Ca² = 0.25

        bool ok = AnalyticalValidationService.TryCompare(
            result,
            "Gamma",
            new[] { "Exponential" },
            new (double, double, int)[] { (0.5, 0.8, 1) },
            AnalyticalModelFamily.Mmc,
            new[] { new DistributionSpec(DistributionFamily.Exponential, 1.25) },
            arrival,
            out _,
            out string? refusal);

        Assert.False(ok);
        Assert.Contains("M/M/c requires exponential (Poisson) inter-arrival times", refusal);
        Assert.Contains("Ca² = 0.25", refusal);
        Assert.Contains("G/G/c", refusal);
    }

    [Fact]
    public void TryCompare_Mgc_RefusesGammaArrivals()
    {
        var result = SteadyResult(Stage());

        bool ok = AnalyticalValidationService.TryCompare(
            result,
            "Gamma",
            new[] { "Exponential" },
            new (double, double, int)[] { (0.5, 0.8, 1) },
            AnalyticalModelFamily.Mgc,
            null,
            Gamma(mean: 2.0, shape: 4.0),
            out _,
            out string? refusal);

        Assert.False(ok);
        Assert.Contains("M/G/c requires exponential (Poisson) inter-arrival times", refusal);
        Assert.Contains("G/G/c", refusal);
    }

    [Fact]
    public void TryCompare_Ggc_UsesTheConfiguredArrivalCa2()
    {
        // M/M/1 Wq at λ = 0.5, μ = 0.8 is 2.08333. G/G/c scales it by
        // (Ca² + Cs²)/2 = (0.25 + 2)/2 = 1.125 → 2.34375. A code path that
        // assumed Ca² = 1 would report 3.125 instead.
        var result = SteadyResult(Stage());
        var arrival = Gamma(mean: 2.0, shape: 4.0);
        var service = Gamma(mean: 1.25, shape: 0.5); // Cs² = 2

        bool ok = AnalyticalValidationService.TryCompare(
            result,
            "Gamma",
            new[] { "Gamma" },
            new (double, double, int)[] { (0.5, 0.8, 1) },
            AnalyticalModelFamily.Ggc,
            new[] { service },
            arrival,
            out IReadOnlyList<ComparisonRow> rows,
            out string? refusal);

        Assert.True(ok, refusal);
        Assert.Single(rows);
        Assert.Equal(2.34375, rows[0].AnalyticalAvgWait, 6);
        Assert.Equal(1.171875, rows[0].AnalyticalQueueLength, 6);
    }

    [Fact]
    public void TryCompare_TransientRun_NamesTheThreshold()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[] { Stage() },
            OperatingTimeMinutes = 165.0,
        };

        bool ok = AnalyticalValidationService.TryCompare(
            result,
            "Exponential",
            new[] { "Exponential" },
            new (double, double, int)[] { (0.5, 0.8, 1) },
            AnalyticalModelFamily.Mmc,
            null,
            null,
            out _,
            out string? refusal);

        Assert.False(ok);
        Assert.Contains("165 simulated minutes", refusal);
        Assert.Contains("100000", refusal);
    }

    [Fact]
    public void TryCompare_NonExponentialFamilyWithoutASpec_IsUnknownNotOne()
    {
        var result = SteadyResult(Stage());

        bool ok = AnalyticalValidationService.TryCompare(
            result,
            "Gamma",
            new[] { "Exponential" },
            new (double, double, int)[] { (0.5, 0.8, 1) },
            AnalyticalModelFamily.Ggc,
            null,
            arrivalSpec: null,
            out _,
            out string? refusal);

        Assert.False(ok);
        Assert.Contains("Ca² is unknown", refusal);
    }

    // ── Config form builds a real arrival spec ───────────────────────────

    private static ConfigPanelViewModel ManualClinic()
    {
        var vm = new ConfigPanelViewModel
        {
            SourceMode = DataSourceMode.EnterManually,
            AdvancedIsOptionalEnabled = true,
        };

        vm.ManualLambda.Value = "0.5";
        vm.PExit.Value = "0.4";
        foreach (var row in vm.StageRows)
        {
            row.MuValue = "1.0";
            row.Servers.Value = "1";
        }

        return vm;
    }

    [AvaloniaFact]
    public void GammaArrival_BuildsTheSpecAtTheResolvedRate()
    {
        var vm = ManualClinic();
        vm.InterArrivalDistribution = "Gamma";
        vm.ArrivalSpread.Value = "4";

        SimulationParameters? parameters = vm.TryBuildRunParameters();

        Assert.NotNull(parameters);
        DistributionSpec spec = Assert.IsType<DistributionSpec>(parameters!.ArrivalDistribution);
        Assert.Equal(DistributionFamily.Gamma, spec.Family);
        Assert.Equal(2.0, spec.Mean, 9); // 1 / λ
        Assert.Equal(4.0, spec.Shape!.Value, 9);
        Assert.Equal(0.5, 1.0 / spec.Mean, 9);
    }

    [AvaloniaFact]
    public void GammaArrival_WithoutSpread_BlocksStartAndSaysWhy()
    {
        var vm = ManualClinic();
        vm.InterArrivalDistribution = "Gamma";
        vm.ArrivalSpread.Value = "";

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("arrival spread", vm.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);

        vm.ArrivalSpread.Value = "4";

        Assert.True(vm.StartIsEnabled, vm.StartBlockedMessage);
    }

    [AvaloniaFact]
    public void UniformArrival_HalfWidthAtTheMean_IsRefused()
    {
        var vm = ManualClinic();
        vm.InterArrivalDistribution = "Uniform";
        vm.ArrivalSpread.Value = "2.0"; // mean inter-arrival is 2.0 → lower bound 0

        Assert.False(vm.StartIsEnabled);

        vm.ValidateArrivalSpread();
        Assert.True(vm.ArrivalSpread.HasError);
        Assert.Contains("less than the mean inter-arrival time", vm.ArrivalSpread.ErrorMessage);

        vm.ArrivalSpread.Value = "1.0";
        vm.ValidateArrivalSpread();
        Assert.False(vm.ArrivalSpread.HasError);
        Assert.True(vm.StartIsEnabled, vm.StartBlockedMessage);
    }

    [AvaloniaFact]
    public void ExponentialArrival_HidesTheSpreadFieldAndCarriesNoSpec()
    {
        var vm = ManualClinic();
        vm.InterArrivalDistribution = "Exponential";

        Assert.False(vm.ArrivalSpreadVisible);
        Assert.Null(vm.TryBuildRunParameters()!.ArrivalDistribution);
    }

    // ── The widget's dedicated steady-state run (D-137) ──────────────────

    /// <summary>
    /// Clicks the widget's steady-state button on a clinic-day configuration and
    /// asserts the comparison appears. This is the D-137 hole closed end to end:
    /// the user never has to discover the 100,000-minute rule to see a table.
    /// </summary>
    [AvaloniaFact]
    public async Task SteadyStateButton_RunsAndPopulatesTheWidget()
    {
        var window = new MainWindow();
        window.Width = 1100;
        window.Show();
        var main = (MainViewModel)window.DataContext!;
        var originalVisible = main.Results.VisibleWidgets.ToArray();
        try
        {
            main.Config.SourceMode = DataSourceMode.EnterManually;
            main.Config.AdvancedIsOptionalEnabled = true;
            main.Config.Seed.Value = "42";
            main.Config.TraceLevel = "Minimal";
            main.Config.ManualLambda.Value = "0.5";
            main.Config.StageRows[0].MuValue = "0.8";
            main.Config.StageRows[1].MuValue = "0.6";
            main.Config.StageRows[2].MuValue = "0.4";
            main.Config.StageRows[0].Servers.Value = "1";
            main.Config.StageRows[1].Servers.Value = "2";
            main.Config.StageRows[2].Servers.Value = "3";
            main.Config.PExit.Value = "0.4";

            // Run the clinic day first: that is the real sequence — the button
            // lives in the widget, and the widget is only on screen once a run has
            // replaced the welcome card.
            Assert.True(main.Config.StartCalculationCommand.CanExecute(null), main.Config.StartBlockedMessage);
            main.Config.StartCalculationCommand.Execute(null);
            await WaitUntilAsync(
                () => main.Results.HasRun && !main.Results.IsBusy,
                TimeSpan.FromSeconds(120));

            // The clinic day itself is transient, so the widget is empty and offers
            // the button. Collapse the other widgets so the frame shows it.
            Assert.True(main.AnalyticalValidation.IsEmpty);
            main.Results.ShowMetrics = false;
            main.Results.ShowChiSquare = false;
            main.Results.ShowTrace = false;
            main.Results.ShowUtilisation = false;
            main.Results.ShowQueueLength = false;
            main.Results.ShowWaitHistogram = false;
            main.Results.ShowSimulationVerification = false;
            BringAnalyticalWidgetIntoView(window);

            var button = window.GetVisualDescendants().OfType<Button>().SingleOrDefault(
                b => (b.Content as string) == "Run steady-state validation (100,000 min)");
            Assert.NotNull(button);
            Assert.True(button!.IsEffectivelyVisible, "the steady-state button must be on screen when the widget is empty");
            var buttonPosition = button.TranslatePoint(new Avalonia.Point(0, 0), window);
            Assert.NotNull(buttonPosition);
            Assert.InRange(buttonPosition!.Value.Y, 0, window.Bounds.Height);
            Assert.True(button.Command!.CanExecute(button.CommandParameter));

            SaveFrame(window, "phase-8v-steady-state-button.png");

            // Click the real button, not the command behind it.
            button.Command.Execute(button.CommandParameter);
            await WaitUntilAsync(
                () => !main.AnalyticalValidation.IsEmpty && !main.AnalyticalValidation.IsSteadyStateRunning,
                TimeSpan.FromSeconds(180));

            Assert.False(
                main.AnalyticalValidation.IsEmpty,
                $"steady-state run did not produce rows: {main.AnalyticalValidation.EmptyMessage}");
            Assert.Equal(3, main.AnalyticalValidation.Rows.Count);

            BringAnalyticalWidgetIntoView(window);
            SaveFrame(window, "phase-8v-steady-state.png");
        }
        finally
        {
            RestoreVisibility(main.Results, originalVisible);
            window.Close();
        }
    }

    /// <summary>
    /// Scrolls the analytical widget into the viewport and asserts that it really
    /// moved there. <c>IsEffectivelyVisible</c> is not a bounds check (D-169): it
    /// returns true for a control scrolled far below the window, which is exactly
    /// how the first version of this test produced a frame of the welcome card.
    /// </summary>
    private static Border BringAnalyticalWidgetIntoView(Window window)
    {
        var widget = window.GetVisualDescendants().OfType<Border>()
            .Single(b => b.Name == "AnalyticalValidationWidget");

        widget.BringIntoView();
        window.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var position = widget.TranslatePoint(new Avalonia.Point(0, 0), window);
        Assert.NotNull(position);
        Assert.InRange(position!.Value.Y, 0, window.Bounds.Height - 1);
        return widget;
    }

    private static void SaveFrame(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        Assert.True(
            File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
            $"{fileName} missing or suspiciously small");
    }

    private static void RestoreVisibility(ResultsPanelViewModel results, IReadOnlyList<string> originalVisible)
    {
        results.ShowMetrics = originalVisible.Contains("metrics");
        results.ShowChiSquare = originalVisible.Contains("chiSquare");
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
