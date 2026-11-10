namespace OpdSimulator.App.Tests;

using System.IO;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;
using Xunit;

/// <summary>
/// Phase 8W: fitting intervals are a scale choice, so the tests pin three
/// things at once — the fitted parameters move to the chosen unit, the fit
/// VERDICT does not (scale-equivariance), and the Input tab's λ readout states
/// the chosen unit without changing the per-minute value the run consumes.
/// </summary>
public class Phase8WFittingUnitTests
{
    private const double MeanInterArrivalMinutes = 4.0;
    private const double MeanServiceMinutes = 3.0;

    [Fact]
    public void FitAll_InHours_ScalesParametersAndLeavesTheVerdictIdentical()
    {
        var binding = Binding();

        var minutes = InputAnalysisService.FitAll(
            binding,
            "Exponential",
            new[] { new StageServiceFamily("Screening", DistributionFamily.Exponential) },
            0.05,
            FittingTimeUnit.Minutes);

        var hours = InputAnalysisService.FitAll(
            binding,
            "Exponential",
            new[] { new StageServiceFamily("Screening", DistributionFamily.Exponential) },
            0.05,
            FittingTimeUnit.Hours);

        Assert.Equal(2, hours.Count);
        Assert.All(hours, report => Assert.Equal(FittingTimeUnit.Hours, report.Unit));

        var ia = Assert.Single(hours, report => report.Label == InputAnalysisService.InterArrivalSeriesLabel);
        var service = Assert.Single(hours, report => report.Label == "Screening service");

        // 4-minute gaps are 1/15 hour apart → 15 arrivals per hour.
        Assert.Equal(15.0, ia.Fitted!.Parameters["rate"], 9);
        // 3-minute service is 1/20 hour → 20 services per hour.
        Assert.Equal(20.0, service.Fitted!.Parameters["rate"], 9);

        // Scale-equivariance: same χ² statistic and p-value, so the verdict a
        // user reads cannot flip just because they changed the reporting unit.
        var iaMinutes = Assert.Single(
            minutes,
            report => report.Label == InputAnalysisService.InterArrivalSeriesLabel);
        Assert.Equal(iaMinutes.ChiSquare!.Statistic, ia.ChiSquare!.Statistic, 9);
        Assert.Equal(iaMinutes.ChiSquare.PValue, ia.ChiSquare.PValue, 12);
        Assert.Equal(iaMinutes.ChiSquare.Decision, ia.ChiSquare.Decision);
    }

    [Fact]
    public void HistogramTitlesAndCaptions_StateTheChosenUnit()
    {
        var hours = InputAnalysisService.FitAll(
            Binding(),
            "Exponential",
            new[] { new StageServiceFamily("Screening", DistributionFamily.Exponential) },
            0.05,
            FittingTimeUnit.Hours);

        var ia = Assert.Single(hours, report => report.Label == InputAnalysisService.InterArrivalSeriesLabel);
        var card = InputAnalysisService.BuildHistogram(ia);

        Assert.Equal("Inter-arrival time (hours)", card.Title);
        Assert.DoesNotContain("minutes", card.Caption!);

        // Bin edges are rebuilt from the scaled samples, so a 4-minute gap sits
        // near 0.067 on the x-axis rather than near 4 under a "minutes" heading.
        Assert.Contains(card.Categories, label => label.Contains(", 0."));
    }

    [Fact]
    public void HistogramCaptions_StateTheDayAndSessionDefinitions()
    {
        var days = InputAnalysisService.FitAll(
            Binding(),
            "Exponential",
            new[] { new StageServiceFamily("Screening", DistributionFamily.Exponential) },
            0.05,
            FittingTimeUnit.Days);
        var dayCard = InputAnalysisService.BuildHistogram(
            Assert.Single(days, report => report.Label == InputAnalysisService.InterArrivalSeriesLabel));
        Assert.EndsWith(" (1 day = 24 h)", dayCard.Caption!);

        var sessions = InputAnalysisService.FitAll(
            Binding(),
            "Exponential",
            new[] { new StageServiceFamily("Screening", DistributionFamily.Exponential) },
            0.05,
            FittingTimeUnit.Sessions);
        var sessionCard = InputAnalysisService.BuildHistogram(
            Assert.Single(sessions, report => report.Label == InputAnalysisService.InterArrivalSeriesLabel));
        Assert.EndsWith(" (1 session = 165 operating minutes)", sessionCard.Caption!);
    }

    [Fact]
    public void InputTab_ShowsTheChosenUnit_WhileTheRunValueStaysPerMinute()
    {
        var vm = new InputTabViewModel(new InputAnalysisViewModel());
        int changed = 0;
        vm.FittingUnitChanged += (_, _) => changed++;

        // Default is minutes on every launch (never persisted; FR-UI-21).
        Assert.Equal(FittingTimeUnit.Minutes, vm.SelectedFittingUnit);
        Assert.Equal("minutes", vm.SelectedFittingUnitLabel);

        vm.SetLoadedFile(Binding());
        Assert.Contains("0.2500 patients/min", vm.MleLambdaText);

        vm.SelectedFittingUnit = FittingTimeUnit.Hours;

        Assert.Equal(1, changed);
        Assert.Equal("hours", vm.SelectedFittingUnitLabel);
        Assert.Contains("15.0000 patients/hour", vm.MleLambdaText);
        Assert.Contains("converted from minutes", vm.MleLambdaText);

        // The window λ the run consumes is still per minute: 40 arrivals over
        // one 165-minute session.
        Assert.NotNull(vm.SelectedWindowLambda);
        Assert.Equal(40.0 / 165.0, vm.SelectedWindowLambda!.Value, 12);
        Assert.Contains("14.5455 patients/hour", vm.WindowLambdaText);
    }

    [Fact]
    public void Parameters_FittedMuLabel_StatesTheFitUnitAndTheManualUnit()
    {
        var vm = new ConfigPanelViewModel();
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));

        var screening = vm.StageRows[1]; // config stage 2 = Screening
        Assert.EndsWith("(from data)", screening.ServiceRateLabel, StringComparison.Ordinal);
        Assert.Matches(@"^μ = [0-9.]+ \(from data\)$", screening.ServiceRateLabel);

        vm.FittingUnitAccessor = () => FittingTimeUnit.Hours;
        vm.RefreshFittedUnitLabels();

        // The Input tab now reports this fit per hour; the Parameters row says
        // the same thing and still gives the per-minute figure it will consume.
        Assert.Contains("/hour", screening.ServiceRateLabel);
        Assert.Contains("/min", screening.ServiceRateLabel);
        Assert.EndsWith("(from data)", screening.ServiceRateLabel, StringComparison.Ordinal);
    }

    /// <summary>Walks up to the repo root and returns a shipped sample file.</summary>
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
    /// One usable binding with constant gaps (4 min) and service times (3 min),
    /// so the expected per-hour rates are exact and the χ² verdict is stable.
    /// </summary>
    private static DataBindingResult Binding()
    {
        // Uniform values across 2..6 minutes keep the mean at four and give the
        // chi-square binning real spread to work with.
        var interArrival = Enumerable.Range(0, 40).Select(i => 2.0 + (i % 5)).ToList();
        var service = Enumerable.Range(0, 40).Select(i => 1.5 + (i % 4)).ToList();

        var rows = Enumerable.Range(0, 40)
            .Select(_ => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>())
            .ToList();
        var dataSet = new DataSet(
            "synthetic.csv",
            new DateTime(2026, 1, 1),
            new[] { "stage" },
            rows);

        return new DataBindingResult(
            "synthetic.csv",
            dataSet,
            Array.Empty<ValidationIssue>(),
            null,
            1.0 / MeanInterArrivalMinutes,
            new[] { "Screening" },
            new[] { 1.0 / MeanServiceMinutes },
            0.4,
            0,
            0,
            0,
            interArrival,
            new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal)
            {
                ["Screening"] = service,
            });
    }
}
