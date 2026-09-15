using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 4b — optional-section toggles on the ConfigPanel (feat/gui-rebuild).
/// Verification intent: a section whose fields are entirely optional (Parameters:
/// manual λ/μ/p_exit; Advanced: seed/trace level) shows an enable toggle that is
/// OFF by default, disables + dims every descendant field while OFF with a
/// FR-UI-7 explanation tooltip, re-enables them when switched ON, and persists
/// the state in the view model so Clear All resets it (D-101). When a section
/// is OFF the run treats its values as not supplied (ParametersSupplied,
/// EffectiveSeed 42, EffectiveTraceLevel State).
/// </summary>
public class Phase4bConfigTests
{
    private static ConfigPanelViewModel NewVm() => new();

    private static Window Host(ConfigPanel panel, int width = 420, int height = 760)
    {
        var window = new Window { Width = width, Height = height, Content = panel };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static CollapsibleSection Section(Window window, string title)
    {
        var sections = window.GetVisualDescendants().OfType<CollapsibleSection>().ToList();
        return sections.FirstOrDefault(s => s.Title == title)
            ?? throw new InvalidOperationException(
                $"no section with title \"{title}\"; found: {string.Join(", ", sections.Select(s => $"\"{s.Title}\""))}");
    }

    private static ContentPresenter Body(Window window, CollapsibleSection section) =>
        window.GetVisualDescendants().OfType<ContentPresenter>()
            .Single(c => ReferenceEquals(c.Content, section.Content));

    [AvaloniaFact]
    public void OptionalSection_ToggleOff_DisablesFields()
    {
        var vm = NewVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var section = Section(window, "3 · Parameters");

            Assert.True(section.IsOptional, "Parameters must be flagged as an optional section");
            Assert.False(section.IsEnabledToggle, "every optional section must default to OFF");

            var fields = section.GetVisualDescendants().OfType<ValidatedField>().ToList();
            Assert.Equal(3, fields.Count); // λ, μ, p_exit — the run-category overrides
            Assert.All(fields, f => Assert.False(f.IsEffectivelyEnabled,
                "every descendant field must be effectively disabled while the section toggle is OFF"));

            Assert.All(fields, f => Assert.Contains(
                "Parameters",
                ToolTip.GetTip(f) as string ?? "",
                StringComparison.OrdinalIgnoreCase));

            var body = Body(window, section);
            Assert.False(body.IsEnabled);
            Assert.True(Math.Abs(body.Opacity - 0.5) < 0.001,
                "a disabled optional section must be dimmed to 50% so the state reads as inactive");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OptionalSection_ToggleOn_EnablesFields()
    {
        var vm = NewVm();
        var panel = new ConfigPanel { DataContext = vm };
        var window = Host(panel);

        try
        {
            var section = Section(window, "3 · Parameters");

            var toggle = section.GetVisualDescendants().OfType<ToggleSwitch>().Single();
            toggle.IsChecked = true;

            Assert.True(section.IsEnabledToggle, "the header switch must write back to the section");
            Assert.True(vm.ParametersIsOptionalEnabled,
                "the two-way binding must reach the view model, where Clear All can reset it");

            var fields = section.GetVisualDescendants().OfType<ValidatedField>().ToList();
            Assert.All(fields, f => Assert.True(f.IsEffectivelyEnabled,
                "fields must become effectively editable the moment the section toggle is ON"));

            var body = Body(window, section);
            Assert.True(body.IsEnabled);
            Assert.True(Math.Abs(body.Opacity - 1.0) < 0.001, "enabling the section must restore full opacity");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClearAll_ResetsOptionalTogglesToOff()
    {
        var vm = NewVm();
        vm.ParametersIsOptionalEnabled = true;
        vm.AdvancedIsOptionalEnabled = true;
        Assert.True(vm.ParametersSupplied);

        vm.ResetToDefaults();

        Assert.False(vm.ParametersIsOptionalEnabled, "Clear All must reset the Parameters toggle to OFF");
        Assert.False(vm.AdvancedIsOptionalEnabled, "Clear All must reset the Advanced toggle to OFF");
        Assert.False(vm.ParametersSupplied, "with Parameters OFF the run must see no manual overrides");
        Assert.Equal(42, vm.EffectiveSeed);
        Assert.Equal("State", vm.EffectiveTraceLevel);
    }

    [AvaloniaFact]
    public void OptionalParametersOff_TreatsManualOverridesAsNotSupplied()
    {
        var vm = NewVm();
        vm.ManualLambda.Value = "0.5";
        vm.ManualMuPerStage.Value = "0.8";

        Assert.False(vm.ParametersSupplied,
            "a factory-default config (section OFF) must supply no manual overrides to the run");

        vm.ParametersIsOptionalEnabled = true;
        Assert.True(vm.ParametersSupplied, "switching the section ON supplies the overrides again");

        Assert.Contains("—", vm.RhoSummary);
        vm.ParametersIsOptionalEnabled = false;
        Assert.True(vm.RhoSummary.Contains("—"),
            "λ is not supplied while Parameters is OFF, so ρ per stage must read as unknown");
    }

    [AvaloniaFact]
    public void OptionalAdvancedOff_DefaultsSeedAndTraceLevel()
    {
        var vm = NewVm();
        vm.Seed.Value = "7";
        vm.TraceLevel = "Rng";

        Assert.Equal(42, vm.EffectiveSeed);
        Assert.Equal("State", vm.EffectiveTraceLevel);

        vm.AdvancedIsOptionalEnabled = true;
        Assert.Equal(7, vm.EffectiveSeed);
        Assert.Equal("Rng", vm.EffectiveTraceLevel);
    }
}