using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using LiveChartsCore.SkiaSharpView.Avalonia;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.2 gate tests (feat/milestone-6c-input-analysis-charts): the input
/// histogram + fitted PDF overlay cards. The pure service must reuse the exact
/// bins the chi-square verdict was computed from — the histogram and the
/// goodness-of-fit table can never disagree — and scale the fitted density by
/// bin-width × N, with the bin widths varying because chi-square uses
/// equal-probability bins. The view model must build real chart controls on the
/// UI thread and stay in sync with the config's upload, reset, and distribution
/// choices.
/// </summary>
public class Phase6c2HistogramTests
{
    private const string SampleCsv = "sample_patients.csv";

    [Fact]
    public void FitAll_MirrorsRunFits_ForUsableBinding()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));

        var reports = InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05);

        Assert.Equal(2, reports.Count); // inter-arrival + the sample's single Screening stage
        Assert.Equal("Inter-arrival", reports[0].Label);
        Assert.Equal("Screening service", reports[1].Label);
        Assert.All(reports, r => Assert.NotNull(r.Fitted));
        Assert.All(reports, r => Assert.NotNull(r.ChiSquare));
    }

    [Fact]
    public void FitAll_EmptyForUnusableBinding()
    {
        Assert.Empty(InputAnalysisService.FitAll(null, "Exponential", "Exponential", 0.05));
        Assert.Empty(InputAnalysisService.FitAll(
            new DataBindingResult(
                null, null, Array.Empty<OpdSimulator.Data.Validation.ValidationIssue>(), null,
                null, Array.Empty<string>(), Array.Empty<double>(), null, 0, 0, 0,
                Array.Empty<double>(),
                new Dictionary<string, IReadOnlyList<double>>()),
            "Exponential", "Exponential", 0.05));
    }

    [Fact]
    public void BuildHistogram_ReusesTheChiSquareBins_NeverRecomputes()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));
        var fit = InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05)[0];
        var chi = fit.ChiSquare!;

        var chart = InputAnalysisService.BuildHistogram(fit);

        Assert.True(chart.HasSeries, "the fitted series must produce a drawable histogram");
        Assert.Equal(chi.BinEdges.Count - 1, chart.Categories.Count);
        Assert.Equal(chi.BinEdges.Count - 1, chart.Observed.Count);
        for (int i = 0; i < chi.Observed.Count; i++)
        {
            Assert.Equal(chi.Observed[i], chart.Observed[i]);
        }
    }

    [Fact]
    public void BuildHistogram_FittedPdfIsDensityTimesBinWidthTimesN()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));
        var fit = InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05)[0];
        var chi = fit.ChiSquare!;
        var edges = chi.BinEdges;
        double n = fit.Samples.Count;
        var distribution = fit.Fitted!.Distribution;

        var chart = InputAnalysisService.BuildHistogram(fit);

        Assert.Equal(edges.Count - 1, chart.FittedPdf.Count);
        for (int i = 0; i < edges.Count - 1; i++)
        {
            double expected = distribution.Density((edges[i] + edges[i + 1]) / 2) * (edges[i + 1] - edges[i]) * n;
            Assert.Equal(expected, chart.FittedPdf[i], 8);
        }
    }

    [Fact]
    public void BuildHistogram_BinLabelsAndCaptionDescribeFitAndVerdict()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));
        var fit = InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05)[0];

        var chart = InputAnalysisService.BuildHistogram(fit);

        Assert.Matches(@"^\[[0-9.]+", chart.Categories[0]);
        Assert.Contains(fit.Fitted!.Name, chart.Caption);
        Assert.Contains("χ²", chart.Caption);
        Assert.Contains(fit.ChiSquare!.Decision, chart.Caption);
    }

    [Fact]
    public void BuildHistogram_FailedFitYieldsSerieslessCard()
    {
        var failed = new FitReport("Idle service", new[] { 0.0, 0.0 }, null, null);

        var chart = InputAnalysisService.BuildHistogram(failed);

        Assert.False(chart.HasSeries);
        Assert.Null(chart.Caption);
        Assert.Empty(chart.Categories);
        Assert.Equal("Idle service time (minutes)", chart.Title);
    }

    [AvaloniaFact]
    public void ApplyPrepared_BuildsChartCardsWithLiveChartsContent()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));
        var vm = new InputAnalysisViewModel();

        vm.ApplyPrepared(Prepare(binding));

        Assert.False(vm.IsEmpty);
        Assert.Equal(4, vm.Charts.Count); // histogram + chi-square card per fit (Inter-arrival, Screening)
        Assert.StartsWith("Inter-arrival", vm.Charts[0].Title);
        Assert.Equal("Chi-square: Inter-arrival", vm.Charts[1].Title);
        Assert.True(vm.Charts[0].HasSeries);
        Assert.False(vm.Charts[0].ShowEmptyState);
        var chart = Assert.IsType<CartesianChart>(vm.Charts[0].ChartContent);
        Assert.Equal(2, chart.Series.Count()); // observed columns + fitted line overlay
    }

    [AvaloniaFact]
    public void Apply_WithNoBinding_ClearsToEmptyState()
    {
        var vm = new InputAnalysisViewModel();

        vm.Apply(null, "Exponential", "Exponential", 0.05);

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Charts);
    }

    [Fact]
    public void ConfigPanelViewModel_DataBindingChanged_FiresOnLoadAndReset()
    {
        var config = new ConfigPanelViewModel();
        int raises = 0;
        config.DataBindingChanged += (_, _) => raises++;

        config.ApplyLoadedFile(SamplePath(SampleCsv));
        Assert.Equal(1, raises);

        config.ResetToDefaults();
        Assert.Equal(2, raises);
    }

    [AvaloniaFact]
    public async Task MainViewModel_UploadResetAndDistributionChange_StayInSync()
    {
        var main = new MainViewModel();
        Assert.True(main.InputAnalysis.IsEmpty, "fresh window: input analysis is empty");

        main.Config.ApplyLoadedFile(SamplePath(SampleCsv));
        Assert.True(await WaitForAsync(() => main.InputAnalysis.Charts.Count == 4),
            "loading a usable file must populate the histogram + chi-square cards for Inter-arrival and Screening");
        Assert.False(main.InputAnalysis.IsEmpty);

        main.Config.InterArrivalDistribution = "Lognormal";
        Assert.True(await WaitForAsync(() => main.InputAnalysis.Charts.Count > 0
            && main.InputAnalysis.Charts[0].Caption?.Contains("Lognormal") == true),
            "switching the inter-arrival distribution must rebuild the cards for the new family");

        main.Config.ResetToDefaults();
        Assert.True(await WaitForAsync(() => main.InputAnalysis.IsEmpty),
            "clear-all must drop the input-analysis charts back to the empty state");
    }

    private static IReadOnlyList<IInputChartData> Prepare(DataBindingResult binding)
    {
        var charts = new System.Collections.Generic.List<IInputChartData>();
        foreach (var fit in InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05))
        {
            charts.Add(InputAnalysisService.BuildHistogram(fit));
            if (fit.ChiSquare is not null)
            {
                charts.Add(InputAnalysisService.BuildChiSquareChart(fit));
            }
        }
        return charts;
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }

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
}