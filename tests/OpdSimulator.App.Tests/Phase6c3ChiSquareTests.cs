using System;
using System.Globalization;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 6c.3 gate tests (feat/milestone-6c-input-analysis-charts): the
/// chi-square observed-vs-expected paired-bar cards in the Input Analysis tab.
/// Content contracts only — grouped side-by-side rendering is exercised by
/// <see cref="Phase6c3Screenshot"/>. The cardinal rule: the chart must read the
/// very same bins/arrays the verdict was computed from, so the bars and the
/// ResultsPanel table can never disagree; every bin of the verdict appears
/// exactly once ("bin 1", "bin 2", …); the observed frequencies must account
/// for every sample; the caption must restate the table's line verbatim
/// (χ² value, df, p, Decision) so the verdict is readable without cross-checking
/// two widgets; and a fit with no chi-square yields an empty state instead of a
/// fabricated chart.
/// </summary>
public class Phase6c3ChiSquareTests
{
    private const string SampleCsv = "sample_patients.csv";

    [Fact]
    public void ChiSquareChart_ObservedAndExpected_HaveSameBinCount()
    {
        var fit = FitSample();

        var chart = InputAnalysisService.BuildChiSquareChart(fit);
        var chi = fit.ChiSquare!;

        int binCount = chi.BinEdges.Count - 1;
        Assert.True(chart.HasSeries);
        Assert.Equal(binCount, chart.Observed.Count);
        Assert.Equal(binCount, chart.Expected.Count);
        Assert.Equal(binCount, chart.Categories.Count);
        Assert.Equal(chi.Observed.Count, chart.Observed.Count);
        Assert.Equal("Chi-square: Inter-arrival", chart.Title);
    }

    [Fact]
    public void ChiSquareChart_ObservedSum_EqualsSampleSize()
    {
        var fit = FitSample();
        var chart = InputAnalysisService.BuildChiSquareChart(fit);

        Assert.Equal(fit.Samples.Count, chart.Observed.Sum());
    }

    [Fact]
    public void ChiSquareChart_Caption_MatchesResultsPanelFormat()
    {
        var fit = FitSample();
        var chi = fit.ChiSquare!;
        var chart = InputAnalysisService.BuildChiSquareChart(fit);
        var caption = Assert.IsType<string>(chart.Caption);

        string stat = chi.Statistic.ToString("0.###", CultureInfo.InvariantCulture);
        string p = chi.PValue.ToString("0.###", CultureInfo.InvariantCulture);

        Assert.Contains($"χ² = {stat},", caption);
        Assert.Contains($"df = {chi.DegreesOfFreedom},", caption);
        Assert.Contains($", p = {p} — {chi.Decision}", caption);
        Assert.Contains(chi.Decision, caption);
    }

    [Fact]
    public void ChiSquareChart_CategoriesFromBinEdges()
    {
        var fit = FitSample();
        var chart = InputAnalysisService.BuildChiSquareChart(fit);

        int binCount = fit.ChiSquare!.BinEdges.Count - 1;
        Assert.Equal(binCount, chart.Categories.Count);
        for (int i = 0; i < binCount; i++)
        {
            Assert.Equal($"bin {i + 1}", chart.Categories[i]);
        }
    }

    [Fact]
    public void InputAnalysisService_BuildChiSquareChart_RejectsNullFit()
    {
        var chart = InputAnalysisService.BuildChiSquareChart(new FitReport("Inter-arrival", Array.Empty<double>(), null, null));

        Assert.False(chart.HasSeries);
        Assert.Null(chart.Caption);
        Assert.Equal("Chi-square: Inter-arrival", chart.Title);
        Assert.Empty(chart.Observed);
        Assert.Empty(chart.Expected);
        Assert.Empty(chart.Categories);
    }

    private static FitReport FitSample()
    {
        var binding = DataAnalyzer.Analyze(SamplePath(SampleCsv));
        var fit = InputAnalysisService.FitAll(binding, "Exponential", "Exponential", 0.05)
            .Single(r => r.Label == "Inter-arrival");
        Assert.NotNull(fit.ChiSquare);
        return fit;
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