using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.5 evidence frame — serial numbers and column alignment on the Results
/// panel's listing tables (FR-UI-36).
/// </summary>
/// <remarks>
/// <para>
/// A NEW file name. D-166 makes screenshot evidence append-only, and
/// <c>phase-8q-results-buffer-trace.png</c> is cited as evidence of the 8Q.4
/// layout, so re-rendering it would destroy the frame that shows 8Q.4.
/// </para>
/// <para>
/// No <c>Width</c> or <c>Height</c> is set anywhere in this test. The window is the
/// one <c>MainWindow.axaml</c> declares, so the frame is the size the app actually
/// renders at. A test that supplies its own size is testing a different program
/// from the one the user runs (D-166).
/// </para>
/// <para>
/// Preferences are restored on the way out <i>whether or not they existed</i>: if
/// the developer's <c>ui.json</c> was absent, toggling widgets would otherwise leave
/// a newly-created one behind, and a test must not write to machine state it did
/// not create (D-186).
/// </para>
/// </remarks>
public class Phase8Q5Screenshot
{
    [AvaloniaFact]
    public void Render_TableSerialNumbersAndAlignment_SavePhase8qTableAlignmentPng()
    {
        string prefsPath = WidgetPreferences.DefaultFilePath;
        bool prefsExisted = File.Exists(prefsPath);
        string? backup = prefsExisted ? File.ReadAllText(prefsPath) : null;

        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);

            // Only the three card families that carry tables, so one frame can show
            // the per-stage table, the chi-square table and the analytical
            // validation table at their real sizes. The queue-length, histogram and
            // simulation-verification widgets are charts, not tables, and would push
            // the tables below the fold.
            foreach (string key in new[]
            {
                "metrics", "chiSquare", "analyticalValidation",
            })
            {
                if (!main.Results.VisibleWidgets.Contains(key))
                {
                    main.Results.ToggleWidget(key);
                }
            }

            foreach (string key in new[]
            {
                "utilisation", "queueLength", "waitHistogram", "simulationVerification",
            })
            {
                if (main.Results.VisibleWidgets.Contains(key))
                {
                    main.Results.ToggleWidget(key);
                }
            }

            var result = RunSimulation(main);
            AttachSteadyStateAnalyticalRows(main.Results, result);
            window.UpdateLayout();

            var text = RenderedText(window);

            // The serial headers the alignment rule requires are really rendered —
            // read from the rendered text, not from the view model (D-186: the
            // location is what matters, and the location here is the screen).
            Assert.Contains("No.", text, StringComparison.Ordinal);
            Assert.Contains("Distribution", text, StringComparison.Ordinal);
            Assert.Contains("M/M/c wait", text, StringComparison.Ordinal);

            // The per-stage table keeps its own 8Q.3 "#" header (FR-UI-34); see D-187
            // for why 8Q.5 did not unify the two.
            Assert.Contains("#", text, StringComparison.Ordinal);

            // The numerical header labels are on screen, so the frame shows columns
            // and not just serial numbers.
            Assert.Contains("Wait (min)", text, StringComparison.Ordinal);

            Save(window, "phase-8q-table-alignment.png");
        }
        finally
        {
            window.Close();
            if (prefsExisted && backup is not null)
            {
                File.WriteAllText(prefsPath, backup);
            }
            else if (!prefsExisted && File.Exists(prefsPath))
            {
                // The test created it; leaving it would overwrite nothing but would
                // still leave machine state the test invented (D-186).
                File.Delete(prefsPath);
            }
        }
    }

    /// <summary>
    /// Runs a real three-stage simulation and hands the outcome to the results panel.
    /// </summary>
    private static SimulationResult RunSimulation(MainViewModel main)
    {
        var sample = Path.Combine(
            FindRepoRoot(AppContext.BaseDirectory), "samples", "sample_3stage_clinic.csv");
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

        main.Results.StartRun();
        main.Results.CompleteRun(outcome);
        return outcome.Result!;
    }

    /// <summary>
    /// Stamps the run as steady-state so the analytical validation table has rows.
    /// </summary>
    /// <remarks>
    /// <c>Compare</c> refuses any run under
    /// <see cref="AnalyticalValidationService.MinimumSteadyStateMinutes"/> minutes,
    /// because M/M/c closed forms describe steady state and a one-clinic-day run is
    /// not it. The refusal is correct; this only re-runs the comparison so the frame
    /// can show the table.
    /// </remarks>
    private static void AttachSteadyStateAnalyticalRows(
        ResultsPanelViewModel vm, SimulationResult actual)
    {
        var steady = new SimulationResult
        {
            StageMetrics = actual.StageMetrics,
            OperatingTimeMinutes = AnalyticalValidationService.MinimumSteadyStateMinutes,
        };

        vm.AnalyticalValidation.ApplyAsync(
            steady,
            "Exponential",
            new[] { "Exponential", "Exponential", "Exponential" },
            new System.Collections.Generic.List<(double Lambda, double Mu, int Servers)>
            {
                (0.1, 0.5, 1), (0.1, 0.25, 2), (0.1, 0.2, 2),
            });

        for (int i = 0; i < 50 && !vm.AnalyticalValidation.HasRows; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static MainViewModel RequireMain(Window window) =>
        window.DataContext as MainViewModel
        ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static string RenderedText(Window window) => string.Join(
        "\n",
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty));

    private static void Save(Window window, string fileName)
    {
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);

        var frame = HeadlessScreenshot.Capture(window);
        frame.Save(shotPath);

        // D-186: assert the file, not the return value of the call that wrote it.
        Assert.True(
            File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
            $"expected a rendered frame at {shotPath}");
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
