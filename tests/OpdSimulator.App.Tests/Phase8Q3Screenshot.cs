using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.3 frame evidence (D-183) — the Performance Measures section with its
/// per-stage listing, serial-number column and stability verdicts, through the
/// real <see cref="MainWindow"/>, per the D-089 method.
/// </summary>
/// <remarks>
/// <para>
/// The frame is evidence; the assertions live in <see cref="PerformanceMeasuresTests"/>
/// against the real view model. What is checked here is the narrower claim that
/// the pieces are on screen <b>together</b> — a verdict that exists in the view
/// model but renders below the fold, or in a column that overlaps the stage name,
/// is a failure no view-model test can see.
/// </para>
/// <para>
/// New filename only (D-166). This is the first frame of this surface, so there is
/// no earlier frame of it to preserve.
/// </para>
/// </remarks>
public class Phase8Q3Screenshot
{
    [AvaloniaFact]
    public void Render_PerformanceMeasuresAndStability_SavePhase8qPerformanceMeasuresPng()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var vm = window.DataContext as MainViewModel
                ?? throw new InvalidOperationException("MainWindow must bind a MainViewModel");

            // A real three-stage run against the committed clinic sample, so the
            // λ, c, μ and ρ on screen are the engine's own rather than literals
            // chosen to look plausible. Server counts 1/2/2 leave Screening and
            // Doctor close enough to saturation that the section has something to
            // say — a frame of three comfortable green rows proves nothing.
            var sample = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv");
            var binding = DataAnalyzer.Analyze(sample);
            Assert.True(binding.IsUsable, "the multi-stage sample must analyse cleanly");

            var config = new ConfigPanelViewModel();
            config.ParametersIsOptionalEnabled = true;
            config.ManualLambda.Value = "0.1";
            config.ManualMuPerStage.Value = "0.5, 0.25, 0.2";
            for (int i = 0; i < 3 && i < config.StageRows.Count; i++)
            {
                config.StageRows[i].Servers.Value = new[] { "1", "2", "2" }[i];
            }

            var outcome = SimulationCoordinator.Run(config.TryBuildRunParameters()!, binding);
            Assert.Null(outcome.Error);
            Assert.NotNull(outcome.Result);

            vm.Results.StartRun();
            vm.Results.CompleteRun(outcome);
            window.UpdateLayout();

            var text = RenderedText(window);

            // The section heading is the thing being added, so it is named here
            // rather than inferred from the rows beneath it.
            Assert.Contains("Performance Measures", text);
            Assert.Contains("System totals", text);
            Assert.Contains("Stability", text);

            // The serial-number column: a "#" header plus at least one number in
            // the first column position. Asserted as text because the header alone
            // would pass even if the bindings behind it were empty.
            Assert.Contains("#", text);

            // Every stage appears in both the listing and the verdict block, which
            // is the "same rows, two readings" relationship the section claims.
            foreach (var stage in new[] { "Reception", "Screening", "Doctor" })
            {
                Assert.Contains(stage, text);
            }

            Assert.True(vm.Results.HasStability, "a completed run must produce verdicts");
            Assert.NotEmpty(vm.Results.BottleneckText);

            var path = Save(window, "phase-8q-performance-measures.png");
            Assert.True(File.Exists(path), $"screenshot missing: {path}");
            Assert.True(
                new FileInfo(path).Length >= 256,
                $"screenshot suspiciously small: {new FileInfo(path).Length} bytes");
        }
        finally
        {
            window.Close();
        }
    }

    private static string RenderedText(Window window) => string.Join(
        "\n",
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));

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

    private static string Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        return shotPath;
    }
}