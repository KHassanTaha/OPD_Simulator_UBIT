using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 5d evidence (feat/gui-rebuild): two rendered frames of the real
/// <see cref="ConfigPanel"/>.
/// 1) phase-5d-config.png — sample_patients.csv loaded against the default
///    3-stage config: the amber stage-count mismatch warning plus Sync/Keep
///    controls (D-114) and the Model section's significance-level α field
///    (D-113) are visible.
/// 2) phase-5d-cleared.png — after Clear All (ResetToDefaults): the warning
///    is gone and the Stages rows show topology-only "μ = — (no source)"
///    labels (5d.1, D-112), the panel back at factory defaults.
/// </summary>
public class Phase5dScreenshot
{
    [AvaloniaFact]
    public void Render_ConfigWarning_And_ClearedState_SavePhase5dScreenshots()
    {
        var vm = new ConfigPanelViewModel();
        vm.ParametersIsOptionalEnabled = true;
        vm.ManualLambda.Value = "0.1";
        vm.ManualMuPerStage.Value = "0.8, 0.5, 0.4";
        vm.ApplyLoadedFile(SamplePath("sample_patients.csv"));

        Assert.True(vm.IsStageMismatchWarningVisible,
            "fixture must produce the stage-count mismatch warning");
        // Phase 7C ruling 5: a fitted rate wins over the comma-list override, so
        // Screening (the one stage present in the sample data) reports the fitted
        // source; Reception and Doctor fall back to the manual comma list.
        Assert.EndsWith("(manual)", vm.StageRows[0].ServiceRateLabel, StringComparison.Ordinal);
        Assert.EndsWith("(from data)", vm.StageRows[1].ServiceRateLabel, StringComparison.Ordinal);
        Assert.EndsWith("(manual)", vm.StageRows[2].ServiceRateLabel, StringComparison.Ordinal);

        var host = new Window
        {
            Width = 460,
            Height = 900,
            Content = new ConfigPanel { DataContext = vm },
        };
        host.Show();

        try
        {
            var scroll = host.GetVisualDescendants().OfType<ScrollViewer>()
                .Single(s => s.Content is StackPanel);
            var root = FindRepoRoot(AppContext.BaseDirectory);
            var shotDir = Path.Combine(root, "logs", "screenshots");
            Directory.CreateDirectory(shotDir);

            var frame = HeadlessScreenshot.Capture(host);
            frame.Save(Path.Combine(shotDir, "phase-5d-config.png"));

            vm.ResetToDefaults();
            host.UpdateLayout();
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            host.UpdateLayout();

            var cleared = HeadlessScreenshot.Capture(host);
            cleared.Save(Path.Combine(shotDir, "phase-5d-cleared.png"));

            var configPath = Path.Combine(shotDir, "phase-5d-config.png");
            var clearedPath = Path.Combine(shotDir, "phase-5d-cleared.png");
            Assert.True(File.Exists(configPath) && new FileInfo(configPath).Length >= 256,
                "config frame missing or suspiciously small");
            Assert.True(File.Exists(clearedPath) && new FileInfo(clearedPath).Length >= 256,
                "cleared frame missing or suspiciously small");
        }
        finally
        {
            host.Close();
        }
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

    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("could not locate OpdSimulator.sln from " + start);
    }
}