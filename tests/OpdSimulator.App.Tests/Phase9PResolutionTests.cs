namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Parameters;
using Xunit;

/// <summary>
/// Phase 9P, resolution and gating (D-242, D-243, D-244, D-245): one μ resolver
/// with provenance, a ρ preview that asks the run's own topology, a Start gate
/// that reads only visible fields, and a service fit that never writes μ.
/// </summary>
/// <remarks>
/// Every test here is written against the value the RUN would use
/// (<c>ManualServiceRates</c>, <c>ServiceFamilies</c>, or the topology's ρ), not
/// against the caption that describes it. A caption test cannot tell a correct
/// number from a wrong one, and the defect 9P fixes was exactly a caption and a
/// number disagreeing.
/// </remarks>
public class Phase9PResolutionTests
{
    // ── μ: the fit wins, an explicit edit does not ──────────────────────

    [Fact]
    public void UneditedCoveredStage_UsesTheFit()
    {
        // Why it matters: a blank μ means "use the data". If the resolver left a
        // value in ManualServiceRates here, the coordinator would stop consulting
        // the file and the run would silently diverge from the uploaded capture.
        var vm = LoadedPanel();
        var fitted = FittedRates(vm);

        var parameters = vm.TryBuildRunParameters();

        Assert.NotNull(parameters);
        Assert.All(parameters!.ManualServiceRates, rate => Assert.Null(rate));
        for (int i = 0; i < parameters.ServiceFamilies.Count; i++)
        {
            // The sample's stage order matches the row order, so the binding's rate
            // and the row are the same stage.
            Assert.Equal(1.0 / fitted[i], parameters.ServiceFamilies[i].Mean, 9);
        }
    }

    [Fact]
    public void PreexistingMu_EnteredBeforeLoad_DoesNotOverrideTheFit()
    {
        // Why it matters: "the field is not empty" and "the user meant it" are
        // different questions. A value typed before any file existed has nothing to
        // override, so the fit must take it over — otherwise loading data would
        // appear to do nothing for a user who had typed a guess first.
        var vm = new ConfigPanelViewModel();
        vm.StageRows[1].MuValue = "0.9";
        Assert.Equal(MuProvenance.Default, vm.StageRows[1].MuProvenance);

        vm.ApplyLoadedFile(Sample("sample_3stage_variable.csv"));

        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Null(parameters!.ManualServiceRates[1]);
        Assert.Equal(1.0 / FittedRates(vm)[1], parameters.ServiceFamilies[1].Mean, 9);
        Assert.EndsWith("(from data)", vm.StageRows[1].ServiceRateLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void UserEditedMu_ReachesManualServiceRates()
    {
        // Why it matters: the owner needs to change μ by hand to demonstrate the
        // simulator with a modified service rate. If the edit stayed in the field
        // while the run kept using the fit, the demonstration would silently be of
        // the fitted system.
        var vm = LoadedPanel();
        vm.StageRows[1].MuValue = "0.3";

        Assert.Equal(MuProvenance.UserSet, vm.StageRows[1].MuProvenance);

        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(0.3, parameters!.ManualServiceRates[1]!.Value, 12);
        Assert.Equal(0.3, parameters.ServiceRates[1]!.Value, 12);
        Assert.Equal(1.0 / 0.3, parameters.ServiceFamilies[1].Mean, 9);
    }

    [Fact]
    public void ServiceRateLabel_ReadsProvenance_NotAStickyFlag()
    {
        // Why it matters: seam S3 was a caption that kept crediting the fit after
        // the user overrode it. The label must change with the value, in the same
        // instant, or the user is told the wrong provenance for their own number.
        var vm = LoadedPanel();
        Assert.EndsWith("(from data)", vm.StageRows[1].ServiceRateLabel, StringComparison.Ordinal);

        vm.StageRows[1].MuValue = "0.3";

        Assert.EndsWith("(per-stage)", vm.StageRows[1].ServiceRateLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("(from data)", vm.StageRows[1].ServiceRateLabel);
    }

    [Fact]
    public void MuField_IsVisibleInFitMode_AndItsPlaceholderCarriesTheFittedValue()
    {
        // Why it matters: D-242 shows the μ field in both modes. Hiding it in fit
        // mode was what made a manual test edit impossible; showing it blank would
        // make the user think no fit existed.
        var vm = LoadedPanel();
        var row = vm.StageRows[1];

        Assert.True(row.IsMuVisible);
        Assert.True(string.IsNullOrWhiteSpace(row.MuValue), "loading must not write μ into the field");
        Assert.False(string.IsNullOrWhiteSpace(row.MuPlaceholder));
        Assert.DoesNotContain("e.g.", row.MuPlaceholder);
    }

    // ── ρ: the preview is the run's topology ────────────────────────────

    [Fact]
    public void RhoSummary_WithExit_MatchesTopologyRhoForEveryStage()
    {
        // Why it matters: the preview used to be λ/(c·μ) from the front-door λ, so
        // Screening and Doctor were shown busier than the run would make them. The
        // only acceptable definition is the topology's own ρ — so this test builds
        // that topology independently, with the exit stage spelled out (index 1 of
        // three — Screening), and requires every number on screen to match.
        var vm = LoadedPanel();
        vm.ParametersIsOptionalEnabled = true;
        vm.ManualLambda.Value = "0.5";
        vm.PExit.Value = "0.4";
        vm.DailyCap.Value = ""; // unlimited: the 85 default would cap offered load

        var topology = ExpectedTopology(vm, exitProbability: 0.4, bypassProbability: 0.0);

        Assert.NotEqual("—", vm.RhoSummary);
        Assert.All(
            Enumerable.Range(0, 3),
            i => Assert.True(
                vm.RhoSummary.Contains($"{vm.StageRows[i].StageName}: {topology.RhoFor(i):0.00}", StringComparison.Ordinal),
                $"stage {i} ({vm.StageRows[i].StageName}) expected rho {topology.RhoFor(i):0.00} but summary = [{vm.RhoSummary}]"));
    }

    [Fact]
    public void RhoSummary_WithBypass_MatchesTopologyRhoForEveryStage()
    {
        // Why it matters: p_bypass removes load from Screening and adds it to the
        // Doctor, so a preview that ignores it shows the wrong stage as the busiest.
        // The exit probability is left to its resolved value (fitted, else default),
        // which is what the run does.
        var vm = LoadedPanel();
        vm.ParametersIsOptionalEnabled = true;
        vm.ManualLambda.Value = "0.5";
        vm.PExit.Value = "";
        vm.PBypass.Value = "0.25";
        vm.DailyCap.Value = "";

        double exit = vm.Binding?.FittedExitProbability
            ?? SimulationCoordinator.DefaultExitProbability;
        var topology = ExpectedTopology(vm, exit, bypassProbability: 0.25);

        Assert.NotEqual("—", vm.RhoSummary);
        Assert.All(
            Enumerable.Range(0, 3),
            i => Assert.True(
                vm.RhoSummary.Contains($"{vm.StageRows[i].StageName}: {topology.RhoFor(i):0.00}", StringComparison.Ordinal),
                $"stage {i} ({vm.StageRows[i].StageName}) expected rho {topology.RhoFor(i):0.00} but summary = [{vm.RhoSummary}]"));
    }

    [Fact]
    public void RhoSummary_UnknownInputs_RendersDashNotZero()
    {
        // Why it matters: a half-typed form has no ρ. Rendering 0.00 would claim a
        // knowably idle system, which is a far more confident statement than "not
        // yet known" and is wrong in a way a screenshot could not reveal.
        var vm = new ConfigPanelViewModel();

        Assert.Equal("—", vm.RhoSummary);
        Assert.DoesNotContain("0.00", vm.RhoSummary);

        vm.ManualLambda.Value = "0.5"; // λ alone is not enough: no μ anywhere
        Assert.Equal("—", vm.RhoSummary);
    }

    // ── the Start gate ──────────────────────────────────────────────────

    [Fact]
    public void HiddenField_NeverBlocksStart()
    {
        // Why it matters: the two 9A G4 repros. Before 9P a stale error on a control
        // the user could no longer see disabled Start with no way to find it, because
        // the gate checked every field it knew about rather than every field in use.
        var commaList = ManualPanel();
        commaList.ManualMuPerStage.Value = "not-a-number";
        commaList.ValidateManualMuPerStage();
        Assert.True(commaList.ManualMuPerStage.HasError, "the fixture must actually have a stale error");
        Assert.True(commaList.StartIsEnabled, "a comma-list error is invisible in manual mode and must not block");

        var pExit = ManualPanel();
        pExit.PExit.Value = "1.5";
        pExit.ValidatePExit();
        Assert.False(pExit.StartIsEnabled, "a visible p_exit error must block");

        pExit.StageCount.Value = "1";
        Assert.True(pExit.StartIsEnabled, "p_exit is hidden with one stage, so its stale error must not block");
        Assert.Equal("1.5", pExit.PExit.Value);
    }

    [Fact]
    public void ManualMode_IgnoresStaleCommaListMuError()
    {
        // Why it matters: the same repro, asserted on the banner as well as the flag.
        // A disabled Start that names a field the user cannot see is still a defect.
        var vm = ManualPanel();
        vm.ManualMuPerStage.Value = "abc";
        vm.ValidateManualMuPerStage();

        Assert.True(vm.StartIsEnabled);
        Assert.DoesNotContain("μ per stage", vm.StartBlockedMessage);
    }

    [Fact]
    public void StageCountDropsToOne_IgnoresStalePExitError()
    {
        // Why it matters: a one-stage network has no exit routing, so p_exit cannot
        // be part of the configuration. Its error must follow its visibility.
        var vm = ManualPanel();
        vm.PExit.Value = "1.5";
        vm.ValidatePExit();
        Assert.False(vm.StartIsEnabled);

        vm.StageCount.Value = "1";

        Assert.True(vm.StartIsEnabled);
        Assert.True(vm.PExit.HasError, "the stale error is kept, not silently cleared");
        Assert.DoesNotContain("p_exit", vm.StartBlockedMessage);
    }

    [Fact]
    public void SwitchingBack_ReinstatesTheHiddenError()
    {
        // Why it matters: ignoring a hidden field must not mean forgetting it. Coming
        // back to two stages restores the routing the error is about, so Start must
        // refuse again — with the same text still in the field.
        var vm = ManualPanel();
        vm.PExit.Value = "1.5";
        vm.ValidatePExit();
        vm.StageCount.Value = "1";
        Assert.True(vm.StartIsEnabled);

        vm.StageCount.Value = "2";
        foreach (var row in vm.StageRows)
        {
            row.MuValue = "1.0"; // a re-grown row starts blank, which would block for its own reason
        }

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("p_exit", vm.StartBlockedMessage, StringComparison.Ordinal);
        Assert.Equal("1.5", vm.PExit.Value);
    }

    [Fact]
    public void StartBlockedMessage_IsNeverEmptyWhenStartIsDisabled()
    {
        // Why it matters: FR-UI-7. Before 9P a field-level error could block Start
        // while the banner said nothing at all, leaving the user with a dead button
        // and no explanation.
        var vm = ManualPanel();
        vm.SignificanceLevel.Value = "1.5";
        vm.ValidateSignificanceLevel();

        Assert.False(vm.StartIsEnabled);
        Assert.False(string.IsNullOrWhiteSpace(vm.StartBlockedMessage));
        Assert.Contains("marked in red", vm.StartBlockedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPBypass_DisablesStartAndNamesTheField()
    {
        // Why it matters: 9A's G5. The field turned red, the run started anyway, and
        // the override was silently dropped — the user believed a number that was
        // never applied. Naming the field in the banner is what makes it findable.
        var vm = ManualPanel(stages: 3);
        vm.PBypass.Value = "1.5";
        vm.ValidatePBypass();

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("p_bypass", vm.StartBlockedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void IsAutoFitting_True_BlocksStartWithItsOwnReason()
    {
        // Why it matters: 9C will raise this around the load pass and must not have to
        // edit the gate to do it. Shipping the hook untested would leave 9C editing
        // another phase's file — the coupling this seam exists to remove.
        var vm = ManualPanel();
        Assert.True(vm.StartIsEnabled);

        vm.IsAutoFitting = true;

        Assert.False(vm.StartIsEnabled);
        Assert.Contains("fitting the uploaded data", vm.StartBlockedMessage, StringComparison.Ordinal);

        vm.IsAutoFitting = false;
        Assert.True(vm.StartIsEnabled);
    }

    // ── the fit writes a shape, not a rate ──────────────────────────────

    [Fact]
    public void ApplyFittedShape_NeverWritesMu()
    {
        // Why it matters: seam S2. Two authorities for μ is how the field and the run
        // drift apart. The load path must move the family and the spread only — and a
        // μ the user typed must survive a fit untouched.
        var blank = new StageRow();
        blank.ApplyFittedShape(new DistributionSpec(DistributionFamily.Gamma, 4.0, Shape: 2.0, Scale: 2.0));

        Assert.Equal(string.Empty, blank.MuValue);
        Assert.Equal(MuProvenance.Default, blank.MuProvenance);
        Assert.Equal(DistributionFamily.Gamma, blank.ServiceFamily);
        Assert.Equal("2", blank.ServiceShape);

        var typed = new StageRow { MuValue = "0.25" };
        typed.ApplyFittedShape(new DistributionSpec(DistributionFamily.Normal, 4.0, StdDev: 1.5));

        Assert.Equal("0.25", typed.MuValue);
        Assert.Equal(MuProvenance.UserSet, typed.MuProvenance);
        Assert.Equal(DistributionFamily.Normal, typed.ServiceFamily);
    }

    [Fact]
    public void ApplyFittedSpec_MeanWise_WritesTheMean()
    {
        // Why it matters: the explicit G/G/c path is the only caller allowed to fill a
        // blank μ. In mean-wise mode the field holds a DURATION, so writing the rate
        // would describe a service time of 1/mean minutes — 8K did exactly that.
        var meanWise = new StageRow();
        meanWise.ApplyFittedSpec(new DistributionSpec(DistributionFamily.Exponential, 4.0), ParameterMode.MeanWise);

        var rateWise = new StageRow();
        rateWise.ApplyFittedSpec(new DistributionSpec(DistributionFamily.Exponential, 4.0), ParameterMode.RateWise);

        Assert.Equal(4.0, double.Parse(meanWise.MuValue), 9);
        Assert.Equal(0.25, double.Parse(rateWise.MuValue), 9);
        Assert.Equal(MuProvenance.Fitted, meanWise.MuProvenance);
        Assert.Equal(MuProvenance.Fitted, rateWise.MuProvenance);
    }

    // ── the spread follows the one unit ─────────────────────────────────

    [Fact]
    public void BuildSpec_InHours_ConvertsSigmaButNotShape()
    {
        // Why it matters: a spread is a duration, so it must follow the unit exactly
        // as μ does; the Gamma shape k is unitless, so converting it would change the
        // distribution's shape rather than its scale. Neither direction is obvious
        // from the type system, so both are pinned here.
        var normal = new StageRow { ServiceFamily = DistributionFamily.Normal, ServiceStdDev = "2" };
        var gamma = new StageRow { ServiceFamily = DistributionFamily.Gamma, ServiceShape = "2" };

        var normalSpec = ConfigPanelViewModel.BuildSpec(normal, rate: 0.5, unit: TimeUnit.Hours);
        var gammaSpec = ConfigPanelViewModel.BuildSpec(gamma, rate: 0.5, unit: TimeUnit.Hours);

        Assert.NotNull(normalSpec);
        Assert.NotNull(gammaSpec);
        Assert.Equal(120.0, normalSpec!.StdDev!.Value, 9);
        Assert.Equal(2.0, gammaSpec!.Shape!.Value, 9);
        Assert.Equal(2.0, gammaSpec.Mean, 9);
    }

    [Fact]
    public void BuildSpec_InHours_UniformRefusalTalksInMinutes()
    {
        // Why it matters: the refusal compares the half-width with the mean, and both
        // are minutes by then. Reporting the typed hours value in that sentence would
        // make a correct refusal read like an arithmetic mistake.
        var row = new StageRow { ServiceFamily = DistributionFamily.Uniform, ServiceSpread = "0.05" };

        // μ = 0.5/min → mean 2 min; 0.05 h = 3 min, which exceeds the mean.
        var spec = ConfigPanelViewModel.BuildSpec(row, rate: 0.5, unit: TimeUnit.Hours);

        Assert.Null(spec);
        Assert.Contains("3 min", row.InlineError!, StringComparison.Ordinal);
        Assert.Contains("2 min", row.InlineError!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0.05", row.InlineError!);
    }

    [Fact]
    public void ManualMuInHours_ReachesParametersAsPerMinute()
    {
        // Why it matters: the μ mirror of the λ conversion. Before 9P the per-stage
        // field ignored the unit, so an hour-shaped form produced a run 60× too fast
        // with nothing on screen to say so.
        var vm = new ConfigPanelViewModel { TimeUnit = TimeUnit.Hours, SourceMode = DataSourceMode.EnterManually };
        vm.ManualLambda.Value = "60"; // one patient per hour
        foreach (var row in vm.StageRows)
        {
            row.MuValue = "60"; // one service per hour
        }

        var parameters = vm.TryBuildRunParameters();

        Assert.NotNull(parameters);
        Assert.Equal(1.0, parameters!.ManualArrivalRate!.Value, 12);
        Assert.All(parameters.ManualServiceRates, rate => Assert.Equal(1.0, rate!.Value, 12));
    }

    // ── fixtures ────────────────────────────────────────────────────────

    /// <summary>A panel with the three-stage sample loaded and analysed.</summary>
    private static ConfigPanelViewModel LoadedPanel()
    {
        var vm = new ConfigPanelViewModel();
        vm.ApplyLoadedFile(Sample("sample_3stage_variable.csv"));
        Assert.True(vm.Binding?.IsUsable == true, "the three-stage sample must analyse cleanly");
        return vm;
    }

    /// <summary>
    /// A fully valid manual configuration. The comma list is deliberately left
    /// blank: in manual mode it is hidden, and a test that filled it would be able
    /// to pass through that field instead of the per-stage one.
    /// </summary>
    private static ConfigPanelViewModel ManualPanel(int stages = 2)
    {
        var vm = new ConfigPanelViewModel { SourceMode = DataSourceMode.EnterManually };
        vm.StageCount.Value = stages.ToString();
        vm.ManualLambda.Value = "0.5";
        foreach (var row in vm.StageRows)
        {
            row.MuValue = "1.0";
        }

        return vm;
    }

    private static double[] FittedRates(ConfigPanelViewModel vm) =>
        vm.Binding!.FittedServiceRates.ToArray();

    /// <summary>
    /// The topology the run would build for this panel, assembled from the
    /// parameters the panel produced and the routing the caller states. The exit
    /// stage index is written out (index 1 of three — Screening) so a wrong routing
    /// rule shows up as a ρ mismatch instead of being mirrored by the expectation.
    /// </summary>
    private static NetworkTopology ExpectedTopology(
        ConfigPanelViewModel vm, double exitProbability, double bypassProbability)
    {
        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);

        var specs = parameters!.StageNames
            .Select((name, i) =>
            {
                double mu = parameters.ManualServiceRates[i] ?? FittedRates(vm)[i];
                return new StageSpec(name, parameters.ServerCounts[i], mu, parameters.ServiceFamilies[i]);
            })
            .ToList();

        Assert.Equal(3, specs.Count);
        int exitStage = exitProbability > 0 ? 1 : -1;
        int bypassStage = bypassProbability > 0 ? 1 : -1;
        int bypassDestination = bypassProbability > 0 ? 2 : -1;

        return new NetworkTopology(
            parameters.ManualArrivalRate!.Value,
            specs,
            exitStage,
            exitProbability,
            bypassProbability,
            bypassStage,
            bypassDestination);
    }

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
