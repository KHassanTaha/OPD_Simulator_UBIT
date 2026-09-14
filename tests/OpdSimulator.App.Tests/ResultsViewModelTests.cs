using OpdSimulator.App.Models;

namespace OpdSimulator.App.Tests;

/// <summary>
/// The results panel lifecycle (M5-E, FR-STAT-6/7): the welcome card yields
/// to progress on a run, the engine report fills the tables, refusals surface
/// in the banner, and reset returns to the empty startup state (FR-UI-21).
/// </summary>
public class ResultsViewModelTests
{
    [Fact]
    public void BeginRun_HidesWelcome_AndShowsProgress()
    {
        var vm = new ResultsViewModel(new DataPreviewStore());

        vm.BeginRun();

        Assert.False(vm.IsWelcomeVisible);
        Assert.True(vm.ShowResults);
        Assert.True(vm.IsRunning);
        Assert.NotNull(vm.StatusText);
    }

    [Fact]
    public void EndRun_FillsSystemAndStageTables_FromEngineReport()
    {
        var outcome = SimOutcomeFactory.ManualRun();
        var vm = new ResultsViewModel(new DataPreviewStore());

        vm.EndRun(outcome, "Seed 7");

        Assert.Null(vm.RunError);
        Assert.True(vm.HasResults);
        Assert.False(vm.IsRunning);
        Assert.Equal(3, vm.Stages.Count);
        Assert.StartsWith("Reception", vm.Stages[0].StageName);
        Assert.Equal(outcome.Result!.TotalPatientsServed.ToString("N0"),
            vm.System.Served.Split(' ')[0]);
        Assert.Equal(outcome.Fits.Count, vm.Fits.Count);
        Assert.NotNull(vm.ChartsData);
        Assert.False(vm.HasTrace); // trace level None => empty
    }

    [Fact]
    public void EndRun_WithError_ShowsBanner_WithoutResults()
    {
        var vm = new ResultsViewModel(new DataPreviewStore());
        var refused = new RunOutcome(null, Array.Empty<FitReport>(), Array.Empty<string>(),
            EffectiveExitProbability: 0.4, Error: "The system is unstable.");

        vm.BeginRun();
        vm.EndRun(refused, "Seed 7");

        Assert.Equal("The system is unstable.", vm.RunError);
        Assert.True(vm.HasError);
        Assert.False(vm.HasResults);
        Assert.True(vm.ShowResults); // the results surface (banner) stays visible mid-session
    }

    [Fact]
    public void Reset_ReturnsToEmptyStartupState()
    {
        var outcome = SimOutcomeFactory.ManualRun();
        var vm = new ResultsViewModel(new DataPreviewStore());
        vm.EndRun(outcome, "Seed 7");
        vm.ToggleWidget("charts");

        vm.Reset();

        Assert.True(vm.IsWelcomeVisible);
        Assert.False(vm.ShowResults);
        Assert.Empty(vm.Stages);
        Assert.Empty(vm.Fits);
        Assert.Null(vm.ChartsData);
        Assert.False(vm.IsRunning);
    }

    [Theory]
    [InlineData("metrics")]
    [InlineData("chiSquare")]
    [InlineData("trace")]
    [InlineData("dataPreview")]
    [InlineData("charts")]
    [InlineData("token")]
    public void ToggleWidget_TogglesVisibilityBackAndForth(string key)
    {
        var vm = new ResultsViewModel(new DataPreviewStore());
        Assert.Contains(key, vm.VisibleWidgets);

        vm.ToggleWidget(key);
        Assert.DoesNotContain(key, vm.VisibleWidgets);
        Assert.False(IsVisible(vm, key));

        vm.ToggleWidget(key);
        Assert.Contains(key, vm.VisibleWidgets);
        Assert.True(IsVisible(vm, key));
    }

    [Fact]
    public void SetWidgets_RestrictsToKnownKeys_AndRaisesVisibility()
    {
        var vm = new ResultsViewModel(new DataPreviewStore());

        vm.SetWidgets(new[] { "metrics", "bogus" });

        Assert.Equal(new[] { "metrics" }, vm.VisibleWidgets);
        Assert.True(vm.ShowMetrics);
        Assert.False(vm.ShowCharts);
    }

    private static bool IsVisible(ResultsViewModel vm, string key)
        => key switch
        {
            "metrics" => vm.ShowMetrics,
            "chiSquare" => vm.ShowChiSquare,
            "trace" => vm.ShowTrace,
            "dataPreview" => vm.ShowDataPreview,
            "charts" => vm.ShowCharts,
            _ => vm.ShowToken,
        };
}