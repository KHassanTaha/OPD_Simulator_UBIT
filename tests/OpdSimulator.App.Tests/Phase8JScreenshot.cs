using System;
using System.Globalization;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8J evidence (AGENTS §18): renders the gate-3b configuration through the
/// headless pipeline and saves it to <c>logs/screenshots/phase-8j-same-behaviour.png</c>.
/// </summary>
/// <remarks>
/// <para>
/// The screenshot's job is to show that 8J changed no behaviour. Phase 8J rewrote how a
/// stage's service family travels from the config panel to the engine, and the UI still
/// offers a single service-family dropdown, so the reachable outcome must be exactly what
/// it was before. Rather than eyeball the metrics, this test asserts them against the
/// pre-8J baseline captured from the coordinator on the same seed: every total and
/// per-stage figure is compared bit-for-bit, so a regression shows up as a failing
/// number rather than a plausible-looking panel.
/// </para>
/// <para>
/// The headless render is this host's substitute for a real-display walk, which
/// DEV_LAUNCH records as owner-required (Wayland host, AGENTS §18 permits a headless
/// walkthrough with the same click sequence).
/// </para>
/// </remarks>
public class Phase8JScreenshot
{
    [AvaloniaFact]
    public void Render_GateConfiguration_SavesPhase8JSameBehaviourPng()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var vm = window.DataContext as MainViewModel
                ?? throw new InvalidOperationException("MainWindow must bind a MainViewModel");

            // The exact configuration from the 8J gate, step 3b: entered manually,
            // three stages at μ 0.8 / 0.6 / 0.4 with 1 / 2 / 3 servers, p_exit 0.4,
            // Exponential throughout, seed 42.
            var config = vm.Config;
            config.ParametersIsOptionalEnabled = true;
            config.ManualLambda.Value = "0.5";
            config.ManualMuPerStage.Value = "0.8, 0.6, 0.4";
            config.PExit.Value = "0.4";
            // Phase 8L: "Exponential throughout" goes through the one source the
            // UI writes to, then onto every row.
            config.DefaultStageServiceFamily = DistributionFamily.Exponential;
            config.ApplyDefaultsToAllStagesCommand.Execute(null);
            config.InterArrivalDistribution = "Exponential";
            config.Seed.Value = "42";
            config.StageRows[1].Servers.Value = "2";
            config.StageRows[2].Servers.Value = "3";

            var parameters = config.TryBuildRunParameters()
                ?? throw new InvalidOperationException("gate configuration must build");

            // 8J's own claim, checked on the real object: three stages, three families,
            // three rates, all Exponential, each spec carrying its own stage's mean.
            Assert.Equal(3, parameters.ServiceFamilies.Count);
            Assert.Equal(3, parameters.ServiceRates.Count);
            Assert.All(parameters.ServiceFamilies, spec =>
                Assert.Equal(Core.Distributions.DistributionFamily.Exponential, spec.Family));

            var outcome = SimulationCoordinator.Run(parameters, binding: null);
            Assert.Null(outcome.Error);
            Assert.NotNull(outcome.Result);

            AssertSameBehaviourAsBaseline(outcome.Result!);

            vm.Results.StartRun();
            vm.Results.CompleteRun(outcome);
            var frame = HeadlessScreenshot.Capture(window);

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-8j-same-behaviour.png");
            frame.Save(path);

            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= 256,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Compares the run against the pre-8J baseline for this seed and configuration.
    /// </summary>
    /// <remarks>
    /// These literals were produced by running the same configuration through
    /// <c>SimulationCoordinator.Run</c> on the commit before 8J
    /// (<c>3f7ed3f</c>). They are reproduced here as exact doubles rather than
    /// round numbers so that a change in the sampling path cannot slip through a
    /// tolerance: 8J moved the service mean from Core's implicit fallback into an
    /// explicit per-stage spec, and the requirement is that this is the same number,
    /// not a close one.
    /// </remarks>
    private static void AssertSameBehaviourAsBaseline(Core.Engine.SimulationResult result)
    {
        var c = CultureInfo.InvariantCulture;

        Assert.Equal(96, result.TotalPatientsServed);
        Assert.Equal(6.361255218169437, result.AverageWaitMinutes);
        Assert.Equal(11.361565824351594, result.AverageSystemTimeMinutes);
        Assert.Equal(1.2026765790581488, result.AverageQueueLength);
        Assert.Equal(0.5420211776308005, result.StageUtilisation);
        Assert.Equal(0.5671883320871884, result.ThroughputPerMinute);
        Assert.Equal(169.25595004172766, result.OperatingTimeMinutes);

        Assert.Equal(3, result.StageMetrics.Count);
        AssertStage(result, 0, "Reception", served: 96, mu: 0.8, rho: 0.625,
            wait: 4.731886941683448, utilisation: 0.7348409398234604);
        AssertStage(result, 1, "Screening", served: 96, mu: 0.6, rho: 0.4166666666666667,
            wait: 1.6041582323241907, utilisation: 0.57239088639195);
        AssertStage(result, 2, "Doctor", served: 60, mu: 0.4, rho: 0.24999999999999994,
            wait: 0.04033607065887921, utilisation: 0.31883170667699096);
    }

    private static void AssertStage(
        Core.Engine.SimulationResult result,
        int index,
        string expectedName,
        int served,
        double mu,
        double rho,
        double wait,
        double utilisation)
    {
        var stage = result.StageMetrics[index];
        Assert.Equal(expectedName, stage.StageName);
        Assert.Equal(served, stage.PatientsServed);
        Assert.Equal(mu, stage.ServiceRate);
        Assert.Equal(rho, stage.Rho);
        Assert.Equal(wait, stage.AverageWaitMinutes);
        Assert.Equal(utilisation, stage.StageUtilisation);
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
}
