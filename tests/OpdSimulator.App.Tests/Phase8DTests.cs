using Avalonia.Headless.XUnit;
using OpdSimulator.App.ViewModels;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8D gate — final polish. Verification intent: the "Expand all" /
/// "Collapse all" commands (8D.3) drive every configuration section's two-way
/// <c>IsExpanded</c> state, so the user can switch between the full form and a
/// header-only summary with one click (AGENTS §16.1 — the user can always undo
/// or reset).
/// </summary>
public class Phase8DTests
{
    private static ConfigPanelViewModel NewVm() => new();

    [AvaloniaFact]
    public void CollapseAll_SetsEverySectionCollapsed()
    {
        var vm = NewVm();
        Assert.True(vm.IsModelSectionExpanded);
        Assert.True(vm.IsParametersSectionExpanded);
        Assert.True(vm.IsStagesSectionExpanded);
        Assert.True(vm.IsHorizonSectionExpanded);
        Assert.True(vm.IsAdvancedSectionExpanded);

        vm.CollapseAllCommand.Execute(null);

        Assert.False(vm.IsModelSectionExpanded);
        Assert.False(vm.IsParametersSectionExpanded);
        Assert.False(vm.IsStagesSectionExpanded);
        Assert.False(vm.IsHorizonSectionExpanded);
        Assert.False(vm.IsAdvancedSectionExpanded);
    }

    [AvaloniaFact]
    public void ExpandAll_SetsEverySectionExpanded()
    {
        var vm = NewVm();
        vm.CollapseAllCommand.Execute(null);

        vm.ExpandAllCommand.Execute(null);

        Assert.True(vm.IsModelSectionExpanded);
        Assert.True(vm.IsParametersSectionExpanded);
        Assert.True(vm.IsStagesSectionExpanded);
        Assert.True(vm.IsHorizonSectionExpanded);
        Assert.True(vm.IsAdvancedSectionExpanded);
    }
}
