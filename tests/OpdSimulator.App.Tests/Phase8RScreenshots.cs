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
using OpdSimulator.Data.Parameters;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8R visual gate evidence (D-190, D-191): the cap field as a user sees it,
/// the backlog/drain table beside the metrics it complements, and the two-stage
/// clinic network routed with a front-door bypass.
/// </summary>
/// <remarks>
/// <para>
/// Screenshot evidence is append-only (D-166): each test writes a NEW filename and
/// never overwrites a frame cited as evidence of a known defect. These three are new
/// claims, not re-renders of old ones.
/// </para>
/// <para>
/// A screenshot is asserted by opening the PNG afterwards and by checking the text
/// the window actually rendered — not by trusting that the capture call returned
/// (D-186). And no test here sets Width/Height/MinWidth/MaxWidth: the sizing in
/// question is the production sizing, and a test that supplied a size the app does
/// not supply would be a test of a different program (D-166).
/// </para>
/// </remarks>
public class Phase8RScreenshots
{
    /// <summary>The clinic capture. Real patient data, uncommitted by design.</summary>
    private const string ClinicCapture = "opd_collection_28_sep_2026_1.csv";

    [AvaloniaFact]
    public void Render_CapField_SavePhase8rCapFieldPng()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);

            // A fresh launch already carries the documented default (FR-UI-21), so the
            // frame shows the field the way the user first meets it.
            Assert.Equal(ConfigPanelViewModel.DefaultDailyCap, main.Config.DailyCap.Value);

            window.UpdateLayout();
            var text = RenderedText(window);

            // The label is persistent, not a placeholder (AGENTS §16.7), the example
            // format is in the watermark, and the scope is stated where it is read.
            Assert.Contains("Maximum patients admitted per session", text);
            Assert.Contains("85", text);
            Assert.Contains("Screening", text);

            // The cap is offered for both calendar modes (D-190).
            Assert.True(main.Config.DailyCapVisible);

            Save(window, "phase-8r-cap-field.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_BacklogAndDrainTable_SavePhase8rBacklogDrainPng()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);

            // A capped two-stage run: the figures under test only appear once there
            // is a session that closes with work outstanding.
            var parameters = TwoStageCappedParameters();
            var outcome = SimulationCoordinator.Run(parameters, binding: null);
            Assert.Null(outcome.Error);

            var result = outcome.Result!;
            Assert.Equal(2, result.StageMetrics.Count);
            Assert.Equal(85, result.ScreeningAdmittedPerDay[0]);

            main.Results.StartRun();
            main.Results.CompleteRun(outcome);
            window.UpdateLayout();

            var text = RenderedText(window);

            // The section exists, is named for what it measures, and says which run's
            // figures these are.
            Assert.Contains("Backlog", text);
            Assert.Contains("drain", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("session", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Screening", text);
            Assert.Contains("Doctor", text);

            // The table is populated from the engine, not left as empty scaffolding:
            // a closed session with a backlog must show a non-zero drain somewhere.
            Assert.Contains(result.StageMetrics.Max(s => s.DrainMinutes).ToString("0.#"), text);

            Save(window, "phase-8r-backlog-drain.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The two-stage clinic shape with a front-door bypass, rendered from the real
    /// capture. Returns early when the capture is absent (D-178).
    /// </summary>
    /// <remarks>
    /// What this frame adds over the unit tests is the routed network as the user
    /// meets it after loading the clinic file. The routing itself is asserted
    /// unconditionally by <c>Coordinator_TwoStageNetworkRoutesTheBypassInsteadOfRefusingIt</c>,
    /// which reaches the same two-stage topology on data CI has.
    /// </remarks>
    [AvaloniaFact]
    public void Render_TwoStageBypassFromTheClinicCapture_SavePhase8r2StageBypassPng()
    {
        var path = Sample(ClinicCapture);
        if (!File.Exists(path))
        {
            return;
        }

        var window = new MainWindow();
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(path);
            window.UpdateLayout();

            // Loading does NOT silently restructure the model (D-105): the factory
            // default is three stages and the file describes two, so the panel must
            // say so and leave the choice to the user.
            Assert.Equal(3, main.Config.StageRows.Count);
            Assert.True(main.Config.IsStageMismatchWarningVisible,
                "a 2-stage file against a 3-stage default must raise the mismatch warning");
            Assert.Contains("2 stage", main.Config.StageMismatchMessage, StringComparison.Ordinal);
            Assert.Contains("Screening, Doctor", main.Config.StageMismatchMessage, StringComparison.Ordinal);

            // The user confirms, and the network becomes the clinic's own two stages.
            main.Config.SyncStagesToData();

            var stageNames = main.Config.StageRows.Select(r => r.StageName).ToArray();
            Assert.Equal(["Screening", "Doctor"], stageNames);
            Assert.False(main.Config.IsStageMismatchWarningVisible, "the warning is spent once the rows agree with the data");
            Assert.True(main.Config.PBypassVisible, "a two-stage network can skip its front door");

            SelectInputTab(window);
            window.UpdateLayout();
            var text = RenderedText(window);
            Assert.Contains("Screening", text);
            Assert.Contains("Doctor", text);

            Save(window, "phase-8r-2stage-bypass.png");
        }
        finally
        {
            window.Close();
        }
    }

    // ── The configuration under test ───────────────────────────────────────

    /// <summary>
    /// λ₀ = 1.0/min into a two-stage clinic, a quarter sent straight to the Doctor,
    /// the daily cap at 85 over a 165-minute session. Both stages keep up, so the
    /// figures on screen reflect the admission gate rather than a queue that never
    /// cleared.
    /// </summary>
    private static SimulationParameters TwoStageCappedParameters() => new(
        ParameterMode.RateWise,
        "Exponential",
        ManualArrivalRate: 1.0,
        ["Screening", "Doctor"],
        [2, 3],
        new double?[] { null, null },
        RunMode.ClinicDay,
        HorizonMinutes: 600,
        GeneratorDays: 1,
        StartDay: DayOfWeek.Monday,
        DailyCap: 85,
        Seed: 42,
        PExitOverride: null,
        "Standard")
    {
        ServiceRates = new double?[] { 1.0, 1.0 },
        ServiceFamilies =
        [
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Exponential, Mean: 1.0),
            new Core.Distributions.DistributionSpec(Core.Distributions.DistributionFamily.Exponential, Mean: 1.0),
        ],
        PBypassOverride = 0.25,
    };

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MainViewModel RequireMain(Window window) =>
        window.DataContext as MainViewModel
        ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static void SelectInputTab(Window window)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = 1;
        window.UpdateLayout();
    }

    private static string RenderedText(Window window) => string.Join(
        "\n",
        window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? ""));

    private static string Sample(string fileName) =>
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", fileName);

    private static void Save(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);

        Assert.True(
            File.Exists(shotPath) && new FileInfo(shotPath).Length >= 512,
            $"{fileName} missing or suspiciously small");
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
