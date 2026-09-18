using System;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 5d gate — config-panel refinements (feat/gui-rebuild). Verification
/// intent: the Stages section is topology only (name + servers; the editable μ
/// field is gone, D-112) and each row shows a read-only μ-source label — fitted
/// "(from data)", manual "(manual)", or "— (no source)"; the Model section
/// carries the significance level α (strictly (0,1), D-113) which flows into
/// the chi-square verdicts and the results-panel caption; and loading data
/// whose detected stage count differs from the configured list raises an
/// amber warning with Sync / Keep actions (D-114). A run whose stage has no μ
/// source is refused with a banner naming that stage.
/// </summary>
public class Phase5dConfigTests
{
    private const string TwoStageHeader = "arrival_time,departure_stage,reception_start,reception_end,screening_start,screening_end";

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

    /// <summary>
    /// A two-stage CSV whose fitted p_exit = 0.5 (Screening + Doctor exits), so
    /// a three-stage config can be exercised past the ArrivalRate and p_exit
    /// refusal gates and down into the per-stage μ gate.
    /// </summary>
    private static string WriteTwoStageCsv()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opdsim-phase5d-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, string.Join('\n',
            TwoStageHeader,
            "8:17:00,Doctor,8:17:10,8:17:20,8:17:30,8:17:40",
            "8:18:00,Screening,8:18:10,8:18:20,8:18:30,8:18:40",
            "8:19:00,Doctor,8:19:10,8:19:20,8:19:30,8:19:40"));
        return path;
    }

    // ── 5d.1 · Stages are topology only ───────────────────────────────────

    [Fact]
    public void DefaultConfig_StagesShowNoSourceLabels_StartBlocked()
    {
        var vm = NewVm();

        Assert.Equal(3, vm.StageRows.Count);
        Assert.All(vm.StageRows, row =>
        {
            Assert.Equal("μ = — (no source)", row.ServiceRateLabel);
            Assert.False(row.HasErrors);
        });
        // D-128 supersedes 5d.1's "runnable in principle" contract: an empty
        // fit-mode config has no usable file, so Start is disabled and the
        // banner names the missing input.
        Assert.False(vm.StartIsEnabled);
        Assert.Contains("data file", vm.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StageRow_ShowsFittedMu_WhenDataLoaded()
    {
        var vm = NewVm();

        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));

        Assert.True(vm.StageRows[1].ServiceRateLabel.EndsWith("(from data)", StringComparison.Ordinal),
            "the Screening stage (single stage in the sample) must read its μ from the fitted rate");
        Assert.Equal("μ = — (no source)", vm.StageRows[0].ServiceRateLabel);
    }

    [Fact]
    public void StageRow_ShowsManualMu_OnlyWhileParametersOn()
    {
        var vm = NewVm();
        vm.ManualMuPerStage.Value = "0.8, 0.5, 0.4";

        // Parameters OFF → the comma list is "not supplied" (D-103).
        Assert.All(vm.StageRows, row =>
            Assert.Equal("μ = — (no source)", row.ServiceRateLabel));

        vm.ParametersIsOptionalEnabled = true;

        Assert.All(vm.StageRows, row =>
            Assert.EndsWith("(manual)", row.ServiceRateLabel, StringComparison.Ordinal));
        Assert.StartsWith("μ = 0.8", vm.StageRows[0].ServiceRateLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_Refused_WhenMuSourceMissingAndParametersOff()
    {
        // Defence in depth: the GUI's Start gating (7C.6, D-128) prevents
        // reaching this path from the UI, but the coordinator still refuses
        // cleanly if called directly (CLI, tests, future callers).
        // Two-stage data supplies λ and p_exit, but the configured third stage
        // (Doctor) has no fitted rate and Parameters is OFF — no manual entry.
        var path = WriteTwoStageCsv();
        try
        {
            var vm = NewVm();
            var binding = DataAnalyzer.Analyze(path);
            Assert.True(binding.IsUsable);
            Assert.True(binding.FittedExitProbability < 1.0, "fixture must pass the p_exit gate");

            var outcome = SimulationCoordinator.Run(vm.TryBuildRunParameters()!, binding);

            Assert.Null(outcome.Result);
            Assert.Equal(string.Format(SimulationCoordinator.MissingServiceRateMessage, "Doctor"), outcome.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── 5d.2 · Significance level α ───────────────────────────────────────

    [Fact]
    public void SignificanceLevel_Default_IsPointZeroFive()
    {
        var vm = NewVm();

        Assert.Equal("0.05", vm.SignificanceLevel.Value);
        Assert.False(vm.SignificanceLevel.HasError);
        Assert.Equal(0.05, vm.SignificanceLevelForRun, precision: 5);
        // D-128: α is valid, but the empty fit-mode config is still incomplete.
        Assert.False(vm.StartIsEnabled);
    }

    [Fact]
    public void SignificanceLevel_InvalidZero_Rejected()
    {
        var vm = NewVm();
        vm.SignificanceLevel.Value = "0";
        vm.ValidateSignificanceLevel();

        Assert.True(vm.SignificanceLevel.HasError);
        Assert.Equal("Significance level must be strictly between 0 and 1. You entered 0.", vm.SignificanceLevel.ErrorMessage);
        Assert.False(vm.StartIsEnabled, "α = 0 is outside the strict (0,1) contract and must block Start");
        Assert.Equal(FitsService.DefaultAlpha, vm.SignificanceLevelForRun, precision: 5);
    }

    [Fact]
    public void SignificanceLevel_InvalidOneAndNonNumeric_Rejected()
    {
        var vm = NewVm();

        vm.SignificanceLevel.Value = "1";
        vm.ValidateSignificanceLevel();
        Assert.StartsWith("Significance level must be strictly between 0 and 1.", vm.SignificanceLevel.ErrorMessage);
        Assert.False(vm.StartIsEnabled);

        vm.SignificanceLevel.Value = "not-a-number";
        vm.ValidateSignificanceLevel();
        Assert.Equal("Enter a number between 0 and 1.", vm.SignificanceLevel.ErrorMessage);
        Assert.False(vm.StartIsEnabled);
    }

    [Fact]
    public void SignificanceLevel_FlowsIntoChiSquareVerdicts()
    {
        // A 120-sample series guarantees a non-degenerate chi-square verdict
        // (≈11 bins, expected count ≈ 11 ≥ 1) — this is the pipeline that would
        // decay to the hard-coded 0.05 if the α plumbing broke.
        var samples = Enumerable.Range(0, 120)
            .Select(i => 1.0 + (i % 10) * 0.5)
            .ToArray();

        var atCustom = FitsService.Fit("synthetic", samples, "Exponential", 0.01).ChiSquare;
        var atDefault = FitsService.Fit("synthetic", samples, "Exponential").ChiSquare;

        Assert.NotNull(atCustom);
        Assert.Equal(0.01, atCustom!.Alpha, precision: 5);
        Assert.Equal(FitsService.DefaultAlpha, atDefault!.Alpha, precision: 5);
    }

    [Fact]
    public void SimulationCoordinator_ThreadsAlpha_ThroughDataBatch()
    {
        var vm = NewVm();
        vm.SignificanceLevel.Value = "0.01";
        vm.ValidateSignificanceLevel();
        var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));

        var outcome = SimulationCoordinator.Run(vm.TryBuildRunParameters()!, binding,
            significanceLevel: vm.SignificanceLevelForRun);

        Assert.NotEmpty(outcome.Fits);
        Assert.All(outcome.Fits.Where(f => f.ChiSquare is not null), f =>
            Assert.Equal(0.01, f.ChiSquare!.Alpha, precision: 5));
    }

    [Fact]
    public void ChiSquareCaption_ReflectsSignificanceLevel()
    {
        var results = new ResultsPanelViewModel();

        Assert.Equal("Chi-square goodness-of-fit (α = 0.05)", results.ChiSquareCaption);

        results.SetChiSquareAlpha(0.01);
        Assert.Equal("Chi-square goodness-of-fit (α = 0.01)", results.ChiSquareCaption);

        results.Reset();
        Assert.Equal("Chi-square goodness-of-fit (α = 0.05)", results.ChiSquareCaption);
    }

    // ── 5d.3 · Stage-count mismatch warning ───────────────────────────────

    [Fact]
    public void StageCount_Match_NoWarning()
    {
        var vm = NewVm();
        vm.StageCount.Value = "1";
        Assert.Single(vm.StageRows);

        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));

        Assert.False(vm.IsStageMismatchWarningVisible);
        Assert.Equal("", vm.StageMismatchMessage);
    }

    [Fact]
    public void StageCount_DataHasFewerStages_ShowsWarning()
    {
        var vm = NewVm();
        Assert.Equal(3, vm.StageRows.Count);

        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));

        Assert.True(vm.IsStageMismatchWarningVisible);
        Assert.Contains("1 stage(s)", vm.StageMismatchMessage);
        Assert.Contains("3", vm.StageMismatchMessage);
        Assert.Contains("Screening", vm.StageMismatchMessage);
        Assert.Contains("no service rate", vm.StageMismatchMessage);
    }

    [Fact]
    public void StageCount_DataHasMoreStages_ShowsWarning()
    {
        var path = WriteTwoStageCsv();
        try
        {
            var vm = NewVm();
            vm.StageCount.Value = "1";

            vm.ApplyLoadedFile(path);

            Assert.True(vm.IsStageMismatchWarningVisible);
            Assert.Contains("2 stage(s)", vm.StageMismatchMessage);
            Assert.Contains("Extra stages in the data will be ignored.", vm.StageMismatchMessage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SyncStages_ResizesRows_AndPopulatesDataNames()
    {
        var vm = NewVm();
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        Assert.True(vm.IsStageMismatchWarningVisible);

        vm.SyncStagesToData();

        Assert.Single(vm.StageRows);
        Assert.Equal("Screening", vm.StageRows[0].StageName);
        Assert.Equal("1", vm.StageCount.Value);
        Assert.False(vm.IsStageMismatchWarningVisible);
        Assert.StartsWith("μ = ", vm.StageRows[0].ServiceRateLabel);
        Assert.EndsWith("(from data)", vm.StageRows[0].ServiceRateLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncCommand_RequestsConfirmation_WithoutResyncing()
    {
        var vm = NewVm();
        var requested = 0;
        vm.SyncStagesRequested += (_, _) => requested++;

        vm.SyncStagesFromDataCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.Equal(3, vm.StageRows.Count);
    }

    [Fact]
    public void KeepCurrentStages_DismissesWarning_KeepsRows()
    {
        var vm = NewVm();
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        Assert.True(vm.IsStageMismatchWarningVisible);

        var requested = 0;
        vm.KeepStageMismatchRequested += (_, _) => requested++;
        vm.KeepCurrentStagesCommand.Execute(null);

        Assert.Equal(1, requested);
        vm.DismissStageMismatchWarning();

        Assert.False(vm.IsStageMismatchWarningVisible);
        Assert.Equal("", vm.StageMismatchMessage);
        Assert.Equal(3, vm.StageRows.Count);
    }
}