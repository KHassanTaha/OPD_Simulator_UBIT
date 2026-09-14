using System.IO;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Data.Parameters;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Root view-model behaviour (M5-F): prefs are applied on construction and the
/// run summary header renders the configured horizon and cap legibly.
/// </summary>
public class MainViewModelTests
{
    [Fact]
    public void Constructor_AppliesPersistedWidgetPreferences_AndCollapsedSections()
    {
        string dir = Path.Combine(Path.GetTempPath(), "opdsim-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var prefs = new WidgetPreferences(Path.Combine(dir, "ui.json"));
            prefs.VisibleWidgets.Add("metrics");
            prefs.VisibleWidgets.Add("charts");
            prefs.CollapsedSections.Add("parameters");
            prefs.Save();

            var vm = new MainViewModel(a => a(), new ToastService(), WidgetPreferences.Load(Path.Combine(dir, "ui.json")));

            Assert.Equal(new[] { "metrics", "charts" }, vm.Results.VisibleWidgets);
            Assert.Contains("parameters", vm.Config.CollapsedSectionKeys);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildRunSummaryHeader_DaysMode_StatesDaysAndStartDay()
    {
        var p = SimOutcomeFactory.Parameters(seed: 42);
        var header = MainViewModel.BuildRunSummaryHeader(p);

        Assert.Contains("Seed 42", header);
        Assert.Contains("day(s), starting Monday", header);
    }

    [Fact]
    public void BuildRunSummaryHeader_MinutesMode_StatesWindow()
    {
        var p = SimOutcomeFactory.Parameters() with { HorizonMode = HorizonMode.Minutes, HorizonMinutes = 30 };
        var header = MainViewModel.BuildRunSummaryHeader(p);

        Assert.Contains("30 minute window", header);
        Assert.DoesNotContain("day", header);
    }

    [Fact]
    public void BuildRunSummaryHeader_WithDailyCap_AppendsCap()
    {
        var p = SimOutcomeFactory.Parameters() with { DailyCap = 10 };
        var header = MainViewModel.BuildRunSummaryHeader(p);

        Assert.Contains("cap 10/day", header);
    }

    [Fact]
    public void Commands_StartAndReset_AreExposed()
    {
        var vm = new MainViewModel(a => a(), new ToastService(), new WidgetPreferences());
        Assert.NotNull(vm.RunCommand);
        Assert.NotNull(vm.ResetAllCommand);
        Assert.NotNull(vm.OpenGuideCommand);
        Assert.NotNull(vm.CloseGuideCommand);
    }

    [Fact]
    public void ResetAll_ClearsConfigAndResults_AndToasts()
    {
        var toasts = new ToastService();
        var vm = new MainViewModel(a => a(), toasts, new WidgetPreferences());
        vm.Results.BeginRun();

        vm.ResetAll();

        Assert.True(vm.Results.IsWelcomeVisible);
        Assert.Single(toasts.Toasts.Where(t => t.Kind == ToastKind.Info));
    }
}