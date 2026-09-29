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

    /// <remarks>
    /// Diagnostic mode is selected explicitly because the Duration dropdown
    /// belongs to that mode (D-174 ruling 6). These tests predate the decoupling
    /// and left the run mode at its ClinicDay default, where the value is not
    /// read at all — the 1-hour sibling passed only because 60 is also the value
    /// a calendar run carries, so it was asserting nothing about the dropdown.
    /// </remarks>
    [Fact]
    public void Duration_FifteenMinutes_ReturnsHorizonMinutes15()
    {
        var vm = new ConfigPanelViewModel { IsDiagnosticTrace = true };
        vm.Duration = DiagnosticDurationPreset.FifteenMinutes;
        Assert.Equal(15.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    [Fact]
    public void Duration_OneHour_ReturnsHorizonMinutes60()
    {
        var vm = new ConfigPanelViewModel { IsDiagnosticTrace = true };
        vm.Duration = DiagnosticDurationPreset.OneHour;
        Assert.Equal(60.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    /// <summary>
    /// The default is one hour (D-174 ruling 6). It was 10000 minutes — roughly
    /// four operating sessions — which produced a trace nobody could read, so
    /// this asserts the shipped default rather than a fact about arithmetic.
    /// </summary>
    [Fact]
    public void Duration_DefaultsToOneHour()
    {
        var vm = new ConfigPanelViewModel { IsDiagnosticTrace = true };
        Assert.Equal(DiagnosticDurationPreset.OneHour, vm.Duration);
        Assert.Equal("1 hour", vm.DurationSelection);
        Assert.Equal(60.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    /// <summary>
    /// The dropdown's first option is the default (ruling 6), so a user who opens
    /// the dropdown sees the value already in force at the top of the list.
    /// </summary>
    [Fact]
    public void Duration_DefaultOptionIsListedFirst()
    {
        var vm = new ConfigPanelViewModel();
        Assert.Equal("1 hour", vm.DurationOptions[0]);
        Assert.Equal(new[] { "1 hour", "15 minutes", "Custom minutes…" }, vm.DurationOptions);
    }

    [Fact]
    public void Duration_CustomMinutes_ParsesFieldValue()
    {
        var vm = new ConfigPanelViewModel();
        vm.Duration = DiagnosticDurationPreset.CustomMinutes;
        vm.CustomMinutes.Value = "10000";
        Assert.Equal(10000, vm.ResolveDiagnosticMinutes());
    }

    /// <summary>
    /// An unparseable custom duration must refuse the build (0) rather than
    /// silently reverting to a default: a run reporting 60 minutes while the
    /// field says "abc" is a wrong answer, not a safe one.
    /// </summary>
    [Fact]
    public void Duration_CustomMinutes_Invalid_RefusesBuild()
    {
        var vm = new ConfigPanelViewModel();
        vm.Duration = DiagnosticDurationPreset.CustomMinutes;
        vm.CustomMinutes.Value = "abc";
        Assert.Equal(0, vm.ResolveDiagnosticMinutes());
    }

    /// <summary>
    /// D-174 removed the time-span presets, which had one dropdown driving BOTH
    /// the diagnostic minutes and the calendar run length. "1 week" meant six
    /// generator days for a five-day clinic. Nothing may reintroduce that
    /// coupling, so the calendar run length is now read only from the Days field.
    /// </summary>
    [Fact]
    public void Duration_DoesNotAffectCalendarRunLength()
    {
        var vm = new ConfigPanelViewModel();
        vm.IsMultiDay = true;
        vm.Days.Value = "3";
        vm.Duration = DiagnosticDurationPreset.FifteenMinutes;

        var parameters = vm.TryBuildRunParameters()!;

        Assert.Equal(3, parameters.GeneratorDays);
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