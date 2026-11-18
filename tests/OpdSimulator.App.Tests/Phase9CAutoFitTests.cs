using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Intent tests for Phase 9C's family decision (D-231). The service must pick
/// the same family the existing fitter would, keep the file's stage order, and
/// refuse short samples rather than guessing a shape.
/// </summary>
public class Phase9CAutoFitTests
{
    private const int Seed = 42;
    private const int SampleCount = 5_000;

    [Fact]
    public void Fit_LognormalSamples_WinsLognormal()
    {
        var samples = Draw(new DistributionSpec(DistributionFamily.Lognormal, 5.0, StdDev: 1.5), SampleCount);
        var binding = Binding(samples, new[] { "Reception" }, new Dictionary<string, IReadOnlyList<double>>());

        var report = AutoFitService.Fit(binding, 0.05);

        var best = Assert.IsType<DistributionFitResult>(report.InterArrival.Best);
        Assert.Equal(DistributionFamily.Lognormal, best.Family);
        Assert.Equal(best.Aic, report.InterArrival.AllCandidates.Where(c => !c.Rejected).Min(c => c.Aic));
    }

    [Fact]
    public void Fit_InsufficientSamples_ReturnsNoWinnerAndNamesTheReason()
    {
        var binding = Binding(Draw(new DistributionSpec(DistributionFamily.Exponential, 4.0), 10),
            new[] { "Reception" }, new Dictionary<string, IReadOnlyList<double>>());

        var report = AutoFitService.Fit(binding, 0.05);

        Assert.Null(report.InterArrival.Best);
        Assert.All(report.InterArrival.AllCandidates,
            c => Assert.Contains("insufficient samples", c.RejectionReason ?? string.Empty));
    }

    [Fact]
    public void Fit_StageOrderAndSamples_FollowTheFile()
    {
        var arrival = Draw(new DistributionSpec(DistributionFamily.Exponential, 4.0), SampleCount);
        var gamma = Draw(new DistributionSpec(DistributionFamily.Gamma, 3.0, Shape: 2.0, Scale: 1.5), SampleCount);
        var normal = Draw(new DistributionSpec(DistributionFamily.Normal, 10.0, StdDev: 2.0), SampleCount);
        var byStage = new Dictionary<string, IReadOnlyList<double>>
        {
            ["Doctor"] = gamma,
            ["Reception"] = normal,
        };
        var binding = Binding(arrival, new[] { "Doctor", "Reception" }, byStage);

        var report = AutoFitService.Fit(binding, 0.05);

        Assert.Equal(new[] { "Doctor", "Reception" }, report.Stages.Select(s => s.StageName));
        Assert.Equal(DistributionFamily.Gamma, Assert.IsType<DistributionFitResult>(report.Stages[0].Fit.Best).Family);
        Assert.Equal(DistributionFamily.Normal, Assert.IsType<DistributionFitResult>(report.Stages[1].Fit.Best).Family);
    }

    [Fact]
    public void Fit_SameSamplesAndAlpha_AreDeterministic()
    {
        var samples = Draw(new DistributionSpec(DistributionFamily.Lognormal, 5.0, StdDev: 1.5), 1_000);
        var binding = Binding(samples, new[] { "Reception" }, new Dictionary<string, IReadOnlyList<double>>());

        var first = AutoFitService.Fit(binding, 0.05);
        var second = AutoFitService.Fit(binding, 0.05);

        Assert.Equal(first.InterArrival.Best!.Family, second.InterArrival.Best!.Family);
        Assert.Equal(first.InterArrival.Best!.Aic, second.InterArrival.Best!.Aic);
    }

    [Fact]
    public void LoadFile_WritesTheBestFitFamily_IntoTheRunParameters()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");

        var report = AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun);
        config.ApplyAutoFit(report);

        var parameters = Assert.IsType<SimulationParameters>(config.TryBuildRunParameters());
        foreach (var winner in report.Stages.Where(s => s.Fit.Best is not null))
        {
            int index = config.StageRows.ToList().FindIndex(r =>
                string.Equals(r.StageName, winner.StageName, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(winner.Fit.Best!.Family, parameters.ServiceFamilies[index].Family);
            Assert.Equal(FieldProvenance.AutoFitted, config.StageRows[index].FamilyProvenance);
            Assert.Contains(winner.Fit.Best!.Family.ToString(), config.StageRows[index].FitDisclosure!);
        }
    }

    [Fact]
    public void LoadFile_WritesTheBestFitArrivalFamily_AndRefreshesRowNotation()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");

        var report = AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun);
        config.ApplyAutoFit(report);

        var family = report.InterArrival.Best?.Family;
        if (family is null)
        {
            Assert.Contains("Could not choose", config.ArrivalFitDisclosure!);
            return;
        }

        Assert.Equal(family.ToString(), config.InterArrivalDistribution);
        Assert.Equal(family, config.ArrivalFamily);
        Assert.All(config.StageRows, row => Assert.Equal(family, row.ArrivalFamily));
        var parameters = Assert.IsType<SimulationParameters>(config.TryBuildRunParameters());
        Assert.Equal(family, parameters.ArrivalDistribution?.Family);
    }

    [Fact]
    public void LoadFile_CarriesWinnerSpread_ButNotItsMean()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        foreach (var row in config.StageRows)
        {
            row.MuValue = "0.3";
        }
        var before = config.StageRows.Select(r => r.MuValue).ToList();

        var report = AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun);
        config.ApplyAutoFit(report);

        Assert.Equal(before, config.StageRows.Select(r => r.MuValue));
    }

    [Fact]
    public void LoadPass_SetsIsAutoFitting_AndClearsItAfterTheApply()
    {
        var main = new MainViewModel();

        main.Config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_variable.csv"));

        Assert.True(main.Config.IsAutoFitting);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (main.Config.IsAutoFitting && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(main.Config.IsAutoFitting);
        Assert.DoesNotContain(main.Config.StageRows, r => string.IsNullOrEmpty(r.FitDisclosure));
    }

    [Fact]
    public void LoadPass_RefusalPath_ClearsIsAutoFitting()
    {
        var main = new MainViewModel();

        main.Config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_clinic.csv"));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (main.Config.IsAutoFitting && watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(main.Config.IsAutoFitting);
    }

    [Fact]
    public void ApplyAutoFit_SecondApply_WritesNothing()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        var report = AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun);

        config.ApplyAutoFit(report);
        int changes = config.StageRows.Sum(r => CountFamilyChanges(r, () => config.ApplyAutoFit(report)));

        Assert.Equal(0, changes);
    }

    private static int CountFamilyChanges(StageRow row, Action action)
    {
        int count = 0;
        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StageRow.ServiceFamily))
            {
                count++;
            }
        }
        row.PropertyChanged += Handler;
        try
        {
            action();
        }
        finally
        {
            row.PropertyChanged -= Handler;
        }
        return count;
    }

    // ---------- Phase 9C.6: routing probabilities disclose the effective value ----------

    [Fact]
    public void RoutingDisclosure_AlwaysNamesTheEffectiveValue()
    {
        var config = new ConfigPanelViewModel();
        Assert.Contains("default", config.PExitFitText!);

        config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_variable.csv"));

        Assert.NotNull(config.PExitFitText);
        Assert.Contains("uploaded data", config.PExitFitText!);
        Assert.NotNull(config.PBypassFitText);
    }

    [Fact]
    public void RoutingFields_AutoFillOnlyWhileTheParametersChannelIsLive()
    {
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_variable.csv"));

        Assert.True(string.IsNullOrEmpty(config.PExit.Value));

        config.ParametersIsOptionalEnabled = true;

        Assert.False(string.IsNullOrEmpty(config.PExit.Value));
        Assert.Contains("uploaded data", config.PExitFitText!);
    }

    [Fact]
    public void RoutingOverride_WarnsNamesTheFit_AndSurvivesTheNextLoad()
    {
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_variable.csv"));
        config.ParametersIsOptionalEnabled = true;
        double fitted = double.Parse(config.PExit.Value!, System.Globalization.CultureInfo.InvariantCulture);

        config.PExit.Value = "0.9";

        Assert.NotNull(config.PExitOverrideWarning);
        Assert.Contains(fitted.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            config.PExitOverrideWarning!);

        config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_bypass.csv"));

        Assert.Equal("0.9", config.PExit.Value);
        Assert.NotNull(config.PExitOverrideWarning);
        Assert.Contains("deviates", config.PExitOverrideWarning!);
    }

    // ---------- Phase 9C.5: provenance moves on load, μ stays with 9P ----------

    [Fact]
    public void NewLoad_DemotesPreviousFitProvenance_ButKeepsTheOwnerOverride()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        var fitted = config.StageRows[0];
        fitted.MuProvenance = MuProvenance.Fitted;
        var user = config.StageRows[1];
        user.MuValue = "9";
        Assert.Equal(MuProvenance.UserSet, user.MuProvenance);

        config.ApplyLoadedFile(Path.Combine(FindRepoRoot(AppContext.BaseDirectory),
            "samples", "sample_3stage_clinic.csv"));

        Assert.Equal(MuProvenance.Default, fitted.MuProvenance);
        Assert.Equal(MuProvenance.UserSet, user.MuProvenance);
        Assert.NotNull(user.MuOverrideWarning);
        Assert.Contains("deviates", user.MuOverrideWarning!);
    }

    [Fact]
    public void MuProvenance_StillDecidesTheValueThatReachesTheRun()
    {
        var config = FittedConfig();
        var user = config.StageRows[0];
        user.MuValue = "0.42";

        var parameters = Assert.IsType<SimulationParameters>(config.TryBuildRunParameters());

        // The owner's typed μ is the one placed in ManualServiceRates; the other
        // stages keep null there so 9P's resolver takes the data's fit. 9C never
        // writes μ itself — it only moves the provenance that decides this.
        Assert.Equal(ToPerMinuteTest(0.42, config), parameters.ManualServiceRates[0]);
        Assert.All(parameters.ManualServiceRates.Skip(1), r => Assert.Null(r));
    }

    /// <summary>Mode/unit conversion mirrored here so the assertion names the expectation.</summary>
    private static double ToPerMinuteTest(double value, ConfigPanelViewModel config)
    {
        if (config.IsMeanWise)
        {
            value = 1.0 / value;
        }
        return config.TimeUnit switch
        {
            TimeUnit.Minutes => value,
            TimeUnit.Seconds => value * 60.0,
            TimeUnit.Hours => value / 60.0,
            _ => value,
        };
    }

    // ---------- Phase 9C.8: Input Analysis caption follows the run, names the fit ----------

    [Fact]
    public void HistogramCaption_NoOverride_NamesOnlyTheRunFamily()
    {
        var fit = InputAnalysisService.FitAll(BindingForCaption(), "Gamma",
            TestStageFamilies.AllExponential(BindingForCaption()!), 0.05)[0];

        var chart = InputAnalysisService.BuildHistogram(fit, new DataFitCaption("Gamma", 412));

        Assert.Contains("Gamma", chart.Caption!);
        Assert.DoesNotContain("data's best fit", chart.Caption!);
    }

    [Fact]
    public void HistogramCaption_OnOverride_NamesDataWinnerAfterTheRunFamily()
    {
        var binding = BindingForCaption();
        var fit = InputAnalysisService.FitAll(binding, "Exponential",
            TestStageFamilies.AllExponential(binding!), 0.05)[0];

        var chart = InputAnalysisService.BuildHistogram(fit, new DataFitCaption("Lognormal", 412));

        Assert.Contains("Exponential", chart.Caption!);
        Assert.Contains("data's best fit: Lognormal (AIC 412)", chart.Caption!);
        Assert.True(chart.Caption!.IndexOf("Exponential", StringComparison.Ordinal)
                    < chart.Caption.IndexOf("data's best fit", StringComparison.Ordinal),
            "the run family must come before the data winner");
    }

    [Fact]
    public void DataFitCaptions_FromConfig_NameTheWinner_EvenAfterAnOverride()
    {
        var config = FittedConfig();
        var winners = config.CurrentDataFitCaptions;

        Assert.NotNull(winners.InterArrival);
        var row = config.StageRows.First(r => r.BestFitFamily is not null);
        Assert.True(winners.Stages.ContainsKey(row.StageName));
        Assert.Equal(row.BestFitFamily!.Value.ToString(), winners.Stages[row.StageName].Family);

        row.ApplyServiceFamily(Enum.GetValues<DistributionFamily>().First(f => f != row.BestFitFamily));

        // The winner describes the file, not the current selection — it must
        // survive the override so the caption can name both truths (D-235).
        Assert.Equal(row.BestFitFamily.Value.ToString(),
            config.CurrentDataFitCaptions.Stages[row.StageName].Family);
    }

    /// <summary>A committed binding whose arrival sample is long enough for a fit.</summary>
    private static DataBindingResult? BindingForCaption()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        return config.Binding;
    }

    // ---------- Phase 9C.4: disclosures and the one-click return to the fit ----------

    [Fact]
    public void OverrideArrivalFamily_WarningNamesTheDataWinner_AndResetRestoresIt()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        var report = AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun);
        config.ApplyAutoFit(report);
        var winner = Assert.IsType<DistributionFitResult>(report.InterArrival.Best);
        string winnerName = winner.Family.ToString();

        string chosen = Enum.GetValues<DistributionFamily>()
            .First(f => f != winner.Family).ToString();
        config.InterArrivalDistribution = chosen;

        Assert.NotNull(config.ArrivalOverrideWarning);
        Assert.Contains(winnerName, config.ArrivalOverrideWarning!);
        Assert.Contains(chosen, config.ArrivalOverrideWarning!);

        config.UseDataFitArrivalCommand.Execute(null);

        Assert.Equal(winnerName, config.InterArrivalDistribution);
        Assert.Null(config.ArrivalOverrideWarning);
    }

    [Fact]
    public void OverrideStageFamily_WarningNamesTheDataWinner_AndResetRestoresIt()
    {
        var config = FittedConfig();
        var row = config.StageRows.First(r => r.BestFitFamily is not null);
        var winner = row.BestFitFamily!.Value;

        var other = Enum.GetValues<DistributionFamily>().First(f => f != winner);
        row.ApplyServiceFamily(other);

        Assert.NotNull(row.FitOverrideWarning);
        Assert.Contains(winner.ToString(), row.FitOverrideWarning!);
        Assert.Contains(other.ToString(), row.FitOverrideWarning!);
        Assert.Equal(FieldProvenance.UserSet, row.FamilyProvenance);

        row.UseDataFitCommand.Execute(null);

        Assert.Equal(winner, row.ServiceFamily);
        Assert.Null(row.FitOverrideWarning);
        Assert.Equal(FieldProvenance.AutoFitted, row.FamilyProvenance);
        Assert.True(string.IsNullOrEmpty(row.MuValue));
    }

    [Fact]
    public void TypedMu_WarningNamesTheFittedValue_AndResetClearsIt()
    {
        var config = FittedConfig();
        var row = config.StageRows.First(r => r.FittedMuPerMinute is > 0);
        double fitted = row.FittedMuPerMinute!.Value;
        row.MuValue = (fitted * 2).ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(row.MuOverrideWarning);
        Assert.Contains(fitted.ToString("G4", System.Globalization.CultureInfo.InvariantCulture), row.MuOverrideWarning!);
        Assert.Equal(MuProvenance.UserSet, row.MuProvenance);

        row.UseDataFitCommand.Execute(null);

        Assert.True(string.IsNullOrEmpty(row.MuValue));
        Assert.Null(row.MuOverrideWarning);
        Assert.Equal(MuProvenance.Default, row.MuProvenance);
    }

    /// <summary>
    /// Loads the variable sample and applies its fit synchronously, so the warning
    /// assertions do not race the background pass. The reset tests exercise the
    /// same view-model state the load pass produces.
    /// </summary>
    private static ConfigPanelViewModel FittedConfig()
    {
        var config = LoadedConfig("sample_3stage_variable.csv");
        config.ApplyAutoFit(AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun));
        return config;
    }

    /// <summary>Loads a committed sample through the same path the GUI uses.</summary>
    private static ConfigPanelViewModel LoadedConfig(string file)
    {
        string root = FindRepoRoot(AppContext.BaseDirectory);
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Path.Combine(root, "samples", file));
        return config;
    }

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found from " + start);
    }

    /// <summary>Builds a usable binding whose sample lists are the only populated columns.</summary>
    private static DataBindingResult Binding(
        IReadOnlyList<double> arrivals,
        IReadOnlyList<string> stageNames,
        IReadOnlyDictionary<string, IReadOnlyList<double>> byStage) =>
        new("mem://auto-fit", null, Array.Empty<OpdSimulator.Data.Validation.ValidationIssue>(), null,
            1.0 / arrivals.Average(), stageNames, Enumerable.Repeat(0.5, stageNames.Count).ToList(),
            0.4, 0, 0, 0, arrivals, byStage);

    /// <summary>Draws deterministic samples from the Core sampler at the fixed seed.</summary>
    private static List<double> Draw(DistributionSpec spec, int count)
    {
        var sampler = DistributionSamplerFactory.Create(spec, new SeededRandomSource(Seed));
        var samples = new List<double>(count);
        for (int i = 0; i < count; i++)
            samples.Add(sampler.NextSample());
        return samples;
    }
}
