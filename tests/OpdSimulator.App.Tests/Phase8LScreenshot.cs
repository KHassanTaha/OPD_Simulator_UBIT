using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8L gate evidence: <c>phase-8l-dropdown-rebind.png</c>, captured headlessly
/// from the real <see cref="MainWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// The frame shows the Input tab after a three-stage file is loaded with three
/// DIFFERENT configured service families, which is the visible payoff of 8L: the
/// per-stage cards are now tested against their own stage's model instead of one
/// family for the whole run.
/// </para>
/// <para>
/// AS IN EVERY PRIOR SCREENSHOT GATE, the <b>asserts are the proof and the frame
/// is the illustration</b>: this model cannot read a PNG, so visual inspection is
/// owner-required and is recorded as such in PROGRESS.md. The test therefore
/// asserts the rendered widget state it claims to show — the labels actually
/// present in the visual tree, the per-stage fit families, the new stage's
/// inherited default — not merely that a file was written.
/// </para>
/// </remarks>
public class Phase8LScreenshot
{
    [AvaloniaFact]
    public void Render_PerStageFamiliesOnInputTab_SavePhase8lDropdownRebindPng()
    {
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 2000; // preview banner + preview table + fit cards
        window.Show();
        try
        {
            if (window.DataContext is not MainViewModel main)
            {
                throw new InvalidOperationException("MainWindow must expose a MainViewModel DataContext");
            }

            var panel = main.Config;

            // ── The Model section must offer exactly ONE service-family control ──
            // The defect: it also offered a "Service distribution" dropdown bound to
            // a property nothing read. Rendered labels are the honest check, because
            // a binding that is present in XAML but visually hidden would still pass
            // a source grep.
            var modelLabels = RenderedDropdownLabels(window);
            Assert.DoesNotContain("Service distribution", modelLabels);
            int serviceFamilyDropdowns = modelLabels
                .Count(l => l.Contains("service family", StringComparison.OrdinalIgnoreCase));
            Assert.True(
                serviceFamilyDropdowns == 1,
                $"exactly one service-family dropdown may exist, and it is the default-for-new-stages one; " +
                $"found {serviceFamilyDropdowns} in [{string.Join(", ", modelLabels)}]");

            // ── Ruling 2, asserted on the model: the default seeds new stages only ──
            panel.StageRows[0].ServiceFamily = DistributionFamily.Exponential;
            panel.StageRows[1].ServiceFamily = DistributionFamily.Normal;
            panel.StageRows[2].ServiceFamily = DistributionFamily.Lognormal;

            // The default differs from two of the three rows, so a bug that rewrote
            // existing rows would be visible in the assertions below.
            panel.DefaultStageServiceFamily = DistributionFamily.Uniform;

            Assert.Equal(DistributionFamily.Exponential, panel.StageRows[0].ServiceFamily);
            Assert.Equal(DistributionFamily.Normal, panel.StageRows[1].ServiceFamily);
            Assert.Equal(DistributionFamily.Lognormal, panel.StageRows[2].ServiceFamily);

            // A stage added now inherits the new default — the dropdown's only job.
            panel.StageCount.Value = "4";
            Assert.Equal(4, panel.StageRows.Count);
            Assert.Equal(DistributionFamily.Uniform, panel.StageRows[3].ServiceFamily);

            // Back to three, so the frame shows a config that matches the fixture's
            // three stages. Dropping the extra row must not disturb the others.
            panel.StageCount.Value = "3";
            Assert.Equal(3, panel.StageRows.Count);
            Assert.Equal(
                new[] { DistributionFamily.Exponential, DistributionFamily.Normal, DistributionFamily.Lognormal },
                panel.StageRows.Select(r => r.ServiceFamily));

            // ── The Input tab must show each stage against its OWN family ─────────
            var binding = DataAnalyzer.Analyze(SamplePath("sample_3stage_variable.csv"));
            Assert.True(binding.IsUsable, "the three-stage fixture must analyse cleanly for the screenshot");
            main.InputTab.SetLoadedFile(binding);
            main.InputAnalysis.Apply(
                binding,
                "Exponential",
                panel.StageServiceFamilies,
                panel.SignificanceLevelForRun);

            // The list the APP hands the tab — panel rows straight through
            // StageServiceFamilies — must carry three DISTINCT families. Asserting
            // the property the UI reads is the point of this frame: it is the seam
            // where 8L's per-stage mapping could have been quietly collapsed back
            // into one global value, and where nothing else in the UI would notice.
            var handed = panel.StageServiceFamilies;
            Assert.Equal(3, handed.Count);
            Assert.Equal(3, handed.Select(f => (int)f.Family).Distinct().Count());

            // One histogram + one chi-square card per fitted series (inter-arrival
            // plus three stages = 4 series = 8 cards), and the service cards are
            // named per stage rather than repeating one global label.
            Assert.Equal(8, main.InputAnalysis.Charts.Count);

            var serviceTitles = main.InputAnalysis.Charts
                .Select(c => c.Title)
                .Where(t => t.Contains("service", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Two cards per stage — histogram and chi-square — so a stage that lost
            // its own identity (one repeated label) would show up as a count or a
            // per-stage tally that does not match.
            Assert.Equal(6, serviceTitles.Count);
            foreach (var stage in new[] { "Reception", "Screening", "Doctor" })
            {
                Assert.Equal(
                    2,
                    serviceTitles.Count(t => t.Contains(stage, StringComparison.OrdinalIgnoreCase)));
            }

            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();

            // First look at the Input tab: the per-stage cards are the functional
            // payoff, so their state is checked while the tab is actually on screen.
            tabs.SelectedIndex = 1; // Input
            window.UpdateLayout();

            var analysis = window.GetVisualDescendants().OfType<InputAnalysisView>().Single();
            Assert.True(analysis.IsEffectivelyVisible, "the fit analysis must be on screen");

            // The saved frame is the Simulation tab, because the phase is named for
            // the rebind: a reviewer must be able to SEE that the Model section now
            // offers one service-family control and that three rows carry three
            // different families. The Input tab's correctness is asserted above and
            // in Phase8LTests; a single PNG cannot show both tabs.
            tabs.SelectedIndex = 0; // Simulation
            panel.IsModelSectionExpanded = true;
            panel.IsStagesSectionExpanded = true;
            window.UpdateLayout();

            int visibleServiceDropdowns = window.GetVisualDescendants()
                .OfType<SearchableDropdown>()
                .Count(d => d.IsEffectivelyVisible
                    && d.Label is not null
                    && d.Label.Contains("service family", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1, visibleServiceDropdowns);

            var bytes = Capture(window, "phase-8l-dropdown-rebind.png");
            Assert.True(bytes >= 512, "phase-8l frame missing or suspiciously small");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// The <c>Label</c> of every service-family dropdown currently in the visual
    /// tree. Uses the rendered control rather than the XAML so a control that is
    /// present in source but not shown cannot pass this gate.
    /// </summary>
    private static string[] RenderedDropdownLabels(Window window)
        => window.GetVisualDescendants()
            .OfType<OpdSimulator.App.Controls.SearchableDropdown>()
            .Where(d => d.IsEffectivelyVisible)
            .Select(d => d.Label)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l!)
            .ToArray();

    /// <summary>Writes the frame under <c>logs/screenshots</c> and returns its size.</summary>
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

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException($"sample file not found above the test output directory: {fileName}");
    }
}
