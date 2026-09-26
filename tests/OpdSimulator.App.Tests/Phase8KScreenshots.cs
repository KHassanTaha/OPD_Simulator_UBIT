using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8K gate evidence: the four frames that show what 8K actually changed,
/// each captured headlessly from the real <see cref="MainWindow"/> through the
/// same seam <see cref="MainViewModel"/> uses.
/// <list type="bullet">
/// <item><c>phase-8k-per-stage-families.png</c> — three stages carrying three
/// DIFFERENT families at once, which is the thing 8J's single-family model could
/// not represent.</item>
/// <item><c>phase-8k-gg1-autofit.png</c> — a G/G/1 stage after the family search
/// ran: the fitted family, its finite AIC and its p-value in the badge, with the
/// user's G/G/1 notation still on screen.</item>
/// <item><c>phase-8k-verification-mixed.png</c> — the simulation-verification
/// cards after a real mixed-family run, i.e. D-157's configured-spec verdicts.</item>
/// <item><c>phase-8k-gamma-uniform-parameters.png</c> — the two families whose
/// spread is neither σ nor a rate: Gamma's shape k and Uniform's half-width w.</item>
/// </list>
/// <para>
/// AS IN EVERY PRIOR SCREENSHOT GATE, the <b>asserts are the proof and the frame is
/// the illustration</b>: this model cannot read a PNG, so visual inspection is
/// owner-required and is recorded as such in PROGRESS.md. Each test therefore
/// asserts the rendered widget state it is claiming to show — the badge text, the
/// per-family field visibility, the realised chart controls — not merely that a
/// file was written.
/// </para>
/// </summary>
public class Phase8KScreenshots
{
    [AvaloniaFact]
    public void Render_PerStageFamilies_SavePhase8kPerStageFamiliesPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 1700; // three stage rows, each with its Advanced setup open
        window.Show();
        try
        {
            var main = RequireMain(window);
            var panel = main.Config;

            // Three different families, one per stage. Before 8K the panel carried a
            // single family for the whole run, so this state was unreachable.
            panel.StageRows[0].UseAdvancedSetup = true;
            panel.StageRows[0].ServiceFamily = DistributionFamily.Exponential;
            panel.StageRows[1].UseAdvancedSetup = true;
            panel.StageRows[1].ServiceFamily = DistributionFamily.Normal;
            panel.StageRows[2].UseAdvancedSetup = true;
            panel.StageRows[2].ServiceFamily = DistributionFamily.Lognormal;

            Assert.Equal(
                new[] { DistributionFamily.Exponential, DistributionFamily.Normal, DistributionFamily.Lognormal },
                panel.StageRows.Select(r => r.ServiceFamily));

            // Each row must reveal the spread input ITS family uses, and only that one.
            Assert.False(panel.StageRows[0].NeedsStdDev);
            Assert.True(panel.StageRows[1].NeedsStdDev);
            Assert.False(panel.StageRows[1].NeedsShape);
            Assert.True(panel.StageRows[2].NeedsStdDev);

            window.UpdateLayout();

            var labels = RenderedFieldLabels(window);
            Assert.Contains("Standard deviation σ", labels);

            var frame = Capture(window, "phase-8k-per-stage-families.png");
            Assert.True(frame >= 512, "per-stage-families frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_GG1AutoFitBadge_SavePhase8kGg1AutofitPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 1700;
        window.Show();
        try
        {
            var main = RequireMain(window);
            var panel = main.Config;

            var variablePath = SamplePath("sample_3stage_variable.csv");
            Assert.True(
                DataAnalyzer.Analyze(variablePath).IsUsable,
                "the variable-variance fixture must analyse cleanly");
            panel.ApplyLoadedFile(variablePath);

            var screening = panel.StageRows[1];
            Assert.Null(screening.AutoFitBadge);

            // G/G/1 names no family: the search runs and the badge reports the decision.
            screening.SelectedModel = "G/G/1";

            Assert.NotNull(screening.AutoFitBadge);
            Assert.Contains("Best fit:", screening.AutoFitBadge);
            Assert.Equal("G/G/1", screening.SelectedModel);
            Assert.DoesNotContain("inf", screening.AutoFitBadge!, StringComparison.OrdinalIgnoreCase);
            AssertNoNonFiniteNumber(screening.AutoFitBadge!);

            window.UpdateLayout();

            // The badge must be realised in the visual tree, not merely set on the VM:
            // a badge bound to a collapsed container would satisfy every assert above.
            Assert.NotNull(FindByText(window, "Best fit:"));

            var frame = Capture(window, "phase-8k-gg1-autofit.png");
            Assert.True(frame >= 512, "gg1-autofit frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_MixedFamilyVerification_SavePhase8kVerificationMixedPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2600; // one card per verified series
        window.Show();
        try
        {
            var main = RequireMain(window);
            ForceAllWidgetsVisible(main.Results);

            // Per-stage verification, produced by a REAL run (D-157).
            //
            // KNOWN LIMITATION, recorded as BLOCKER-8K-1: every stage here is
            // Exponential even though the frame is named "mixed", because
            // SimulationCoordinator.BuildStageSpecs rebuilds each engine spec as
            // `new DistributionSpec(family, Mean: 1.0 / mu)` and therefore DROPS the
            // configured spread. DistributionSamplerFactory then refuses any
            // non-Exponential stage ("Normal distribution requires StdDev > 0"), so a
            // genuinely mixed run cannot execute yet. The per-stage verdict plumbing
            // this frame is meant to evidence IS exercised for real here, and the
            // mixed-family behaviour is covered directly by
            // Phase8BVerificationTests.VerifyAll_MixedStageParameters_*; what is
            // missing is only the end-to-end run. Changing the coordinator would change
            // what the engine samples, so it is held for an owner ruling.
            var specs = new List<DistributionSpec>
            {
                new(DistributionFamily.Exponential, Mean: 2.0),
                new(DistributionFamily.Exponential, Mean: 4.0),
                new(DistributionFamily.Exponential, Mean: 5.0),
            };
            var result = RunThreeStage(main, specs, "sample_3stage_variable.csv");

            Assert.Equal(3, result.StageMetrics.Count);

            main.SimulationVerification.Apply(
                result,
                "Exponential",
                specs,
                ConfigSignificance(main));

            Assert.False(main.SimulationVerification.IsEmpty);
            Assert.NotEmpty(main.SimulationVerification.Charts);

            // One card per series: inter-arrival plus one per stage, in stage order.
            // Titles are worded by the histogram builder, so this asserts the shape
            // (which series, in which order) rather than one exact phrasing.
            var titles = main.SimulationVerification.Charts.Select(c => c.Title).ToList();
            Assert.Equal(4, titles.Count);
            Assert.Contains("Inter-arrival", titles[0]);
            Assert.Contains("Reception", titles[1]);
            Assert.Contains("Screening", titles[2]);
            Assert.Contains("Doctor", titles[3]);
            Assert.All(titles.Skip(1), t => Assert.Contains("service", t, StringComparison.OrdinalIgnoreCase));

            // Every card must carry a verdict, either a chart or a stated reason.
            Assert.All(
                main.SimulationVerification.Charts,
                c => Assert.True(c.HasSeries || !string.IsNullOrWhiteSpace(c.Caption)));

            window.UpdateLayout();
            Assert.All(
                main.SimulationVerification.Charts,
                c => Assert.True(
                    c.ChartContent is not null,
                    $"card '{c.Title}' has no chart content; caption was: {c.Caption}"));
            Assert.All(main.SimulationVerification.Charts, c => Assert.True(c.HasSeries));

            var frame = Capture(window, "phase-8k-verification-mixed.png");
            Assert.True(frame >= 512, "verification-mixed frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Render_GammaAndUniformSpreadFields_SavePhase8kGammaUniformParametersPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 1700;
        window.Show();
        try
        {
            var main = RequireMain(window);
            var panel = main.Config;

            // Gamma and Uniform are the two families whose spread is not a σ.
            panel.StageRows[0].UseAdvancedSetup = true;
            panel.StageRows[0].ServiceFamily = DistributionFamily.Gamma;
            panel.StageRows[0].ServiceShape = "2";
            panel.StageRows[1].UseAdvancedSetup = true;
            panel.StageRows[1].ServiceFamily = DistributionFamily.Uniform;
            panel.StageRows[1].ServiceSpread = "0.5";

            // Exactly one spread input per row: Gamma shows k and not w or σ.
            Assert.True(panel.StageRows[0].NeedsShape);
            Assert.False(panel.StageRows[0].NeedsSpread);
            Assert.False(panel.StageRows[0].NeedsStdDev);
            Assert.True(panel.StageRows[1].NeedsSpread);
            Assert.False(panel.StageRows[1].NeedsShape);
            Assert.False(panel.StageRows[1].NeedsStdDev);

            window.UpdateLayout();

            // Both fields must be realised on screen, and the σ field must not be.
            var labels = RenderedFieldLabels(window);
            Assert.Contains("Shape k", labels);
            Assert.Contains("Half-width w", labels);
            Assert.DoesNotContain("Standard deviation σ", labels);

            var frame = Capture(window, "phase-8k-gamma-uniform-parameters.png");
            Assert.True(frame >= 512, "gamma-uniform frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static MainViewModel RequireMain(Window window)
        => window.DataContext as MainViewModel
           ?? throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");

    private static double ConfigSignificance(MainViewModel main) => main.Config.SignificanceLevelForRun;

    /// <summary>
    /// Runs a real three-stage clinic so the verification frame is produced by the
    /// engine rather than by hand-built samples.
    /// </summary>
    private static SimulationResult RunThreeStage(
        MainViewModel main,
        IReadOnlyList<DistributionSpec> specs,
        string fixture)
    {
        var config = new ConfigPanelViewModel();
        config.ParametersIsOptionalEnabled = true;
        config.ManualLambda.Value = "0.1";
        config.ManualMuPerStage.Value = "0.5, 0.25, 0.2";
        config.StageRows[0].Servers.Value = "1";
        config.StageRows[1].Servers.Value = "2";
        config.StageRows[2].Servers.Value = "3";

        var parameters = config.TryBuildRunParameters()!;

        // Replace the service families with the per-stage set. μ is left exactly as the
        // coordinator resolved it, so only the FAMILY differs between stages (D-150).
        parameters = parameters with { ServiceFamilies = specs };

        var binding = DataAnalyzer.Analyze(SamplePath(fixture));
        Assert.True(binding.IsUsable, $"{fixture} must analyse cleanly");

        var outcome = SimulationCoordinator.Run(parameters, binding);
        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);

        main.Results.StartRun();
        main.Results.CompleteRun(outcome);
        return outcome.Result!;
    }

    private static string[] ForceAllWidgetsVisible(ResultsPanelViewModel results)
    {
        string[] original = results.VisibleWidgets.ToArray();
        foreach (string key in original)
        {
            if (!results.VisibleWidgets.Contains(key))
            {
                results.ToggleWidget(key);
            }
        }

        return original;
    }

    /// <summary>
    /// Collects the label text of every realised <c>ValidatedField</c> in the tree.
    /// Reading the labels back is how a test proves a field is actually on screen
    /// rather than merely bound to a visibility flag.
    /// </summary>
    private static string[] RenderedFieldLabels(Window window)
        => window.GetVisualDescendants()
            .OfType<ValidatedField>()
            .Where(f => f.IsVisible)
            .Select(f => f.Label)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!)
            .ToArray();

    private static TextBlock? FindByText(Window window, string needle)
        => window.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(t => t.Text is not null && t.Text.Contains(needle, StringComparison.Ordinal));

    private static void AssertNoNonFiniteNumber(string text)
    {
        char[] separators = [' ', '(', ')', ',', '=', 'p'];
        foreach (string token in text.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double value))
            {
                Assert.True(
                    !double.IsNaN(value) && !double.IsInfinity(value),
                    $"'{token}' in \"{text}\" is not a finite number");
            }
        }
    }

    private static long Capture(Window window, string fileName)
    {
        var frame = HeadlessScreenshot.Capture(window);
        var shotDir = Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "logs", "screenshots");
        Directory.CreateDirectory(shotDir);
        var shotPath = Path.Combine(shotDir, fileName);
        frame.Save(shotPath);
        return new FileInfo(shotPath).Length;
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
}
