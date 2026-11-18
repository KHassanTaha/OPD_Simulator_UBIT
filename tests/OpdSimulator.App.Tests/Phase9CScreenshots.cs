using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 9C.4 headless walkthrough (AGENTS §18, D-089/D-166): the disclosures and
/// the `[Use the data fit]` reset are checked on the control tree the app really
/// builds, at the production window size, with a frame saved under a new name.
/// Existing evidence frames are append-only and are never overwritten (D-166).
/// </summary>
public class Phase9CScreenshots
{
    [AvaloniaFact]
    public void Render_LoadedData_DisclosesFitAndOverride_SavePhase9cDisclosuresPng()
    {
        // No size override: MainWindow.axaml declares the production size (D-166).
        var window = new MainWindow();
        window.Show();
        try
        {
            var config = RequireMain(window).Config;
            config.ApplyLoadedFile(Sample("sample_3stage_variable.csv"));
            // Apply synchronously so the structural assertions do not race the
            // background load pass; the frame shows the same view-model state.
            config.ApplyAutoFit(AutoFitService.Fit(config.Binding!, config.SignificanceLevelForRun));

            var winner = config.StageRows.First(r => r.BestFitFamily is not null);
            var other = Enum.GetValues<Core.Distributions.DistributionFamily>()
                .First(f => f != winner.BestFitFamily!.Value);
            winner.ApplyServiceFamily(other);
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var text = window.GetVisualDescendants().OfType<TextBlock>().ToList();
            Assert.Contains(text, t => t.Text == config.ArrivalFitDisclosure && t.IsEffectivelyVisible);
            Assert.Contains(text, t => t.Text == config.ArrivalOverrideWarning && t.IsEffectivelyVisible);
            Assert.Contains(text, t => t.Text == winner.FitOverrideWarning && t.IsEffectivelyVisible);

            var resetButtons = window.GetVisualDescendants().OfType<Button>()
                .Where(b => (b.Content?.ToString() ?? "") == "Use the data fit").ToList();
            Assert.True(resetButtons.Any(b => b.IsEffectivelyVisible),
                "at least one [Use the data fit] button must be visible on override");

            Save(window, "phase-9c-disclosures.png");
        }
        finally
        {
            window.Close();
        }
    }

    private static MainViewModel RequireMain(Window window)
        => window.DataContext as MainViewModel
           ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null && !File.Exists(Path.Combine(dir, "OpdSimulator.sln")); i++)
        {
            dir = Path.GetDirectoryName(dir);
        }

        var shotDir = Path.Combine(dir ?? throw new InvalidOperationException("repo root missing"),
            "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var path = Path.Combine(shotDir, fileName);
        frame.Save(path);
        Assert.True(File.Exists(path) && new FileInfo(path).Length >= 256,
            $"screenshot missing or suspiciously small: {path}");
    }

    private static string Sample(string fileName)
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

        throw new FileNotFoundException("sample not found", fileName);
    }
}
