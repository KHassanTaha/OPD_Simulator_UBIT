using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 7C gate (feat/milestone-7-model-driven): the two-path data-source
/// configuration. Verification intent: the mode selector defaults to
/// FitFromData; EnterManually hides the comma-list μ field, seeds p_exit = 0.4
/// and requires λ + every per-stage μ + p_exit before Start; FitFromData
/// requires a usable file with a resolvable μ for every stage; and both paths
/// build valid <see cref="SimulationParameters"/>. Start is a completeness gate
/// per D-128, which supersedes 5d.1's "runnable in principle" contract.
/// (Phase 7D replaced the collapsible Data section with an always-on status
/// strip; the data UI itself moved to the Input tab.)
/// </summary>
public class Phase7CTests
{
    private static ConfigPanelViewModel NewVm() => new();

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

    /// <summary>Switches to manual mode and fills λ, every per-stage μ and p_exit — a fully valid manual config.</summary>
    private static ConfigPanelViewModel ManualVm()
    {
        var vm = NewVm();
        vm.SourceMode = DataSourceMode.EnterManually;
        vm.ManualLambda.Value = "0.5";
        vm.StageRows[0].MuValue = "0.8";
        vm.StageRows[1].MuValue = "0.6";
        vm.StageRows[2].MuValue = "0.4";
        vm.ValidateManualLambda();
        return vm;
    }

    private static Window Host(ConfigPanel panel, int width = 420, int height = 900)
    {
        var window = new Window { Width = width, Height = height, Content = panel };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    // ── Mode selector ───────────────────────────────────────────────────

    [Fact]
    public void ModeSelector_DefaultsToFitFromData()
    {
        var vm = NewVm();

        Assert.Equal(DataSourceMode.FitFromData, vm.SourceMode);
        Assert.Equal(ConfigPanelViewModel.FitFromDataLabel, vm.SourceModeSelection);
        Assert.True(vm.IsDataSectionVisible);
        Assert.False(vm.IsManualMuEditable);
        Assert.True(vm.IsCommaListMuVisible);
    }

    [AvaloniaFact]
    public void ModeSelector_SwitchToManual_KeepsStatusStripVisible()
    {
        var vm = NewVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            // Phase 7D replaced the collapsible Data section with an always-on
            // status strip; the mode selector no longer hides data UI here.
            var manage = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Manage input →");
            Assert.True(manage.IsVisible, "the data status strip must show in fit mode");

            vm.SourceMode = DataSourceMode.EnterManually;
            window.UpdateLayout();

            Assert.True(manage.IsVisible, "the data status strip stays visible in manual mode too (Phase 7D)");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ModeSelector_SwitchBackToFit_RestoresCommaListMu()
    {
        var vm = NewVm();
        vm.SourceMode = DataSourceMode.EnterManually;
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var commaMu = window.GetVisualDescendants().OfType<ValidatedField>()
                .Single(f => f.Label == "Manual μ per stage (per minute)");
            Assert.False(commaMu.IsVisible, "manual mode owns per-stage μ — the comma list hides");

            vm.SourceMode = DataSourceMode.FitFromData;
            window.UpdateLayout();

            Assert.True(commaMu.IsVisible, "returning to fit mode must restore the comma-list μ override");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ManualMode_HidesCommaListMuField()
    {
        var vm = ManualVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var commaMu = window.GetVisualDescendants().OfType<ValidatedField>()
                .Single(f => f.Label == "Manual μ per stage (per minute)");
            Assert.False(commaMu.IsVisible, "manual mode owns per-stage μ — the comma list must hide");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FitMode_ShowsCommaListMuField()
    {
        var vm = NewVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var commaMu = window.GetVisualDescendants().OfType<ValidatedField>()
                .Single(f => f.Label == "Manual μ per stage (per minute)");
            Assert.True(commaMu.IsVisible, "fit mode keeps the comma-list μ override");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ModeSelector_LabelDrivesTheEnumThroughSelection()
    {
        var vm = NewVm();

        vm.SourceModeSelection = ConfigPanelViewModel.EnterManuallyLabel;

        Assert.Equal(DataSourceMode.EnterManually, vm.SourceMode);
        Assert.False(vm.IsDataSectionVisible);

        vm.SourceModeSelection = ConfigPanelViewModel.FitFromDataLabel;

        Assert.Equal(DataSourceMode.FitFromData, vm.SourceMode);
    }

    // ── EnterManually gating (D-128) ────────────────────────────────────

    [Fact]
    public void ManualMode_DefaultPExit_IsPointFour()
    {
        var vm = NewVm();

        vm.SourceMode = DataSourceMode.EnterManually;

        Assert.Equal("0.4", vm.PExit.Value);
        Assert.False(vm.PExit.HasError);
    }

    [Fact]
    public void ManualMode_EmptyLambda_StartBlocked()
    {
        var vm = ManualVm();
        vm.ManualLambda.Value = "";
        vm.ValidateManualLambda();

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("λ", vm.StartBlockedMessage);
    }

    [Fact]
    public void ManualMode_MissingMuOnStage_StartBlocked()
    {
        var vm = ManualVm();
        vm.StageRows[1].MuValue = "";

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("Screening has no μ", vm.StartBlockedMessage);
    }

    [Fact]
    public void ManualMode_InvalidMu_Blocked()
    {
        var vm = ManualVm();
        vm.StageRows[2].MuValue = "0";

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("Doctor has an invalid μ", vm.StartBlockedMessage);
    }

    [Fact]
    public void ManualMode_AllValid_StartEnabled()
    {
        var vm = ManualVm();

        Assert.True(vm.StartIsEnabled);
        Assert.True(vm.StartCalculationCommand.CanExecute(null));
        Assert.Equal("", vm.StartBlockedMessage);
    }

    [Fact]
    public void ManualMode_PExitOne_StartBlocked()
    {
        var vm = ManualVm();
        vm.PExit.Value = "1";
        vm.ValidatePExit();

        Assert.True(vm.PExit.HasError, "p_exit = 1 is outside the Core contract [0,1)");
        Assert.False(vm.StartIsEnabled);
    }

    // ── FitFromData gating (D-128) ──────────────────────────────────────

    [Fact]
    public void FitMode_NoFileLoaded_StartBlocked()
    {
        var vm = NewVm();

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("data file", vm.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FitMode_ValidFile_StartEnabled()
    {
        var vm = NewVm();
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        vm.SyncStagesToData();

        Assert.Single(vm.StageRows);
        Assert.True(vm.StartIsEnabled, $"a usable file covering every stage must complete fit mode, but: {vm.StartBlockedMessage}");
        Assert.Equal("", vm.StartBlockedMessage);
    }

    // ── Parameters produced by both paths ───────────────────────────────

    [Fact]
    public void ManualMode_PerStageMu_FlowsIntoParameters()
    {
        var vm = ManualVm();

        var parameters = vm.TryBuildRunParameters();

        Assert.NotNull(parameters);
        Assert.Equal(0.5, parameters!.ManualArrivalRate!.Value, precision: 5);
        Assert.Equal(new double?[] { 0.8, 0.6, 0.4 }, parameters.ManualServiceRates);
        Assert.Equal(0.4, parameters.PExitOverride!.Value, precision: 5);
    }

    [Fact]
    public void BothModes_ProduceValidSimulationParameters()
    {
        var manual = ManualVm();
        var manualParams = manual.TryBuildRunParameters();
        Assert.NotNull(manualParams);
        Assert.Equal(3, manualParams!.StageNames.Count);

        var fit = NewVm();
        fit.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        fit.SyncStagesToData();
        var fitParams = fit.TryBuildRunParameters();
        Assert.NotNull(fitParams);
        Assert.Single(fitParams!.StageNames);
    }
}