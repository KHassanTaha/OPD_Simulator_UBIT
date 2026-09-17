using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Data.Parameters;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 7A — time-unit selection, parameter-mode relocation and time-span
/// presets. Pure view-model tests use [Fact]; the dropdown-to-view-model
/// binding test uses [AvaloniaFact] because it hosts a real control.
/// </summary>
public class Phase7ATests
{
    [Fact]
    public void TimeUnit_Default_IsMinutes()
    {
        Assert.Equal(TimeUnit.Minutes, new ConfigPanelViewModel().TimeUnit);
    }

    [Fact]
    public void ParameterMode_Default_IsRateWise()
    {
        Assert.Equal(ParameterMode.RateWise, new ConfigPanelViewModel().ParameterMode);
    }

    /// <summary>
    /// ToPerMinute is private on purpose (7A.5); these three tests reach it
    /// through the public build seam — TryBuildRunParameters converts the
    /// manual λ to per-minute, so the asserted ArrivalRate IS the converted
    /// value.
    /// </summary>
    [Fact]
    public void ToPerMinute_Minutes_ReturnsUnchanged()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.ManualLambda.Value = "0.5";
        Assert.Equal(0.5, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void ToPerMinute_Seconds_MultipliesBy60()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.TimeUnit = TimeUnit.Seconds;
        vm.ManualLambda.Value = "0.5";
        Assert.Equal(30.0, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void ToPerMinute_Hours_DividesBy60()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.TimeUnit = TimeUnit.Hours;
        vm.ManualLambda.Value = "60";
        Assert.Equal(1.0, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void RateWise_LambdaPassesThrough()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.ParameterMode = ParameterMode.RateWise;
        vm.TimeUnit = TimeUnit.Minutes;
        vm.ManualLambda.Value = "0.5";
        Assert.Equal(0.5, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void MeanWise_LambdaIsInverseOfMean()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.ParameterMode = ParameterMode.MeanWise;
        vm.TimeUnit = TimeUnit.Minutes;
        vm.ManualLambda.Value = "2.0";
        Assert.Equal(0.5, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void MeanWise_SecondsMean_ConvertsCorrectly()
    {
        var vm = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
        vm.ParameterMode = ParameterMode.MeanWise;
        vm.TimeUnit = TimeUnit.Seconds;
        vm.ManualLambda.Value = "120";
        Assert.Equal(0.5, vm.TryBuildRunParameters()!.ManualArrivalRate);
    }

    [Fact]
    public void TimeSpan_FifteenMinutes_ReturnsHorizonMinutes15()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.FifteenMinutes;
        Assert.Equal(15.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    [Fact]
    public void TimeSpan_OneHour_ReturnsHorizonMinutes60()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.OneHour;
        Assert.Equal(60.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    [Fact]
    public void TimeSpan_OneDay_ReturnsGeneratorDays1()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.OneDay;
        Assert.Equal(1, vm.ResolveGeneratorDays());
    }

    [Fact]
    public void TimeSpan_OneWeek_ReturnsGeneratorDays6()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.OneWeek;
        // Mon/Tue/Wed/Thu/Sat = 5 operating days, plus the following Monday
        // to complete the clinic week → 6 generator days within a 7-day span.
        Assert.Equal(6, vm.ResolveGeneratorDays());
    }

    [Fact]
    public void TimeSpan_OneMonth_ReturnsGeneratorDays26()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.OneMonth;
        // 30 days × 5/7 operating ratio ≈ 21.4 → 22, × 1.2 buffer → 26.
        Assert.Equal(26, vm.ResolveGeneratorDays());
    }

    [Fact]
    public void TimeSpan_CustomDays_ParsesFieldValue()
    {
        var vm = new ConfigPanelViewModel();
        vm.TimeSpan = TimeSpanPreset.CustomDays;
        vm.CustomDays.Value = "14";
        Assert.Equal(14, vm.ResolveGeneratorDays());
    }

    /// <summary>
    /// The wrapper must bridge the strongly typed TimeUnit to the inner
    /// SearchableDropdown's string selection in both directions: when the
    /// view model changes TimeUnit, the dropdown's committed item follows.
    /// The dropdown selection is committed programmatically so the binding
    /// (not a pointer event) is the seam under test.
    /// </summary>
    [AvaloniaFact]
    public void TimeUnitSelector_BindsToViewModel()
    {
        var vm = new ConfigPanelViewModel();
        var selector = new TimeUnitSelector();
        selector.Bind(TimeUnitSelector.SelectedUnitProperty,
            new Binding(nameof(ConfigPanelViewModel.TimeUnit)) { Source = vm, Mode = BindingMode.TwoWay });

        var window = new Window { Content = selector };
        window.Show();
        try
        {
            var dropdown = selector.GetVisualDescendants().OfType<SearchableDropdown>().Single();

            Assert.Equal("Minutes", dropdown.SelectedItem);

            vm.TimeUnit = TimeUnit.Hours;
            Assert.Equal("Hours", dropdown.SelectedItem);

            vm.TimeUnit = TimeUnit.Seconds;
            Assert.Equal("Seconds", dropdown.SelectedItem);
        }
        finally
        {
            window.Close();
        }
    }
}