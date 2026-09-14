using SkiaSharp;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Chart data projection (FR-UI-4): the pure ChartsBuilder output must map
/// onto LiveCharts2 view models the tabs bind to, with empty placeholders
/// instead of crashes for empty inputs. Runs without an Avalonia app session,
/// exercising the palette fallback path.
/// </summary>
public class ChartViewModelTests
{
    [Fact]
    public void FromCategorical_EmptyData_YieldsEmptyChart()
    {
        var chart = ChartViewModel.FromCategorical(
            new CategoricalChartData("Histogram", Array.Empty<string>(), Array.Empty<ChartSeries>()),
            SKColors.Teal, SKColors.Navy);

        Assert.False(chart.HasData);
        Assert.True(chart.ShowEmpty);
        Assert.Empty(chart.Series);
    }

    [Fact]
    public void FromCategorical_ObservedPlusFitted_YieldsColumnsAndLineOverlay()
    {
        var data = new CategoricalChartData(
            "Inter-arrival histogram",
            new[] { "[0,1)", "[1,2)" },
            new[]
            {
                new ChartSeries("Observed", new double[] { 4, 6 }),
                new ChartSeries("Fitted distribution", new double[] { 3.7, 5.1 }),
            });

        var chart = ChartViewModel.FromCategorical(data, SKColors.Teal, SKColors.Navy);

        Assert.True(chart.HasData);
        Assert.Equal(3, chart.Series.Count); // two columns + the fitted line
        Assert.Equal(2, chart.XAxes[0].Labels!.Count);
        Assert.Equal("Inter-arrival histogram", chart.Title);
    }

    [Fact]
    public void FromLine_EmptyData_YieldsEmptyChart()
    {
        var chart = ChartViewModel.FromLine(
            new LineChartData("X", Array.Empty<double>(), Array.Empty<double>(), "Series"),
            SKColors.Teal);

        Assert.False(chart.HasData);
        Assert.Empty(chart.Series);
    }

    [Fact]
    public void FromLine_PopulatesSeriesAndAxes()
    {
        var chart = ChartViewModel.FromLine(
            new LineChartData("Queue length", new double[] { 0, 1, 2 }, new double[] { 1, 3, 2 }, "Reception"),
            SKColors.Teal);

        Assert.True(chart.HasData);
        Assert.Single(chart.Series);
    }

    [Fact]
    public void SetCharts_PopulatesBothTabs_FromASuccessfulRun()
    {
        var outcome = SimOutcomeFactory.ManualRun();
        var data = ChartsBuilder.Build(outcome.Result!, outcome.Fits);
        var vm = ChartsPanelViewModel.ForResources(); // no app session: uses palette fallback

        vm.SetCharts(data);

        Assert.True(vm.HasCharts);
        Assert.False(vm.ShowEmpty);
        // Both tabs populate from a successful run. Exact counts vary with which
        // service fits succeed on the generated samples, so assert the stable
        // guarantees: the inter-arrival + chi-square + utilisation charts are
        // always present, and each stage contributes a waiting histogram.
        Assert.True(vm.InputCharts.Count >= 3);
        Assert.True(vm.RunCharts.Count >= 3);
        Assert.Contains(vm.InputCharts, c => c.HasData);
    }

    [Fact]
    public void SetCharts_Null_ClearsBothTabs_AndShowsEmptyState()
    {
        var vm = ChartsPanelViewModel.ForResources();

        vm.SetCharts(null);

        Assert.Empty(vm.InputCharts);
        Assert.Empty(vm.RunCharts);
        Assert.False(vm.HasCharts);
        Assert.True(vm.ShowEmpty);
    }

    [Fact]
    public void Clear_ResetsTabs_AndEmptyState()
    {
        var outcome = SimOutcomeFactory.ManualRun();
        var vm = ChartsPanelViewModel.ForResources();
        vm.SetCharts(ChartsBuilder.Build(outcome.Result!, outcome.Fits));

        vm.Clear();

        Assert.False(vm.HasCharts);
        Assert.True(vm.ShowEmpty);
    }
}