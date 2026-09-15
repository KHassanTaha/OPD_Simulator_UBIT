using System;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 5 gate — run flow (feat/gui-rebuild). Verification intent: the three
/// run modes dispatch correctly (D-105) — only DiagnosticTrace records a trace,
/// ClinicDay is a one-session calendar run, MultiDay generates exactly the
/// requested day blocks; trace detail follows the selected level; and the three
/// refusal paths surface clean banners — missing λ, the fitted p_exit = 1.0
/// "every row exits after Screening" case, and the exact Core
/// <c>UnstableSystemException</c> wording (G3/G4/5-F). The welcome card starts
/// visible and is replaced by the first run attempt.
/// </summary>
public class Phase5RunFlowTests
{
    private static ConfigPanelViewModel ManualClinic()
    {
        var vm = new ConfigPanelViewModel();
        vm.ParametersIsOptionalEnabled = true;
        vm.ManualLambda.Value = "0.1";
        vm.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        foreach (var row in vm.StageRows)
        {
            row.Servers.Value = "1";
        }

        return vm;
    }

    private static string SamplePath(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "samples", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException($"Sample file {fileName} not found above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void DiagnosticTrace_RecordsTraceWithArrivals()
    {
        var vm = ManualClinic();
        vm.IsDiagnosticTrace = true;
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(RunMode.DiagnosticTrace, parameters!.RunMode);

        var outcome = SimulationCoordinator.Run(parameters, binding: null);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.True(outcome.Result!.TotalPatientsServed > 0, "a stable diagnostic run serves patients");
        Assert.NotEmpty(outcome.TraceLines);
        Assert.Contains(outcome.TraceLines, line => line.Contains("ARRIVAL"));
    }

    [Fact]
    public void TraceLevel_NoneEventsStateRng_ChangeRenderedDetail()
    {
        var baseline = ManualClinic();
        baseline.IsDiagnosticTrace = true;
        baseline.AdvancedIsOptionalEnabled = true;

        // None: nothing rendered.
        baseline.TraceLevel = "None";
        var none = SimulationCoordinator.Run(baseline.TryBuildRunParameters()!, binding: null);
        Assert.Null(none.Error);
        Assert.Empty(none.TraceLines);

        // Events: arrivals present, RNG rows dropped below the Rng level.
        baseline.TraceLevel = "Events";
        var events = SimulationCoordinator.Run(baseline.TryBuildRunParameters()!, binding: null);
        Assert.NotEmpty(events.TraceLines);
        Assert.Contains(events.TraceLines, line => line.Contains("ARRIVAL"));
        Assert.DoesNotContain(events.TraceLines, line => line.Contains("RNG"));

        // Rng: the draw rows are rendered (T=… RNG …).
        baseline.TraceLevel = "Rng";
        var rng = SimulationCoordinator.Run(baseline.TryBuildRunParameters()!, binding: null);
        Assert.NotEmpty(rng.TraceLines);
        Assert.Contains(rng.TraceLines, line => line.Contains("RNG"));
    }

    [Fact]
    public void ClinicDay_IsOneSessionCalendarRun_WithNoTrace()
    {
        var vm = ManualClinic();
        Assert.True(vm.IsSingleDay);
        var parameters = vm.TryBuildRunParameters();
        Assert.Equal(RunMode.ClinicDay, parameters!.RunMode);

        var outcome = SimulationCoordinator.Run(parameters, binding: null);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.Equal(1, outcome.Result!.GeneratorDays);
        Assert.Single(outcome.Result.AdmittedPerDay);
        Assert.Empty(outcome.TraceLines); // calendar runs record no trace (D-105)
    }

    [Fact]
    public void MultiDay_GeneratesExactlyTheRequestedDays()
    {
        var vm = ManualClinic();
        vm.IsMultiDay = true;
        vm.Days.Value = "3";
        vm.Days.ClearError();
        var parameters = vm.TryBuildRunParameters();

        var outcome = SimulationCoordinator.Run(parameters!, binding: null);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.Equal(3, outcome.Result!.GeneratorDays);
        Assert.Equal(3, outcome.Result.AdmittedPerDay.Count);
        Assert.Empty(outcome.TraceLines);
    }

    [Fact]
    public void MissingArrivalRate_RefusesWithCleanBanner()
    {
        // Factory default config: no manual λ and no data file.
        var vm = new ConfigPanelViewModel();
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);

        var outcome = SimulationCoordinator.Run(parameters!, binding: null);

        Assert.Null(outcome.Result);
        Assert.Equal(SimulationCoordinator.MissingArrivalRateMessage, outcome.Error);
    }

    [Fact]
    public void FittedPExitOne_RefusesWithEveryRowExitsAfterScreeningBanner()
    {
        // sample_patients.csv: every departure_stage is Screening → fitted
        // p_exit = 1.0, so no patient reaches a downstream stage.
        var vm = new ConfigPanelViewModel();
        var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
        Assert.True(binding.IsUsable);
        Assert.Equal(1.0, binding.FittedExitProbability!.Value, precision: 4);

        var outcome = SimulationCoordinator.Run(vm.TryBuildRunParameters()!, binding);

        Assert.Null(outcome.Result);
        Assert.Equal(SimulationCoordinator.FittedPExitEqualsOneMessage, outcome.Error);
    }

    [Fact]
    public void UnstableConfig_RefusesWithExactCoreMessage()
    {
        // Reception ρ = 1.0/(0.4·1) = 2.5 ≥ 1 → UnstableSystemException. Its
        // message must reach the banner verbatim (G3/G4 — no rewriting).
        var vm = ManualClinic();
        vm.ManualLambda.Value = "1.0";
        vm.ManualMuPerStage.Value = "0.4, 0.5, 0.4";
        var outcome = SimulationCoordinator.Run(vm.TryBuildRunParameters()!, binding: null);

        Assert.Null(outcome.Result);
        Assert.NotNull(outcome.Error);
        Assert.StartsWith("stage 'Reception' is unstable", outcome.Error);
        Assert.Contains("lower the arrival rate or add servers", outcome.Error);
        Assert.NotEqual(SimulationCoordinator.MissingArrivalRateMessage, outcome.Error);
    }

    [Fact]
    public void WelcomeCard_VisibleInitially_ReplacedByFirstRunAttempt()
    {
        var results = new ResultsPanelViewModel();

        Assert.True(results.IsWelcomeVisible);
        Assert.False(results.HasRun);

        results.StartRun();

        Assert.False(results.IsWelcomeVisible);
        Assert.True(results.HasRun);
        Assert.True(results.IsBusy);

        results.CompleteRun(new RunOutcome(null, Array.Empty<Models.FitReport>(),
            Array.Empty<string>(), SimulationCoordinator.DefaultExitProbability,
            SimulationCoordinator.MissingArrivalRateMessage));

        Assert.False(results.IsBusy);
        Assert.True(results.HasError);
        Assert.Equal(SimulationCoordinator.MissingArrivalRateMessage, results.RunError);
        Assert.Empty(results.StageRows);
    }
}