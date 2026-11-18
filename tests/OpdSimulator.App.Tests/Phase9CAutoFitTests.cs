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
