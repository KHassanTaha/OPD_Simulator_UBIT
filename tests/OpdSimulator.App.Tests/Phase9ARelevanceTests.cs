using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 9A gate: the configuration panel shows only controls that can affect
/// the current path, names every unit in the unit the user chose, and describes
/// the entity flow honestly — one engine-wide arrival process, and a derived
/// per-stage inflow instead of a per-stage arrival dropdown (D-225, D-229).
/// </summary>
public class Phase9ARelevanceTests
{
    private static ConfigPanelViewModel NewVm()
    {
        var vm = new ConfigPanelViewModel();
        vm.StageCount.Value = "3";
        return vm;
    }

    private static Window Host(ConfigPanel panel)
    {
        // No explicit sizing (D-166): the test reads the same layout the app does.
        var window = new Window { Content = panel };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static ValidatedField Field(Window window, string anchor)
        => window.GetVisualDescendants().OfType<ValidatedField>()
            .Single(f => f.HelpAnchor == anchor);

    private static Button ManageInputButton(Window window)
        => window.GetVisualDescendants().OfType<Button>()
            .Single(b => (b.Content?.ToString() ?? "") == "Manage input →");

    // ── G1: the data-source strip ────────────────────────────────────────

    [AvaloniaFact]
    public void FitMode_ShowsDataSourceStrip()
    {
        // Control case: the binding was not deleted on the fit path, only made
        // conditional. Observe effective visibility (D-169) because the parent
        // Border owns the flag, not the button.
        var vm = new ConfigPanelViewModel();
        var window = Host(new ConfigPanel { DataContext = vm });
        try
        {
            Assert.True(vm.IsDataSourceStripVisible);
            Assert.True(ManageInputButton(window).IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ManualMode_HidesDataSourceStrip()
    {
        // G1: the manual path has no file to manage, so the strip leaves the
        // visible set. The Input tab is still a top-level tab (no lost link).
        var vm = new ConfigPanelViewModel { SourceMode = DataSourceMode.EnterManually };
        var window = Host(new ConfigPanel { DataContext = vm });
        try
        {
            Assert.False(vm.IsDataSourceStripVisible);
            Assert.False(ManageInputButton(window).IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    // ── G3: routing overrides follow the stage count ─────────────────────

    [AvaloniaFact]
    public void StageCountDropsToOne_HidesPBypass()
    {
        var vm = NewVm();
        var window = Host(new ConfigPanel { DataContext = vm });

        try
        {
            var pExit = Field(window, "p-exit");
            var pBypass = Field(window, "p-bypass");
            Assert.True(pExit.IsEffectivelyVisible, "3 stages: p_exit is the control case");
            Assert.True(pBypass.IsEffectivelyVisible, "3 stages: p_bypass is the control case");

            vm.StageCount.Value = "1";
            window.UpdateLayout();

            Assert.False(vm.PExitVisible);
            Assert.False(vm.PBypassVisible, "G3: one stage has nowhere to bypass to");
            Assert.False(pExit.IsEffectivelyVisible, "p_exit must leave the visible set after 3→1");
            Assert.False(pBypass.IsEffectivelyVisible, "p_bypass must not stay on screen after 3→1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StageCountRisesToTwo_ShowsPBypass()
    {
        // Two is the exact threshold: bypass needs a stage to skip and a later
        // stage to land on, so one stage hides it and two restores it.
        var vm = NewVm();
        vm.StageCount.Value = "1";
        var window = Host(new ConfigPanel { DataContext = vm });

        try
        {
            var pBypass = Field(window, "p-bypass");
            Assert.False(pBypass.IsEffectivelyVisible);

            vm.StageCount.Value = "2";
            window.UpdateLayout();

            Assert.True(vm.PBypassVisible);
            Assert.True(pBypass.IsEffectivelyVisible, "two stages restore p_bypass");
        }
        finally
        {
            window.Close();
        }
    }

    // ── G6: captions follow the chosen unit and interpretation ───────────

    /// <summary>
    /// Five rate units. Expected strings are rebuilt from <see cref="FittingTimeUnits"/>
    /// — the same source the production caption uses — so this asserts the binding
    /// follows that source rather than a copied literal.
    /// </summary>
    [Theory]
    [InlineData(ParameterMode.RateWise, TimeUnit.Minutes)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Seconds)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Hours)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Days)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Sessions)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Minutes)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Seconds)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Hours)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Days)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Sessions)]
    public void LambdaLabel_FollowsTimeUnitAndParameterMode(ParameterMode mode, TimeUnit unit)
    {
        var vm = new ConfigPanelViewModel { ParameterMode = mode, TimeUnit = unit };
        var fitting = TimeUnits.ToFitting(unit);
        bool mean = mode == ParameterMode.MeanWise;
        string duration = FittingTimeUnits.Label(fitting);
        string rate = FittingTimeUnits.RateLabel(fitting);
        string suffix = FittingTimeUnits.RateUnit(fitting);

        var (label, badge) = mean
            ? ($"Manual mean inter-arrival 1/λ ({duration})", suffix)
            : ($"Manual λ ({rate})", $"/ {suffix}");

        Assert.Equal(label, vm.LambdaFieldLabel);
        Assert.Equal(badge, vm.LambdaFieldUnit);
    }

    [Theory]
    [InlineData(ParameterMode.RateWise, TimeUnit.Minutes)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Seconds)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Hours)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Days)]
    [InlineData(ParameterMode.RateWise, TimeUnit.Sessions)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Minutes)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Seconds)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Hours)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Days)]
    [InlineData(ParameterMode.MeanWise, TimeUnit.Sessions)]
    public void MuLabel_FollowsTimeUnitAndParameterMode(ParameterMode mode, TimeUnit unit)
    {
        var vm = new ConfigPanelViewModel { ParameterMode = mode, TimeUnit = unit };
        var fitting = TimeUnits.ToFitting(unit);
        bool mean = mode == ParameterMode.MeanWise;
        string duration = FittingTimeUnits.Label(fitting);
        string rate = FittingTimeUnits.RateLabel(fitting);
        string suffix = FittingTimeUnits.RateUnit(fitting);

        var (panelLabel, panelBadge) = mean
            ? ($"Manual mean service time 1/μ per stage ({duration})", suffix)
            : ($"Manual μ per stage ({rate})", $"/ {suffix}");
        Assert.Equal(panelLabel, vm.MuFieldLabel);
        Assert.Equal(panelBadge, vm.MuFieldUnit);

        // The row cannot see the panel's unit; it is pushed from the same source.
        var row = vm.StageRows[0];
        var (rowLabel, rowBadge) = mean
            ? ($"Service mean 1/μ ({duration})", suffix)
            : ($"Service rate μ ({rate})", $"/ {suffix}");
        Assert.Equal(rowLabel, row.MuLabel);
        Assert.Equal(rowBadge, row.MuUnit);
    }

    [Theory]
    [InlineData(TimeUnit.Minutes)]
    [InlineData(TimeUnit.Seconds)]
    [InlineData(TimeUnit.Hours)]
    [InlineData(TimeUnit.Days)]
    [InlineData(TimeUnit.Sessions)]
    public void SpreadLabel_FollowsTimeUnit(TimeUnit unit)
    {
        var vm = new ConfigPanelViewModel { TimeUnit = unit };
        var row = vm.StageRows[0];
        string duration = FittingTimeUnits.Label(TimeUnits.ToFitting(unit));
        string suffix = FittingTimeUnits.RateUnit(TimeUnits.ToFitting(unit));

        row.ServiceFamily = DistributionFamily.Normal;
        Assert.Equal($"Standard deviation σ ({duration})", row.SpreadLabel);
        Assert.Equal(suffix, row.SpreadUnit);

        row.ServiceFamily = DistributionFamily.Uniform;
        Assert.Equal($"Half-width w ({duration})", row.SpreadLabel);
        Assert.Equal(suffix, row.SpreadUnit);
    }

    [AvaloniaFact]
    public void Captions_AreBoundOnTheInputFields()
    {
        // A correct view-model value is not enough: the field must actually read
        // it, and keep reading it after a unit/mode change (a binding, not a
        // one-time copy).
        var vm = new ConfigPanelViewModel { TimeUnit = TimeUnit.Hours };
        var window = Host(new ConfigPanel { DataContext = vm });

        try
        {
            var lambda = Field(window, "manual-lambda");
            Assert.Equal(vm.LambdaFieldLabel, lambda.Label);
            Assert.Equal(vm.LambdaFieldUnit, lambda.Unit);

            vm.ParameterMode = ParameterMode.MeanWise;
            window.UpdateLayout();
            Assert.Equal(vm.LambdaFieldLabel, lambda.Label);
            Assert.Equal(vm.LambdaFieldUnit, lambda.Unit);
        }
        finally
        {
            window.Close();
        }
    }

    // ── D-229: entity flow, told honestly ────────────────────────────────

    [AvaloniaFact]
    public void NoPerStageArrivalDropdown_Exists()
    {
        // The old control claimed each stage chose its own arrival process. It
        // must be gone from the tree, not merely hidden — a hidden control would
        // still be found here and the lie would survive for the screen reader.
        var vm = NewVm();
        var window = Host(new ConfigPanel { DataContext = vm });

        try
        {
            var dropdowns = window.GetVisualDescendants().OfType<DropdownField>().ToList();
            Assert.DoesNotContain(dropdowns, d => d.HelpAnchor == "stage-arrival-distribution");
            Assert.DoesNotContain(dropdowns, d => d.HelpAnchor == "default-arrival-family");
        }
        finally
        {
            window.Close();
        }
    }

    [Theory]
    [InlineData("Exponential", "M")]
    [InlineData("Deterministic", "D")]
    [InlineData("Gamma", "G")]
    public void NotationFirstLetter_TracksTheModelSection(string arrivalFamily, string letter)
    {
        // One arrival process is chosen in Model; every row's notation follows
        // it and its service-side choices are rebuilt (§5.4 step 2). A row must
        // never be able to retune the engine-wide family from its own dropdown.
        var vm = NewVm();
        vm.InterArrivalDistribution = arrivalFamily;

        Assert.All(vm.StageRows, row =>
        {
            Assert.Equal(letter, row.ArrivalLetter);
            var specific = row.ModelOptions
                .Where(m => !ModelNotationParser.IsGeneralModel(m))
                .ToList();
            Assert.All(specific, m => Assert.StartsWith($"{letter}/", m, StringComparison.Ordinal));
            // G/G/c stays available on every row: G names no family, it asks the
            // data to choose one, so it never claims an arrival process (D-229).
            Assert.Contains("G/G/1", row.ModelOptions);
        });
    }

    [Fact]
    public void InflowLine_EqualsTheTopology_ForEveryStage()
    {
        // §5.4 step 3: the row's read-only inflow line is formatted from 9P's
        // topology. Build that topology independently (the run's own recipe)
        // and require every number on screen to match it — including the routed
        // inflow, which is NOT the front-door λ once exit/bypass are configured.
        var vm = new ConfigPanelViewModel();
        vm.ApplyLoadedFile(Sample("sample_3stage_variable.csv"));
        Assert.True(vm.Binding?.IsUsable == true, "the three-stage sample must analyse cleanly");
        vm.ParametersIsOptionalEnabled = true;
        vm.DailyCap.Value = ""; // set first: the cap changes the topology's admitted load
        vm.ManualLambda.Value = "0.5";
        vm.PExit.Value = "0.4";
        vm.PBypass.Value = "0.25";

        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        var specs = parameters!.StageNames
            .Select((name, i) =>
            {
                double mu = parameters.ManualServiceRates[i] ?? FittedRates(vm)[i];
                return new StageSpec(name, parameters.ServerCounts[i], mu, parameters.ServiceFamilies[i]);
            })
            .ToList();
        var topology = new NetworkTopology(
            parameters.ManualArrivalRate!.Value,
            specs,
            exitStageIndex: 1, // Screening, spelled out — not mirrored from the UI
            exitProbability: 0.4,
            bypassProbability: 0.25,
            bypassStageIndex: 1,
            bypassDestinationIndex: 2);

        // Routing really changes the downstream inflow; otherwise this test
        // could pass by echoing the front-door λ on every row.
        Assert.True(topology.EffectiveArrivalRate(1) < topology.EffectiveArrivalRate(0));

        string rateLabel = FittingTimeUnits.RateLabel(TimeUnits.ToFitting(vm.TimeUnit));
        for (int i = 0; i < vm.StageRows.Count; i++)
        {
            // Default TimeUnit is minutes, where DescribeFittedRate prints the
            // bare number with 0.##. Mirror only that formatting — never the
            // arithmetic — so a wrong inflow cannot be hidden by the assertion.
            string expected =
                $"Inflow λᵢ = {topology.EffectiveArrivalRate(i):0.##} ({rateLabel})   ρᵢ = {topology.RhoFor(i):0.00}";
            Assert.Equal(expected, vm.StageRows[i].InflowLabel);
        }
    }

    // ── fixtures ─────────────────────────────────────────────────────────

    private static double[] FittedRates(ConfigPanelViewModel vm) =>
        vm.Binding!.FittedServiceRates.ToArray();

    private static string Sample(string fileName)
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
