using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8E gate — four post-merge fixes.
///
/// <para><b>Bug 1 (logic).</b> The fitted p_exit = 1.0 refusal fired for every
/// network, including a single-stage one. With no downstream stage there is
/// nothing to refuse: everyone leaving after the only stage is the correct and
/// only possible behaviour. The refusal must now depend on the topology, and
/// the 2+/3-stage behaviour must be untouched.</para>
///
/// <para><b>Bug 2 (cosmetic).</b> Avalonia's Fluent theme paints a blue focus
/// ring on <c>TextBox</c>. The dropdown must show the brand green instead —
/// recoloured, never suppressed, because the keyboard pass requires a visible
/// focus indicator (AGENTS §16.7).</para>
///
/// <para><b>Bug 3 (functional).</b> Two independent root causes, see the
/// comments on the individual tests.</para>
///
/// <para><b>Bug 4 (cosmetic).</b> The Clear-All confirmation dialog had zero
/// outer padding and a single uniform 16 px gap, so it read as cramped. Every
/// gap must now resolve to a theme token.</para>
///
/// <para>These are plain Facts where the subject is pure logic (the coordinator)
/// and AvaloniaFacts where a real visual tree, focus or style resolution is the
/// thing under test.</para>
/// </summary>
public class Phase8EFixTests
{
    // ── Bug 1 — single-stage p_exit = 1.0 ──────────────────────────────────

    /// <summary>
    /// Why it matters: a one-stage network has no downstream stage, so a fitted
    /// p_exit of 1.0 is the truth about the data, not a broken configuration.
    /// Refusing it made the canonical single-stage demo file unrunnable.
    /// </summary>
    [Fact]
    public void SingleStageNetwork_FittedPExitOne_RunsWithoutRefusal()
    {
        var binding = AnalyzeSampleWithPExitOne(out _);
        var vm = SingleStageScreening();
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Single(parameters!.StageNames);
        Assert.Equal("Screening", parameters.StageNames[0]);

        var outcome = SimulationCoordinator.Run(parameters, binding);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        Assert.True(outcome.Result!.TotalPatientsServed > 0, "a stable single-stage run serves patients");
    }

    /// <summary>
    /// Why it matters: the fix must be gated on the topology, not a blanket
    /// removal. With two stages there IS a downstream stage, so p_exit = 1.0
    /// still means "nobody ever gets there" and must still be refused.
    /// </summary>
    [Fact]
    public void TwoStageNetwork_FittedPExitOne_StillRefused()
    {
        var binding = AnalyzeSampleWithPExitOne(out _);
        var vm = ManualRun(stageCount: 2, mu: "0.8, 0.6");
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(2, parameters!.StageNames.Count);

        var outcome = SimulationCoordinator.Run(parameters, binding);

        Assert.Null(outcome.Result);
        Assert.Equal(SimulationCoordinator.FittedPExitEqualsOneMessage, outcome.Error);
    }

    /// <summary>
    /// Regression guard for the real three-stage clinic: the 5-F banner must be
    /// reproduced verbatim, with no reworded or softened message.
    /// </summary>
    [Fact]
    public void ThreeStageNetwork_FittedPExitOne_StillRefused()
    {
        var binding = AnalyzeSampleWithPExitOne(out _);
        var vm = ManualRun(stageCount: 3, mu: "0.8, 0.6, 0.4");
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(3, parameters!.StageNames.Count);

        var outcome = SimulationCoordinator.Run(parameters, binding);

        Assert.Null(outcome.Result);
        Assert.Equal(SimulationCoordinator.FittedPExitEqualsOneMessage, outcome.Error);
        Assert.Contains("no patient reaches a downstream stage", outcome.Error);
    }

    // ── Phase 8X — prebuilt ComboBox ──────────────────────────────────────

    /// <summary>
    /// Why it matters: the replacement must be the platform ComboBox, not
    /// another hand-rolled field. The whole point of the change is that open,
    /// move and commit are the prebuilt control's behaviour.
    /// </summary>
    [AvaloniaFact]
    public void DropdownField_UsesAStandardComboBox()
    {
        var dropdown = new DropdownField
        {
            Label = "Arrival distribution",
            Items = new[] { "Exponential", "Normal", "Lognormal" },
        };
        var window = Host(dropdown);

        var combo = dropdown.FindDescendantOfType<ComboBox>();
        Assert.NotNull(combo);
        Assert.Equal(3, combo.ItemCount);
        Assert.Equal("Arrival distribution", AutomationProperties.GetName(combo));

        window.Close();
    }

    /// <summary>
    /// Why it matters: a standard ComboBox must show every option when opened,
    /// including when one is already selected — the failure the old custom
    /// filter created by pre-filtering the list to the current value.
    /// </summary>
    [AvaloniaFact]
    public void DropdownField_OpenedWithValue_ShowsAllOptions()
    {
        var all = new[] { "Exponential", "Normal", "Lognormal", "Gamma", "Uniform" };
        var dropdown = new DropdownField
        {
            Label = "Arrival distribution",
            Items = all,
            SelectedItem = "Exponential",
        };
        var window = Host(dropdown);

        var combo = dropdown.FindDescendantOfType<ComboBox>()!;
        Assert.Equal("Exponential", combo.SelectedItem);

        combo.IsDropDownOpen = true;
        Assert.True(combo.IsDropDownOpen);
        Assert.Equal(all.Length, combo.ItemCount);
        Assert.Equal("Exponential", combo.SelectedItem);

        combo.IsDropDownOpen = false;
        window.Close();
    }

    /// <summary>
    /// Why it matters: choosing an item in the ComboBox must round-trip through
    /// the wrapper's two-way <c>SelectedItem</c>, which is what every screen
    /// binds to.
    /// </summary>
    [AvaloniaFact]
    public void DropdownField_Selection_CommitsValue()
    {
        var dropdown = new DropdownField
        {
            Label = "Arrival distribution",
            Items = new[] { "Exponential", "Normal", "Lognormal", "Gamma", "Uniform" },
            SelectedItem = "Exponential",
        };
        var window = Host(dropdown);

        var combo = dropdown.FindDescendantOfType<ComboBox>()!;
        combo.SelectedItem = "Lognormal";

        Assert.Equal("Lognormal", dropdown.SelectedItem);
        Assert.Equal("Lognormal", combo.SelectedItem);

        window.Close();
    }

    // ── Bug 4 — dialog padding ─────────────────────────────────────────────

    /// <summary>
    /// Why it matters: the confirm dialog is the one surface every destructive
    /// action passes through, and it read as cramped next to the rest of the
    /// app. The assertion is on the resolved values, so a literal regression
    /// back to a hardcoded number fails here.
    /// </summary>
    [AvaloniaFact]
    public void ThemedDialog_Padding_UsesThemeTokens()
    {
        var dialog = new ThemedDialog { Title = "Clear all fields?", Message = "This resets every field." };
        dialog.Show();

        // The root Border is the one that wraps the two StackPanels.
        var root = dialog.GetVisualDescendants()
            .OfType<Border>()
            .First(b => b.Child is StackPanel);

        var spaceL = (double)Theme("SpaceL");
        var spaceM = (double)Theme("SpaceM");
        var spaceS = (double)Theme("SpaceS");

        Assert.Equal(24d, spaceL);
        Assert.Equal(16d, spaceM);
        Assert.Equal(12d, spaceS);

        // Outer padding: 24 on all sides, from ThicknessSpaceL.
        Assert.Equal(new Thickness(spaceL), root.Padding);

        // Title → message gap 16, message → button row gap 24: expressed as the
        // Spacing of two nested StackPanels (Avalonia cannot mix a literal with
        // a DynamicResource inside one Thickness — AVLN2005).
        var stack = (StackPanel)root.Child!;
        Assert.Equal(spaceM, stack.Spacing);
        var inner = (StackPanel)stack.Children[1]!;
        Assert.Equal(spaceL, inner.Spacing);

        // Button row: 12 px between the buttons, right-aligned.
        var buttons = inner.Children.OfType<Grid>().Single();
        Assert.Equal(spaceS, buttons.ColumnSpacing);
        Assert.All(
            buttons.Children.OfType<Button>(),
            btn => Assert.Equal(HorizontalAlignment.Right, btn.HorizontalAlignment));

        // No hardcoded margin may creep back onto the message or the button row.
        var message = (TextBlock)((ScrollViewer)inner.Children[0]!).Content!;
        Assert.Equal(default, message.Margin);
        Assert.Equal(default, buttons.Margin);

        dialog.Close();
    }

    // ── helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a theme token. The indexer on <c>ResourceDictionary</c> does not
    /// walk into MergedResources, so Theme.axaml keys must go through
    /// TryGetResource — the same call the production code uses.
    /// </summary>
    private static object Theme(string key)
    {
        var app = Application.Current!;
        Assert.True(app.Resources.TryGetResource(key, null, out var value), $"theme resource '{key}' must resolve");
        return value!;
    }

    private static Window Host(Control control, int width = 800, int height = 600)
    {
        var window = new Window { Width = width, Height = height, Content = control };
        window.Show();
        return window;
    }

    /// <summary>
    /// Loads the committed single-stage sample whose rows all exit after the
    /// only stage, which is what makes the fitted p_exit exactly 1.0.
    /// </summary>
    private static DataBindingResult AnalyzeSampleWithPExitOne(out string path)
    {
        path = SamplePath("sample_patients.csv");
        var binding = DataAnalyzer.Analyze(path);
        Assert.True(binding.IsUsable, "the single-stage sample must load cleanly");
        Assert.Equal(1.0, binding.FittedExitProbability!.Value, precision: 6);
        return binding;
    }

    private static ConfigPanelViewModel SingleStageScreening()
    {
        var vm = new ConfigPanelViewModel();
        vm.StageCount.Value = "1";
        vm.StageRows[0].StageName = "Screening";
        return vm;
    }

    /// <summary>A manual (EnterManually-shaped) run with an explicit λ and μ per stage.</summary>
    private static ConfigPanelViewModel ManualRun(int stageCount, string mu)
    {
        var vm = new ConfigPanelViewModel();
        vm.ParametersIsOptionalEnabled = true;
        vm.ManualLambda.Value = "0.5";
        vm.ManualMuPerStage.Value = mu;
        vm.StageCount.Value = stageCount.ToString();
        foreach (var row in vm.StageRows)
        {
            row.Servers.Value = "1";
        }

        vm.IsDiagnosticTrace = true;
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
}
