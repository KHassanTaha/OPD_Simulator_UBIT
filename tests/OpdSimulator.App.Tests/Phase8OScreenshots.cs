using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8O gate evidence (D-172, D-173, D-174) — the headless walkthrough
/// through the real <see cref="MainWindow"/>, per the D-089 method.
/// </summary>
/// <remarks>
/// <para>
/// The frames are evidence, not assertions. Every structural claim in this file
/// is asserted in <see cref="Phase8OTests"/> against the real objects; what is
/// left here is the picture the owner reviews, and each test saves a NEW
/// filename. Nothing in this file overwrites an existing screenshot — see D-166:
/// an existing frame is the evidence that a defect existed, and re-rendering its
/// filename deletes that evidence.
/// </para>
/// <para>
/// The five frames, in the order the user meets them:
/// </para>
/// <list type="number">
/// <item>the Input tab with a single-session file — the MLE-only case, unchanged
/// from pre-8O behaviour;</item>
/// <item>the Input tab with the six-session file — the dual λ panel, the
/// divergence note, and the window that produced the window λ;</item>
/// <item>Horizon with the calendar run modes, showing that no time-span dropdown
/// survives;</item>
/// <item>Horizon in diagnostic-trace mode, showing the Duration dropdown and
/// its 1-hour default;</item>
/// <item>the calculations dialog after a run that used the window estimate.</item>
/// </list>
/// </remarks>
public class Phase8OScreenshots
{
    [AvaloniaFact]
    public void Render_InputSingleSession_SavePhase8oInputSingleSessionPng()
    {
        var window = new MainWindow();
        window.Width = 1400;
        window.Height = 2200;
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));

            SelectInputTab(window);
            window.UpdateLayout();

            // A single-session file has ONE λ and one window, and 8O must not
            // have changed what such a user sees. Asserting "not disabled" on a
            // default-constructed tab would prove nothing, so the claim is made
            // about the file: a window λ that matches 165 minutes exactly, and
            // both radios offered.
            var text = RenderedText(window);
            Assert.Contains("Observation window", text);
            Assert.Contains("Arrival rate (λ)", text);
            Assert.Contains("1 operating day (165 operating minutes)",
                main.InputTab.WindowLambdaText);
            Assert.False(main.InputTab.IsWindowChoiceDisabled);
            Assert.True(main.InputTab.HasWindowLambda);

            Save(window, "phase-8o-input-single-session.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_InputMultiday_SavePhase8oInputMultidayPng()
    {
        var window = new MainWindow();
        window.Width = 1400;
        window.Height = 2400;
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(Sample("sample_multiday.csv"));

            SelectInputTab(window);
            window.UpdateLayout();

            // The frame's whole purpose: both estimates are on screen at once,
            // with the divergence between them stated in words.
            var text = RenderedText(window);
            Assert.Contains("Arrival rate (λ)", text);
            Assert.Contains("MLE (1 ÷ mean inter-arrival gap)", text);
            Assert.Contains("Window (arrivals ÷ operating minutes)", text);
            Assert.Contains("6 operating days (990 operating minutes)",
                main.InputTab.WindowLambdaText, StringComparison.Ordinal);
            Assert.NotNull(main.InputTab.LambdaDivergenceText);
            Assert.NotEmpty(main.InputTab.LambdaDivergenceText!);

            Save(window, "phase-8o-input-multiday.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The Input tab at the window size the app actually opens at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other four 8O frames size the window to 1400×2200 so a tall tab fits
    /// in one image, and that is a **document capture, not a picture of the
    /// app**: <c>MainWindow.axaml</c> declares <c>Width="1200" Height="760"</c>,
    /// so a user has never seen the layout those frames show, and a control
    /// clipped or re-wrapped at 1200 would not show up in them. That is D-166's
    /// failure mode in a new dress — a test rendering a configuration the app
    /// does not produce — and it is why this frame exists.
    /// </para>
    /// <para>
    /// The size is read from the window's own declared bounds rather than typed
    /// in, so if the XAML ever changes size this frame follows it. Asserting
    /// the declared value is the other half: a test that quietly renders at
    /// 1200×760 while the app opens at something else is the defect, not the
    /// fix.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Render_InputMultiday_AtDeclaredWindowSize_SavePhase8oInputMultidayDefaultSizePng()
    {
        var window = new MainWindow();
        var declaredWidth = window.Width;
        var declaredHeight = window.Height;
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.ApplyLoadedFile(Sample("sample_multiday.csv"));

            SelectInputTab(window);
            window.UpdateLayout();

            // The size the app ships with, asserted rather than assumed: a frame
            // taken at a size the user cannot reach is not evidence.
            Assert.Equal(1200.0, declaredWidth);
            Assert.Equal(760.0, declaredHeight);
            Assert.Equal(1200.0, window.Bounds.Width);
            Assert.Equal(760.0, window.Bounds.Height);

            // And the λ block survived the narrower layout — this is the claim
            // the taller frames cannot make.
            var text = RenderedText(window);
            Assert.Contains("MLE (1 ÷ mean inter-arrival gap)", text);
            Assert.Contains("Window (arrivals ÷ operating minutes)", text);

            Save(window, "phase-8o-input-multiday-default-size.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// D-174's evidence. The "Time span" dropdown drove BOTH the diagnostic
    /// minutes and the calendar run length, and is gone. This frame proves its
    /// absence in the calendar modes — the run-mode radios, the Days field, and
    /// no span control anywhere.
    /// </summary>
    [AvaloniaFact]
    public void Render_HorizonCalendar_SavePhase8oHorizonCleanPng()
    {
        var window = new MainWindow();
        window.Width = 1400;
        window.Height = 1800;
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.IsMultiDay = true;
            main.Config.Days.Value = "5";
            main.Config.CollapseAllCommand.Execute(null);
            main.Config.ExpandAllCommand.Execute(null);

            SelectTab(window, 0);
            window.UpdateLayout();

            // The defect D-174 removed is asserted, not just photographed: a
            // label reading "Time span" would mean the coupled control came back.
            Assert.DoesNotContain("Time span", RenderedText(window));
            Assert.Contains("Single day", RenderedText(window));
            Assert.Contains("Diagnostic trace", RenderedText(window));

            Save(window, "phase-8o-horizon-clean.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_HorizonDiagnostic_SavePhase8oHorizonDiagnosticPng()
    {
        var window = new MainWindow();
        window.Width = 1400;
        window.Height = 1800;
        window.Show();
        try
        {
            var main = RequireMain(window);
            main.Config.IsDiagnosticTrace = true;
            main.Config.CollapseAllCommand.Execute(null);
            main.Config.ExpandAllCommand.Execute(null);

            SelectTab(window, 0);
            window.UpdateLayout();

            // Ruling 6: the Duration control belongs to the diagnostic mode, and
            // its first option is the default.
            Assert.Equal(new[] { "1 hour", "15 minutes", "Custom minutes…" },
                main.Config.DurationOptions);
            Assert.Equal(60.0, main.Config.TryBuildRunParameters()!.HorizonMinutes);

            Save(window, "phase-8o-horizon-diagnostic.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The calculations dialog after a run that used the WINDOW estimate, so the
    /// frame shows the estimate that was NOT applied as well as the one that was
    /// — the point being that a reader can see both numbers and which one ran.
    /// </summary>
    [AvaloniaFact]
    public void Render_CalculationsWindowLambda_SavePhase8oCalculationsPng()
    {
        var window = new MainWindow();
        window.Width = 1400;
        window.Height = 1800;
        window.Show();
        try
        {
            var main = RequireMain(window);
            var binding = DataAnalyzerBridge.Analyze(Sample("sample_multiday.csv"));

            main.Config.ApplyLoadedFile(Sample("sample_multiday.csv"));
            main.SelectedLambdaSource = LambdaSource.Window;

            // The fixture's rows all exit at Screening, so the fitted p_exit is
            // 1.0 and the coordinator refuses a three-stage network with no
            // downstream route. Overriding it is the action the refusal message
            // itself recommends, and it is what a user hitting that banner does.
            main.Config.ParametersIsOptionalEnabled = true;
            main.Config.PExit.Value = "0.4";

            // The fixture carries no doctor timings (its doctor cells are blank),
            // so the three default stages need μ for the stage the file does not
            // cover — the Path A fallback in AGENTS §19.3, step 2.
            main.Config.ManualMuPerStage.Value = "0.8, 0.5, 0.4";

            var parameters = main.Config.TryBuildRunParameters();
            Assert.NotNull(parameters);
            Assert.Equal(LambdaSource.Window, parameters!.LambdaSource);

            var outcome = Services.SimulationCoordinator.Run(parameters, binding);
            Assert.Null(outcome.Error);

            main.Results.StartRun();
            main.Results.CompleteRun(outcome, parameters, "fitted from sample_multiday.csv", binding);

            SelectTab(window, 0);
            window.UpdateLayout();

            var rows = main.Results.CalculationsRows;
            Assert.Contains(rows, r => r.Label == "λ — MLE");
            Assert.Contains(rows, r => r.Label == "λ — window");
            Assert.Contains(rows, r => r.Label == "Observation window");

            Save(window, "phase-8o-calculations.png");
        }
        finally
        {
            window.Close();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static MainViewModel RequireMain(Window window) =>
        window.DataContext as MainViewModel
        ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static void SelectTab(Window window, int index)
    {
        var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
        tabs.SelectedIndex = index;
        window.UpdateLayout();
    }

    private static void SelectInputTab(Window window) => SelectTab(window, 1);

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

/// <summary>
/// A thin alias so this file reads as a walkthrough rather than a service call.
/// </summary>
internal static class DataAnalyzerBridge
{
    internal static DataBindingResult Analyze(string path) =>
        Services.DataAnalyzer.Analyze(path);
}
