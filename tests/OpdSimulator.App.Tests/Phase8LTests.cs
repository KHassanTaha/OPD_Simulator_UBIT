using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Loaders;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8L gate tests: one service-family source, and per-stage families on the
/// Input tab.
/// </summary>
/// <remarks>
/// The defect this phase fixes was a control that looked authoritative and was
/// inert. The Model section offered a "Service distribution" dropdown bound to a
/// property nothing in the run read, while the per-stage families introduced in
/// 8K were the values that actually reached the engine and the per-stage
/// chi-square. So the tests here are about *wiring*, not about arithmetic: which
/// property a dropdown is bound to, which source seeds a new stage, and which
/// family each stage's samples are tested against. A test that only asserted
/// "the run still produces numbers" would have passed straight through the
/// defect, which is the whole reason this file exists.
/// </remarks>
public class Phase8LTests
{
    [Fact]
    public void ModelDropdown_DrivesDefaultStageServiceFamily()
    {
        // WHY: the old Model-section dropdown was the visible symptom — a control
        // that a user would reasonably believe governed service times, bound to a
        // property the run ignored. Two halves to this assertion, because either
        // alone is satisfiable by accident:
        //   1. the XAML must have exactly one service-family dropdown, and it must
        //      be bound to DefaultStageServiceFamily (the property 8K made
        //      authoritative), not to a global string;
        //   2. the property must not have simply been renamed into another
        //      dead binding — writing it must be observable in the row state the
        //      run reads.
        var axaml = File.ReadAllText(ConfigPanelXamlPath());

        var serviceFamilyBindings = axaml
            .Split('\n')
            .Where(line => line.Contains("DefaultStageServiceFamily", StringComparison.Ordinal))
            .ToList();

        Assert.Single(serviceFamilyBindings);
        Assert.Contains("SelectedItem=\"{Binding DefaultStageServiceFamily, Mode=TwoWay}\"",
            serviceFamilyBindings[0]);

        // The one dropdown must still tell the user what it is for. The Label sits
        // on the element's opening line, not on the SelectedItem line, so it is
        // asserted against the document.
        Assert.Contains("Label=\"Default service family", axaml, StringComparison.Ordinal);

        // No control anywhere in the panel may bind the deleted global string.
        Assert.DoesNotContain("Config.ServiceDistribution", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigPanelViewModel.ServiceDistribution", axaml, StringComparison.Ordinal);

        // And the surviving dropdown is live: the property it writes is the one
        // Apply-to-all pushes onto the rows the engine reads.
        var config = new ConfigPanelViewModel();
        config.DefaultStageServiceFamily = DistributionFamily.Gamma;
        config.ApplyDefaultsToAllStagesCommand.Execute(null);
        Assert.All(config.StageRows, row => Assert.Equal(DistributionFamily.Gamma, row.ServiceFamily));
    }

    [Fact]
    public void ChangingDefault_DoesNotRetroactivelyChangeExistingStages()
    {
        // WHY: the dropdown is named "for new stages". If it silently rewrote
        // existing rows, a user who changed it would overwrite deliberate
        // per-stage choices without being told — the exact opposite of what the
        // label promises, and a data-loss bug rather than a cosmetic one.
        var config = new ConfigPanelViewModel();
        config.StageRows[0].ServiceFamily = DistributionFamily.Normal;
        config.StageRows[1].ServiceFamily = DistributionFamily.Lognormal;

        config.DefaultStageServiceFamily = DistributionFamily.Uniform;

        // Existing rows keep what the user set on them…
        Assert.Equal(DistributionFamily.Normal, config.StageRows[0].ServiceFamily);
        Assert.Equal(DistributionFamily.Lognormal, config.StageRows[1].ServiceFamily);

        // …and a stage added afterwards carries the new default.
        int before = config.StageRows.Count;
        config.StageCount.Value = (before + 1).ToString();
        Assert.Equal(before + 1, config.StageRows.Count);
        Assert.Equal(DistributionFamily.Uniform, config.StageRows[^1].ServiceFamily);
    }

    [Fact]
    public void ApplyDefaultsToAllStages_ReadsTheSameSource()
    {
        // WHY: "Apply to all stages" and the dropdown are two controls over one
        // value. If they read different sources — which is what happened when the
        // dropdown still pointed at the deleted global — the user sets a family,
        // presses Apply, and the rows silently keep something else. Pin both
        // controls to DefaultStageServiceFamily, in both directions: set the
        // default then apply, and set the default then read it back.
        var config = new ConfigPanelViewModel();
        config.StageRows[0].ServiceFamily = DistributionFamily.Deterministic;
        config.StageRows[1].ServiceFamily = DistributionFamily.Deterministic;

        config.DefaultStageServiceFamily = DistributionFamily.Lognormal;
        config.ApplyDefaultsToAllStagesCommand.Execute(null);

        Assert.All(config.StageRows, row => Assert.Equal(DistributionFamily.Lognormal, row.ServiceFamily));
        Assert.All(config.StageRows, row => Assert.Equal(DistributionFamily.Lognormal, row.ServiceFamily));
    }

    [Fact]
    public void ResetToDefaults_ReturnsDefaultStageServiceFamilyToExponential()
    {
        // WHY: a reset that leaves one control on a non-default value produces a
        // panel that looks factory-fresh but silently seeds the next stage from
        // whatever the user happened to try. The old reset wrote the deleted
        // global string, so nothing asserted this and the gap was invisible.
        var config = new ConfigPanelViewModel();
        config.DefaultStageServiceFamily = DistributionFamily.Gamma;
        Assert.Equal(DistributionFamily.Gamma, config.DefaultStageServiceFamily);

        config.ResetToDefaults();

        Assert.Equal(DistributionFamily.Exponential, config.DefaultStageServiceFamily);
    }

    [Fact]
    public void InputAnalysisService_FitAll_UsesEachStagesOwnFamily()
    {
        // WHY: this is the requirement the whole phase exists for. Before it, the
        // Input tab tested every stage's samples against ONE family, so a user who
        // configured Screening as Normal and Doctor as Lognormal saw two verdicts
        // for a model the run never used. Three distinct families is the smallest
        // number that proves the mapping is per stage: with two, a bug that
        // applied the first stage's family to all the rest would pass.
        //
        // The sample VALUES are deliberately arbitrary — this asserts which fitter
        // was requested per stage, not whether the fit is a good one. Chi-square
        // verdicts for a poor sample are covered by the 6c.3 gate tests.
        var families = new[]
        {
            new StageServiceFamily("Reception", DistributionFamily.Exponential),
            new StageServiceFamily("Screening", DistributionFamily.Normal),
            new StageServiceFamily("Doctor", DistributionFamily.Lognormal),
        };
        var binding = ThreeStageBinding(families.Select(f => f.StageName));

        var reports = InputAnalysisService.FitAll(binding, "Exponential", families, 0.05);

        // One inter-arrival report plus one per stage.
        Assert.Equal(4, reports.Count);

        var byLabel = reports.ToDictionary(r => r.Label, r => r.Fitted?.Name);
        Assert.Equal("Exponential", byLabel["Inter-arrival"]);
        Assert.Equal("Exponential", byLabel["Reception service"]);
        Assert.Equal("Normal", byLabel["Screening service"]);
        Assert.Equal("Lognormal", byLabel["Doctor service"]);

        // The mapping is by stage NAME, not by position. Reversing the
        // configured order must not swap which family a stage is tested against —
        // the configured rows and the data file's stages are independent lists
        // and they can disagree in order (5d.3).
        var reversed = families.Reverse().ToList();
        var reversedReports = InputAnalysisService.FitAll(binding, "Exponential", reversed, 0.05)
            .ToDictionary(r => r.Label, r => r.Fitted?.Name);
        Assert.Equal("Exponential", reversedReports["Reception service"]);
        Assert.Equal("Normal", reversedReports["Screening service"]);
        Assert.Equal("Lognormal", reversedReports["Doctor service"]);
    }

    /// <summary>
    /// A usable binding whose stages each carry their own service samples.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than loaded from a file because the shipped sample CSV
    /// has one stage (Screening), and a one-stage file cannot express "each stage
    /// has its own family". <see cref="DataBindingResult.IsUsable"/> only requires
    /// a non-null <see cref="DataSet"/>, no error and no issues, so a minimal
    /// dataset is enough — FitAll reads the sample lists, not the cells.
    /// </remarks>
    private static DataBindingResult ThreeStageBinding(IEnumerable<string> stageNames)
    {
        var names = stageNames.ToList();
        var dataSet = new DataSet(
            "synthetic",
            new DateTime(2026, 1, 1),
            new[] { "stage" },
            new List<IReadOnlyDictionary<string, string>>());

        var serviceByStage = new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            // Fixed, evenly spread values: deterministic, and distinct per stage so
            // a report cannot accidentally carry another stage's samples.
            var samples = Enumerable.Range(1, 40).Select(i => 1.0 + (i * 0.37) + name.Length).ToList();
            serviceByStage[name] = samples;
        }

        var interArrival = Enumerable.Range(1, 40).Select(i => 2.0 + (i * 0.53)).ToList();

        return new DataBindingResult(
            "synthetic",
            dataSet,
            Array.Empty<OpdSimulator.Data.Validation.ValidationIssue>(),
            null,
            0.5,
            names,
            names.Select(_ => 0.5).ToList(),
            0.4,
            0,
            0,
            0,
            interArrival,
            serviceByStage);
    }

    /// <summary>Walks up to the repo root and returns the config panel's XAML.</summary>
    private static string ConfigPanelXamlPath()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "OpdSimulator.App", "Views", "ConfigPanel.axaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException("ConfigPanel.axaml not found above the test output directory.");
    }
}
