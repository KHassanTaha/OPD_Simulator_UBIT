using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Data.Loaders;
using OpdSimulator.Data.Validation;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 7D gate (feat/milestone-7-model-driven): the merged Input tab. The
/// Data section's upload/preview/validation UI and the separate "Input
/// Analysis" tab become one "Input" tab (Simulation | Input | Token Generator |
/// Help); the data preview leaves the Results panel; and the config panel's data
/// UI is reduced to a status strip that hands stage-mismatch handling (D-114)
/// over to the Input tab.
/// </summary>
public class Phase7DTests
{
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

    private static DataSet Data(params IReadOnlyDictionary<string, string>[] rows)
        => new("sample.csv", DateTime.UtcNow, new[] { "arrival_time", "service_start" }, rows);

    private static IReadOnlyDictionary<string, string> Row(int arrival, int service)
        => new Dictionary<string, string>
        {
            ["arrival_time"] = arrival.ToString(),
            ["service_start"] = service.ToString(),
        };

    private static DataBindingResult Binding(
        DataSet? dataSet,
        IReadOnlyList<ValidationIssue>? issues = null,
        string? error = null,
        string path = "sample.csv")
        => new(
            path,
            dataSet,
            issues ?? Array.Empty<ValidationIssue>(),
            error,
            null,
            Array.Empty<string>(),
            Array.Empty<double>(),
            null,
            0,
            0,
            0,
            Array.Empty<double>(),
            new Dictionary<string, IReadOnlyList<double>>());

    private static Window Host(Control content, int width = 900, int height = 900)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static TabControl Shell(Window window)
        => window.GetVisualDescendants().OfType<TabControl>().Single();

    // ── InputPreviewViewModel (7D.1, RULING 1) ──────────────────────────

    [Fact]
    public void Preview_SetPreview_ProjectsColumnsAndRows()
    {
        var preview = new InputPreviewViewModel();

        preview.SetPreview(Binding(Data(Row(0, 5), Row(3, 9))));

        Assert.Equal(new[] { "arrival_time", "service_start" }, preview.ColumnTitles!);
        Assert.NotNull(preview.Rows);
        Assert.Equal(2, preview.Rows!.Count());
        Assert.True(preview.HasRows);
        Assert.Null(preview.LoadErrorSummary);
    }

    [Fact]
    public void Preview_SetPreview_MapsInvalidRowsToZeroBasedIndices()
    {
        var preview = new InputPreviewViewModel();
        var issues = new[] { new ValidationIssue(3, "arrival_time", "missing value") };

        preview.SetPreview(Binding(Data(Row(0, 5), Row(3, 9)), issues));

        Assert.NotNull(preview.InvalidRows);
        Assert.True(preview.InvalidRows!.ContainsKey(2));
        Assert.Equal("missing value", preview.InvalidRows[2]);
    }

    [Fact]
    public void Preview_HasValidationIssues_FalseWhenClean()
    {
        var preview = new InputPreviewViewModel();

        preview.SetPreview(Binding(Data(Row(0, 5))));

        Assert.False(preview.HasValidationIssues);
        Assert.False(preview.HasValidationErrors);
        Assert.Null(preview.BannerMessage);
    }

    [Fact]
    public void Preview_RowIssues_AreWarningsWithSummary()
    {
        var preview = new InputPreviewViewModel();
        var issues = new[] { new ValidationIssue(1, "arrival_time", "missing value") };

        preview.SetPreview(Binding(Data(Row(0, 5)), issues));

        Assert.True(preview.HasValidationIssues);
        Assert.False(preview.HasValidationErrors);
        Assert.Equal(BannerSeverity.Warning, preview.Severity);
        Assert.NotNull(preview.BannerMessage);
    }

    [Fact]
    public void Preview_LoadFailure_IsAnErrorWithMessage()
    {
        var preview = new InputPreviewViewModel();

        preview.SetPreview(Binding(null, error: "No stage columns found."));

        Assert.True(preview.HasValidationIssues);
        Assert.True(preview.HasValidationErrors);
        Assert.Equal(BannerSeverity.Error, preview.Severity);
        Assert.Equal("No stage columns found.", preview.BannerMessage);
    }

    [Fact]
    public void Preview_IssueSummary_TruncatesAfterFive()
    {
        var preview = new InputPreviewViewModel();
        var issues = Enumerable.Range(1, 7)
            .Select(i => new ValidationIssue(i, "arrival_time", $"problem {i}"))
            .ToArray();

        preview.SetPreview(Binding(Data(Row(0, 5)), issues));

        Assert.Contains("problem 5", preview.IssueSummary);
        Assert.DoesNotContain("problem 6", preview.IssueSummary);
        Assert.EndsWith("…and 2 more", preview.IssueSummary);
    }

    [Fact]
    public void Preview_Clear_ResetsEverything()
    {
        var preview = new InputPreviewViewModel();
        preview.SetPreview(Binding(Data(Row(0, 5))));

        preview.Clear();

        Assert.Null(preview.ColumnTitles);
        Assert.Null(preview.Rows);
        Assert.Null(preview.InvalidRows);
        Assert.Null(preview.LoadErrorSummary);
        Assert.False(preview.HasRows);
        Assert.False(preview.HasValidationIssues);
    }

    // ── InputTabViewModel (7D.1, RULING 2/3) ────────────────────────────

    [Fact]
    public void InputTab_StartsInEmptyState()
    {
        var vm = new InputTabViewModel();

        Assert.False(vm.HasFile);
        Assert.Equal("No file loaded.", vm.StatusText);
        Assert.False(vm.HasStageMismatch);
    }

    [Fact]
    public void InputTab_Analysis_IsTheInjectedInstance()
    {
        var analysis = new InputAnalysisViewModel();
        var vm = new InputTabViewModel(analysis);

        Assert.Same(analysis, vm.Analysis);
    }

    [Fact]
    public void InputTab_SetLoadedFile_ShowsPreviewAndStatus()
    {
        var vm = new InputTabViewModel();

        vm.SetLoadedFile(Binding(Data(Row(0, 5), Row(3, 9)), path: "clinic.csv"));

        Assert.True(vm.HasFile);
        Assert.Contains("2 rows", vm.StatusText);
        Assert.Contains("clinic.csv", vm.StatusText);
        Assert.True(vm.Preview.HasRows);
    }

    [Fact]
    public void InputTab_Clear_EmptiesPreviewAndAnalysis()
    {
        var vm = new InputTabViewModel();
        var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
        vm.SetLoadedFile(binding);
        vm.Analysis.Apply(binding, "Exponential", "Exponential", 0.05);
        Assert.False(vm.Analysis.IsEmpty);

        vm.Clear();

        Assert.False(vm.HasFile);
        Assert.True(vm.Analysis.IsEmpty);
        Assert.False(vm.Preview.HasRows);
    }

    [Fact]
    public void InputTab_UploadCommand_RaisesUploadRequested()
    {
        var vm = new InputTabViewModel();
        var raised = 0;
        vm.UploadFileRequested += (_, _) => raised++;

        vm.UploadFileCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InputTab_UseForSimulationCommand_RaisesRequested()
    {
        var vm = new InputTabViewModel();
        var raised = 0;
        vm.UseForSimulationRequested += (_, _) => raised++;

        vm.UseForSimulationCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InputTab_SyncStagesCommand_RaisesRequested()
    {
        var vm = new InputTabViewModel();
        var raised = 0;
        vm.StageMismatchSyncRequested += (_, _) => raised++;

        vm.SyncStagesFromDataCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InputTab_KeepStagesCommand_RaisesRequested()
    {
        var vm = new InputTabViewModel();
        var raised = 0;
        vm.StageMismatchKeepRequested += (_, _) => raised++;

        vm.KeepCurrentStagesCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InputTab_ClearCommand_RaisesClearFileRequested()
    {
        var vm = new InputTabViewModel();
        var raised = 0;
        vm.ClearFileRequested += (_, _) => raised++;

        vm.ClearFileCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void InputTab_ApplyStageMismatch_TogglesWarning()
    {
        var vm = new InputTabViewModel();

        vm.ApplyStageMismatch(true, "1 stage(s) vs 3");

        Assert.True(vm.HasStageMismatch);
        Assert.Equal("1 stage(s) vs 3", vm.StageMismatchMessage);

        vm.ApplyStageMismatch(false, "");

        Assert.False(vm.HasStageMismatch);
    }

    // ── Input tab view (7D.2) ───────────────────────────────────────────

    [AvaloniaFact]
    public void InputTab_EmptyState_ShowsNoDataMessage()
    {
        var vm = new InputTabViewModel();
        var window = Host(new InputTab { DataContext = vm });

        try
        {
            var empty = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == "No data file loaded.");
            Assert.True(empty.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputTab_ClearFileButton_HiddenUntilFileLoaded()
    {
        var vm = new InputTabViewModel();
        var window = Host(new InputTab { DataContext = vm });

        try
        {
            var clear = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Clear File");
            Assert.False(clear.IsEffectivelyVisible);

            vm.SetLoadedFile(Binding(Data(Row(0, 5))));
            window.UpdateLayout();

            Assert.True(clear.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputTab_AfterLoad_AnalysisSectionVisible()
    {
        var vm = new InputTabViewModel();
        var binding = DataAnalyzer.Analyze(SamplePath("sample_patients.csv"));
        var window = Host(new InputTab { DataContext = vm });

        try
        {
            vm.SetLoadedFile(binding);
            vm.Analysis.Apply(binding, "Exponential", "Exponential", 0.05);
            window.UpdateLayout();

            var preview = window.GetVisualDescendants().OfType<DataPreviewTable>().Single();
            var analysis = window.GetVisualDescendants().OfType<InputAnalysisView>().Single();

            Assert.True(preview.IsEffectivelyVisible, "the merged tab must show the data preview");
            Assert.True(analysis.IsEffectivelyVisible, "the merged tab must show the fit analysis");
            Assert.False(vm.Analysis.IsEmpty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputTab_ValidationIssues_RenderAsBanner()
    {
        var vm = new InputTabViewModel();
        var issues = new[] { new ValidationIssue(1, "arrival_time", "missing value") };
        var window = Host(new InputTab { DataContext = vm });

        try
        {
            vm.SetLoadedFile(Binding(Data(Row(0, 5)), issues));
            window.UpdateLayout();

            var banner = window.GetVisualDescendants().OfType<ErrorBanner>()
                .First(b => b.IsEffectivelyVisible);
            Assert.Contains("missing value", banner.Message);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InputTab_StageMismatchWarning_VisibleWhenSet()
    {
        var vm = new InputTabViewModel();
        vm.SetLoadedFile(Binding(Data(Row(0, 5))));
        vm.ApplyStageMismatch(true, "The data has 1 stage(s).");
        var window = Host(new InputTab { DataContext = vm });

        try
        {
            var sync = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Sync stages from data");
            Assert.True(sync.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    // ── Config panel strip (7D.4) ───────────────────────────────────────

    [AvaloniaFact]
    public void ConfigPanel_DataSection_ReplacedByStatusStrip()
    {
        var window = Host(new ConfigPanel { DataContext = new ConfigPanelViewModel() });

        try
        {
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<CollapsibleSection>(),
                s => s.Title == "1 · Data");

            var manage = window.GetVisualDescendants().OfType<Button>()
                .Single(b => (b.Content?.ToString() ?? "") == "Manage input →");
            Assert.True(manage.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ConfigPanel_StatusStrip_MismatchIndicator_VisibleOnMismatch()
    {
        var vm = new ConfigPanelViewModel();
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        Assert.True(vm.HasStageMismatch, "precondition: the sample's 1 stage differs from the default 3");

        var window = Host(new ConfigPanel { DataContext = vm });

        try
        {
            var indicator = window.GetVisualDescendants().OfType<TextBlock>()
                .Single(t => t.Text == "Stages differ from data — see the Input tab.");
            Assert.True(indicator.IsEffectivelyVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ConfigPanel_ManageInputButton_NavigatesToInputTab()
    {
        var main = new MainViewModel();
        var selected = new List<int>();
        main.TabSelectionChanged += (_, index) => selected.Add(index);

        main.Config.NavigateToInputTabCommand.Execute(null);

        Assert.Equal(new[] { 1 }, selected);
    }

    // ── Results panel loses the preview (7D.5) ──────────────────────────

    [Fact]
    public void ResultsPanel_NoLongerContainsDataPreview()
    {
        Assert.Null(typeof(ResultsPanelViewModel).GetProperty("ShowDataPreview"));
        Assert.Null(typeof(ResultsPanelViewModel).GetProperty("Preview"));
    }

    [AvaloniaFact]
    public void ResultsPanel_View_HasNoDataPreviewTable()
    {
        var window = Host(new ResultsPanel { DataContext = new ResultsPanelViewModel() });

        try
        {
            Assert.Empty(window.GetVisualDescendants().OfType<DataPreviewTable>());
            Assert.DoesNotContain(
                window.GetVisualDescendants().OfType<CheckBox>(),
                c => c.Content?.ToString() == "Data preview");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void WidgetPreferences_LegacyDataPreviewKey_StillParses()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opd-7d-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{\"VisibleWidgets\":[\"metrics\",\"dataPreview\"]}");
        try
        {
            var prefs = WidgetPreferences.Load(path);

            Assert.Contains("metrics", prefs.VisibleWidgets);
            Assert.Contains("dataPreview", prefs.VisibleWidgets);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Shell tab order (7D.3, RULING 2) ────────────────────────────────

    [AvaloniaFact]
    public void Shell_Tabs_AreSimulationInputTokenHelp()
    {
        var window = new MainWindow();
        window.Show();

        try
        {
            var headers = Shell(window).Items.Cast<TabItem>()
                .Select(t => t.Header?.ToString()).ToArray();
            Assert.Equal(new[] { "Simulation", "Input", "Token Generator", "Help" }, headers);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TabIndex_InputIsOne()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Assert.Equal("Input", (Shell(window).Items[1] as TabItem)?.Header as string);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TabIndex_TokenGeneratorIsTwo()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Assert.Equal("Token Generator", (Shell(window).Items[2] as TabItem)?.Header as string);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TabIndex_HelpIsThree()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Assert.Equal("Help", (Shell(window).Items[3] as TabItem)?.Header as string);
        }
        finally
        {
            window.Close();
        }
    }

    // ── MainViewModel wiring (7D.6) ─────────────────────────────────────

    [AvaloniaFact]
    public void InputTab_UseForSimulation_SwitchesToFitAndSelectsSimulation()
    {
        var main = new MainViewModel { PickDataFileAsync = () => Task.FromResult<string?>(null) };
        main.Config.SourceMode = DataSourceMode.EnterManually;
        var selected = new List<int>();
        main.TabSelectionChanged += (_, index) => selected.Add(index);

        main.InputTab.UseForSimulationCommand.Execute(null);

        Assert.Equal(DataSourceMode.FitFromData, main.Config.SourceMode);
        Assert.Equal(new[] { 0 }, selected);
    }

    [AvaloniaFact]
    public void InputTab_StageSync_ReplacesConfigStages()
    {
        var main = new MainViewModel { PickDataFileAsync = () => Task.FromResult<string?>(null) };
        main.Config.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        Assert.True(main.Config.IsStageMismatchWarningVisible);

        main.InputTab.SyncStagesFromDataCommand.Execute(null);

        Assert.Single(main.Config.StageRows);
        Assert.False(main.Config.IsStageMismatchWarningVisible);
        Assert.False(main.InputTab.HasStageMismatch);
    }

    [AvaloniaFact]
    public void InputTab_ClearFile_UnloadsConfigFile()
    {
        var main = new MainViewModel { PickDataFileAsync = () => Task.FromResult<string?>(null) };
        main.Config.ApplyLoadedFile(SamplePath("sample_patients.csv"));
        Assert.NotNull(main.Config.Binding);
        Assert.True(main.InputTab.HasFile);

        main.InputTab.ClearFileCommand.Execute(null);

        Assert.Null(main.Config.Binding);
        Assert.Null(main.Config.LoadedFileName);
        Assert.False(main.InputTab.HasFile);
    }

    [Fact]
    public async Task InputTab_UploadRequested_UsesTheSinglePickerPath()
    {
        var picks = 0;
        var main = new MainViewModel();
        main.PickDataFileAsync = () =>
        {
            picks++;
            return Task.FromResult<string?>(null);
        };

        // Both entry points route to the same picker (RULING 3).
        main.InputTab.UploadFileCommand.Execute(null);
        main.Config.UploadDataCommand.Execute(null);

        // The async-void handlers complete synchronously for a completed picker task.
        await Task.Yield();
        Assert.Equal(2, picks);
    }
}
