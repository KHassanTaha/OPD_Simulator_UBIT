using System;
using System.IO;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 5 evidence: renders the full run flow end-to-end through Avalonia's
/// headless pipeline — a diagnostic-trace run with manual λ/μ, completed and
/// shown on the results panel — and saves the frame to
/// logs/screenshots/phase-5-results.png (AGENTS §18 screenshot evidence).
/// </summary>
public class Phase5Screenshot
{
    [AvaloniaFact]
    public void Render_RunResults_SavesPhase5Screenshot()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var vm = window.DataContext as MainViewModel
                ?? throw new InvalidOperationException("MainWindow must bind a MainViewModel");

            var config = vm.Config;
            config.ParametersIsOptionalEnabled = true;
            config.ManualLambda.Value = "0.1";
            config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
            config.AdvancedIsOptionalEnabled = true;
            config.TraceLevel = "State";
            config.IsDiagnosticTrace = true;
            config.HorizonMinutes.Value = "1500";

            var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding: null);
            Assert.Null(outcome.Error);
            Assert.NotNull(outcome.Result);
            Assert.True(outcome.Result!.TotalPatientsServed > 0);

            vm.Results.StartRun();
            vm.Results.CompleteRun(outcome);
            window.UpdateLayout();

            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-5-results.png");
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