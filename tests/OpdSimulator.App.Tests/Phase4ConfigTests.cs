using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 4 gate — ConfigPanel behaviour (feat/gui-rebuild). Verification intent:
/// the panel exposes the six documented sections + pinned footer; the stage
/// list resizes live; p_exit is shown only for 2+ stages and enforces the Core
/// [0,1) exposure-rule (value 1 blocks Start with an inline cause+remedy); and
/// Clear All returns every field to its factory default (FR-UI-21 — no
/// last-used state is ever restored).
/// </summary>
public class Phase4ConfigTests
{
    private static ConfigPanelViewModel NewVm() => new();

    private static Window Host(ConfigPanel panel, int width = 420, int height = 760)
    {
        var window = new Window { Width = width, Height = height, Content = panel };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    [AvaloniaFact]
    public void ConfigPanel_HasSixSections_And_Footer()
    {
        var panel = new ConfigPanel { DataContext = NewVm() };
        var window = Host(panel);

        try
        {
            Assert.Equal(6, window.GetVisualDescendants().OfType<CollapsibleSection>().Count());
            var footer = window.GetVisualDescendants().OfType<PinnedFooterBar>().Single();
            Assert.Equal("Start Calculation", footer.PrimaryText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StageCount_Change_ResizesRows()
    {
        var vm = NewVm();

        Assert.Equal(3, vm.StageRows.Count);
        Assert.Equal(
            new[] { "Reception", "Screening", "Doctor" },
            vm.StageRows.Select(r => r.StageName));

        vm.StageCount.Value = "5";
        Assert.Equal(5, vm.StageRows.Count);
        Assert.Equal("Stage 4", vm.StageRows[3].StageName);
        Assert.Equal("Stage 5", vm.StageRows[4].StageName);

        vm.StageCount.Value = "1";
        Assert.Single(vm.StageRows);
        Assert.Equal("Reception", vm.StageRows[0].StageName);
    }

    [AvaloniaFact]
    public void PExit_VisibleOnlyForTwoOrMoreStages()
    {
        var vm = NewVm();

        Assert.True(vm.PExitVisible, "default 3-stage config must show the p_exit override");

        vm.StageCount.Value = "1";
        Assert.False(vm.PExitVisible, "a single stage has no exit route — p_exit must be hidden");

        vm.StageCount.Value = "2";
        Assert.True(vm.PExitVisible, "2 stages reintroduce the early-exit route");
    }

    [AvaloniaFact]
    public void PExit_ValueOne_SetsInlineError_AndBlocksStart()
    {
        var vm = NewVm();
        // D-128 supersedes 5d.1's "runnable in principle" contract: an empty
        // fit-mode config is incomplete (no usable file) and Start is disabled.
        Assert.False(vm.StartIsEnabled, "an empty fit-mode config is not complete (D-128)");

        // Manual overrides must be in use for the inline error to gate Start;
        // the Parameters section is OFF at factory ground, and an OFF section's
        // fields do not participate in Start gating (D-103).
        vm.ParametersIsOptionalEnabled = true;
        vm.PExit.Value = "1";
        vm.ValidatePExit();

        Assert.True(vm.PExit.HasError);
        Assert.Equal("Exit probability must be less than 1. You entered 1.", vm.PExit.ErrorMessage);
        Assert.False(vm.StartIsEnabled, "p_exit = 1 is outside the Core contract [0,1) and must block Start");
        Assert.False(vm.StartCalculationCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void PExit_ValueZeroPointFour_IsAccepted()
    {
        var vm = NewVm();

        vm.PExit.Value = "0.4";
        vm.ValidatePExit();

        Assert.False(vm.PExit.HasError);
        Assert.Null(vm.PExit.ErrorMessage);
        // D-128: a valid p_exit alone does not complete the config (no file / λ / μ).
        Assert.False(vm.StartIsEnabled);
        Assert.False(vm.StartCalculationCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void StageName_And_Servers_BindCorrectly()
    {
        var vm = NewVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var nameBox = window.GetVisualDescendants().OfType<TextBox>()
                .Single(t => t.Text == "Reception");
            nameBox.Text = "Reception Desk";
            Assert.True(vm.StageRows[0].StageName == "Reception Desk",
                "editing the stage-name TextBox must write back to the row VM");

            var serversField = window.GetVisualDescendants().OfType<ValidatedField>()
                .Single(f => f.Label == "Servers" && ReferenceEquals(f.DataContext, vm.StageRows[0]));
            Assert.Equal("1", serversField.Value);
            serversField.Value = "2";
            Assert.True(vm.StageRows[0].Servers.Value == "2",
                "changing a stage's Servers field must write back to the row VM");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClearAll_ResetsEveryFieldToDefault()
    {
        var vm = NewVm();
        vm.ManualLambda.Value = "0.7";
        vm.ManualMuPerStage.Value = "0.8, 0.6";
        vm.PExit.Value = "0.4";
        vm.Seed.Value = "7";
        vm.StageCount.Value = "5";
        vm.IsMultiDay = true;
        vm.Days.Value = "10";
        vm.DailyCap.Value = "40";
        vm.TraceLevel = "Rng";
        vm.InterArrivalDistribution = "Poisson";
        vm.ServiceDistribution = "Normal";
        vm.IsMeanWise = true;
        vm.StartDay = "Saturday";

        vm.ResetToDefaults();

        Assert.Equal("No file loaded", vm.DataStatus);
        Assert.Equal("Exponential", vm.InterArrivalDistribution);
        Assert.Equal("Exponential", vm.ServiceDistribution);
        Assert.True(vm.IsRateWise);
        Assert.False(vm.IsMeanWise);
        Assert.Equal("", vm.ManualLambda.Value);
        Assert.Equal("", vm.ManualMuPerStage.Value);
        Assert.Equal("", vm.PExit.Value);
        Assert.Equal("3", vm.StageCount.Value);
        Assert.Equal(3, vm.StageRows.Count);
        Assert.Equal("Reception", vm.StageRows[0].StageName);
        Assert.True(vm.IsSingleDay);
        Assert.False(vm.IsMultiDay);
        Assert.Equal("Monday", vm.StartDay);
        Assert.Equal("1", vm.Days.Value);
        Assert.Equal("", vm.DailyCap.Value);
        Assert.Equal("42", vm.Seed.Value);
        Assert.Equal("State", vm.TraceLevel);
        // D-128: a reset returns to an empty fit-mode config, which is not startable.
        Assert.False(vm.StartIsEnabled);
    }

    [AvaloniaFact]
    public void ClearAllCommand_RequestsConfirmation_WithoutResetting()
    {
        var vm = NewVm();
        vm.Seed.Value = "7";

        var requested = 0;
        vm.ClearAllRequested += (_, _) => requested++;

        vm.ClearAllCommand.Execute(null);

        Assert.True(requested == 1, "the command must ask the view for confirmation first");
        Assert.True(vm.Seed.Value == "7", "nothing may reset until the confirmation is answered");
    }

    [AvaloniaFact]
    public void UploadCommand_RaisesUploadRequested()
    {
        var vm = NewVm();

        var requested = 0;
        vm.UploadRequested += (_, _) => requested++;

        vm.UploadDataCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.Equal("No file loaded", vm.DataStatus);
    }

    [AvaloniaFact]
    public void OptionalSection_ToggledOff_ClearsFieldErrors()
    {
        var vm = NewVm();
        vm.ParametersIsOptionalEnabled = true;

        vm.PExit.Value = "1";
        vm.ValidatePExit();
        Assert.True(vm.PExit.HasError, "with Parameters on, p_exit = 1 must flag an inline error");
        Assert.False(vm.StartIsEnabled);

        vm.ParametersIsOptionalEnabled = false;

        Assert.False(vm.PExit.HasError, "turning an optional section OFF must clear the errors of its fields");
        Assert.Null(vm.PExit.ErrorMessage);
        // D-128: the cleared p_exit is not what blocks Start — the config simply
        // has no data file yet. The gate names the real blocker, not p_exit.
        Assert.False(vm.StartIsEnabled);
        Assert.DoesNotContain("p_exit", vm.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void OptionalSection_ToggledOff_InvalidLambda_IsNotTheBlocker()
    {
        var vm = NewVm();
        vm.ParametersIsOptionalEnabled = true;

        vm.ManualLambda.Value = "-5";
        vm.ValidateManualLambda();
        Assert.False(vm.StartIsEnabled, "with the section ON an invalid manual λ must block Start");

        vm.ParametersIsOptionalEnabled = false;

        Assert.False(vm.ManualLambda.HasError, "the error must be cleared, not merely ignored");
        // D-128: an OFF section's fields are skipped, so λ is not the blocker —
        // the empty fit-mode config is.
        Assert.False(vm.StartIsEnabled);
        Assert.DoesNotContain("λ", vm.StartBlockedMessage);
    }

    [AvaloniaFact]
    public void OptionalSection_ToggledOn_RevalidatesOnNextBlur()
    {
        var vm = NewVm();
        vm.ParametersIsOptionalEnabled = true;

        vm.PExit.Value = "1";
        vm.ValidatePExit();
        Assert.True(vm.PExit.HasError, "p_exit = 1 while the section is ON must flag an error");

        vm.ParametersIsOptionalEnabled = false;
        Assert.False(vm.PExit.HasError, "OFF clears the stale error (real true→false transition)");

        vm.ParametersIsOptionalEnabled = true;

        Assert.False(vm.PExit.HasError, "turning the section back ON must wait for the next blur, not re-flag old text");
        // D-128: no pending p_exit error, but the config still lacks data.
        Assert.False(vm.StartIsEnabled);
        Assert.DoesNotContain("p_exit", vm.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);

        vm.ValidatePExit();
        Assert.True(vm.PExit.HasError, "blurring out of p_exit = 1 must re-flag and block again");
        Assert.False(vm.StartIsEnabled);
    }

    [AvaloniaFact]
    public void Render_ConfigPanel_SavesPhase4Screenshot()
    {
        var panel = new ConfigPanel { DataContext = NewVm() };
        var window = Host(panel, width: 440, height: 780);

        try
        {
            var frame = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("headless pipeline produced no frame");

            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);
            var path = Path.Combine(shotDir, "phase-4-config.png");
            frame.Save(path);

            const int minBytes = 256;
            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(new FileInfo(path).Length >= minBytes,
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