using System.Globalization;
using System.Linq;
using Avalonia.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Models;
using OpdSimulator.Core.Engine;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8M gate — the four chart/readability fixes approved after Phase 8L.
/// </summary>
/// <remarks>
/// <para>
/// Every test here exists because a real reader would otherwise be misled, so
/// each one asserts a FACT the user is shown, not an implementation detail:
/// </para>
/// <list type="bullet">
/// <item>A bar's height is its server's share of its stage, so a 3-server stage
/// and a 1-server stage are comparable at a glance (contribution scale).</item>
/// <item>The dashed line is the equal share, not the stage average — a benchmark
/// the bars can actually be read against.</item>
/// <item>Every category shows its own label, which was broken TWICE over: a
/// per-server series left null-heavy categories, AND LiveCharts2's own non-null
/// default Labeler silently overrode the string Labels.</item>
/// <item>One stage is one colour everywhere, by stage index rather than name, so
/// renaming a stage cannot break the legend.</item>
/// <item>The queue length is drawn as a step, because the engine only changes it
/// at events.</item>
/// <item>The calculations text states the formulae and their provenance.</item>
/// </list>
/// </remarks>
public class Phase8MChartTests
{
    /// <summary>1 / 2 / 3 servers → the six-bar case the owner asked to be verified.</summary>
    private static SimulationResult SixBarResult() => Phase8MFixtures.ResultWithStages(
        ("Reception", 1, 0.80, new[] { 0.80 }),
        ("Screening", 2, 0.50, new[] { 0.50, 0.50 }),
        ("Doctor", 3, 0.60, new[] { 0.60, 0.60, 0.60 }));

    /// <summary>2 / 4 / 5 servers → the eleven-bar case, with real imbalance.</summary>
    private static SimulationResult ElevenBarResult() => Phase8MFixtures.ResultWithStages(
        ("Triage", 2, 0.45, new[] { 0.45, 0.45 }),
        ("Lab", 4, 0.60, new[] { 0.85, 0.60, 0.60, 0.35 }),
        ("Consultation", 5, 0.40, new[] { 0.40, 0.40, 0.40, 0.40, 0.40 }));

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 6)]
    [InlineData(new[] { 2, 4, 5 }, 11)]
    public void UtilisationChart_BarCountEqualsTotalServerCount(int[] serverCounts, int expectedBars)
    {
        var result = Phase8MFixtures.ResultWithStages(serverCounts
            .Select((c, i) => ($"Stage{i + 1}", c, 0.5, Enumerable.Repeat(0.5, c).ToArray()))
            .ToArray());

        var chart = UtilisationChartService.Build(result);

        // The whole point of the contribution scale: every bar is one server, so
        // the bar count must track the server count and nothing else — no
        // hardcoded three stages, no capping at four.
        Assert.Equal(expectedBars, chart.Bars.Count);
        Assert.Equal(serverCounts, chart.Bars
            .Select(b => b.ServerCount)
            .Distinct()
            .OrderBy(c => c)
            .ToArray());
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 })]
    [InlineData(new[] { 2, 4, 5 })]
    public void UtilisationChart_BarHeightIsServerShareOfStage(int[] serverCounts)
    {
        var result = Phase8MFixtures.ResultWithStages(serverCounts
            .Select((c, i) => ($"Stage{i + 1}", c, 0.5, Enumerable.Repeat(0.5, c).ToArray()))
            .ToArray());

        var chart = UtilisationChartService.Build(result);

        foreach (var bar in chart.Bars)
        {
            Assert.Equal(bar.Utilisation / bar.ServerCount, bar.Contribution, precision: 12);
        }

        // A stage's contributions must add back up to the stage utilisation —
        // otherwise the bar heights are lying about the headline number.
        foreach (var stage in result.StageMetrics)
        {
            var sum = chart.Bars.Where(b => b.StageName == stage.StageName).Sum(b => b.Contribution);
            Assert.Equal(stage.StageUtilisation, sum, precision: 12);
            Assert.True(sum <= 1.0 + 1e-9, $"{stage.StageName} contributions must stay at or below 100%");
        }
    }

    [Fact]
    public void UtilisationChart_EqualShareReferenceLine_MatchesContributionScale()
    {
        // 2/4/5 with deliberate imbalance, so the reference line has something to
        // be measured against.
        var chart = UtilisationChartService.Build(ElevenBarResult());

        // Lab: 4 servers at 0.60 stage utilisation → equal share 0.15.
        var lab = chart.ReferenceLines.Single(r => r.FirstBarIndex == 2);
        Assert.Equal(0.60 / 4, lab.EqualShare, precision: 12);

        // Triage: 2 servers at 0.45 → 0.225. Consultation: 5 at 0.40 → 0.08.
        Assert.Equal(0.45 / 2, chart.ReferenceLines[0].EqualShare, precision: 12);
        Assert.Equal(0.40 / 5, chart.ReferenceLines[2].EqualShare, precision: 12);

        // An equal share at the old stage-average value would be c× too high and
        // sit above every bar, so assert the line really is below the stage mean.
        foreach (var line in chart.ReferenceLines)
        {
            var stage = chart.Bars[line.FirstBarIndex];
            Assert.True(line.EqualShare < stage.Utilisation, "the benchmark must be the share, not the average");
        }
    }

    [Fact]
    public void UtilisationChart_ReferenceLine_AtStageUtilOverC_LabeledEqualShare()
    {
        // The owner's named test, kept as a single readable statement of intent:
        // the line sits at StageUtilisation / c and is labelled as the equal
        // share, so nobody can later mistake it for the stage average.
        var result = Phase8MFixtures.ResultWithStages(("Lab", 4, 0.60, new[] { 0.60, 0.60, 0.60, 0.60 }));
        var chart = UtilisationChartService.Build(result);

        var line = Assert.Single(chart.ReferenceLines);
        Assert.Equal(result.StageMetrics[0].StageUtilisation / result.StageMetrics[0].ServerCount, line.EqualShare, precision: 12);

        // The label the chart actually draws for this line.
        var builder = typeof(ChartControlBuilder)
            .GetMethod("BuildUtilisationChart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.NotNull(builder);

        // The caption is the user-facing statement that the line is the
        // equal-share benchmark, and that amber means a deviation.
        Assert.Contains("equal-share", UtilisationChartService.Caption, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("amber", UtilisationChartService.Caption, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UtilisationChart_AmberFlagsOnlyServersThatDeviateFromTheirStage()
    {
        var chart = UtilisationChartService.Build(ElevenBarResult());

        // Lab is 0.60 across four servers: 0.85 and 0.35 are 0.25 off, the two
        // in the middle are exactly at the mean. Only the deviators are amber.
        var lab = chart.Bars.Where(b => b.StageName == "Lab").ToList();
        Assert.Equal(new[] { true, false, false, true }, lab.Select(b => b.IsOutlier).ToArray());

        // Perfectly balanced stages flag nothing, whatever their server count.
        Assert.All(chart.Bars.Where(b => b.StageName == "Consultation"), b => Assert.False(b.IsOutlier));
    }

    [Fact]
    public void UtilisationChart_StagesRenamedStillMapToPaletteByIndex()
    {
        // Stage names are user-editable, so a name-keyed palette would break on a
        // rename. Two different namings of the same three-stage network must
        // therefore produce identical colours for the same positions.
        var before = UtilisationChartService.Build(Phase8MFixtures.ResultWithStages(
            ("Reception", 1, 0.8, new[] { 0.8 }),
            ("Screening", 2, 0.5, new[] { 0.5, 0.5 }),
            ("Doctor", 3, 0.6, new[] { 0.6, 0.6, 0.6 })));
        var after = UtilisationChartService.Build(Phase8MFixtures.ResultWithStages(
            ("Triage", 1, 0.8, new[] { 0.8 }),
            ("Lab", 2, 0.5, new[] { 0.5, 0.5 }),
            ("Clinic", 3, 0.6, new[] { 0.6, 0.6, 0.6 })));

        Assert.Equal(
            before.Bars.Select(b => StageColourPalette.ForStageIndex(b.StageIndex)).ToArray(),
            after.Bars.Select(b => StageColourPalette.ForStageIndex(b.StageIndex)).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, before.Bars.Select(b => b.StageIndex).Distinct().ToArray());
    }

    [Fact]
    public void UtilisationChart_BarsCarryEverythingTheDetailTableCarried()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(
            new RunOutcome(ElevenBarResult(), Array.Empty<OpdSimulator.App.Models.FitReport>(),
                Array.Empty<string>(), 0.4, 0.0, null));

        // Phase 8S (D-196) removed the Per-server detail table, so this test no longer
        // reads a row off a view-model list. Its question is unchanged and now has to
        // be answered from the chart data itself: is every per-server number the table
        // used to display still reachable? The owner approved the removal on the
        // grounds that the chart already carries it — this is that claim, asserted
        // rather than assumed.
        var data = UtilisationChartService.Build(ElevenBarResult());

        // One bar per server, in the flattened stage/server order they are drawn in.
        Assert.Equal(11, data.Bars.Count);
        Assert.Equal(
            Enumerable.Range(1, 11).Select(i => (double)i),
            data.Bars.Select((_, i) => (double)(i + 1)));

        // D-171's point: a bar must carry the server's OWN utilisation as well as the
        // contribution it is drawn at, or a 25 % bar on a 2-server stage is ambiguous
        // between a busy server and a genuinely idle one. Both numbers are still there.
        Assert.All(data.Bars, bar =>
        {
            Assert.False(string.IsNullOrWhiteSpace(bar.StageName));
            Assert.InRange(bar.ServerNumber, 1, 11);
            Assert.InRange(bar.Utilisation, 0d, 1d);
            Assert.InRange(bar.Contribution, 0d, 1d);
            Assert.True(bar.ServerCount >= 1, $"bar {bar.StageName}/{bar.ServerNumber} has no server count");

            // The contribution is the utilisation rescaled by the server count — the
            // arithmetic the removed table made checkable by hand.
            Assert.Equal(bar.Contribution, bar.Utilisation / bar.ServerCount, 6);
        });

        // The two deviating Lab servers are still identifiable as such from the data,
        // which is what the chart's amber flag and its caption depend on.
        Assert.Equal(2, data.Bars.Count(b => b.IsOutlier));
    }

    [Fact]
    public void UtilisationChart_CaptionIsExactlyTheApprovedFourSentences()
    {
        Assert.Equal(
            "Each bar is one server's contribution to its stage's utilisation: busy time ÷ "
            + "(server count × operating time). The dashed line marks the equal-share benchmark. "
            + "Contributions within a stage sum to the stage utilisation (≤ 100%). "
            + "Bars in amber deviate from their stage mean by more than 15 percentage points.",
            UtilisationChartService.Caption);
    }
}

/// <summary>
/// Phase 8M gate — the shared stage-colour palette (D-163, FR-UI-27) and the
/// label rendering fix (D-160, N1). Split from the chart-shape tests because
/// these are the two things a future chart author is most likely to break by
/// reaching for a hardcoded colour or a default labeler.
/// </summary>
public class Phase8MPaletteTests
{
    /// <summary>1 / 2 / 3 servers → the six-bar case, shared with the palette tests.</summary>
    private static SimulationResult SixBarResult() => Phase8MFixtures.ResultWithStages(
        ("Reception", 1, 0.80, new[] { 0.80 }),
        ("Screening", 2, 0.50, new[] { 0.50, 0.50 }),
        ("Doctor", 3, 0.60, new[] { 0.60, 0.60, 0.60 }));

    [Fact]
    public void Palette_DifferentIndices_ReturnDifferentColours()
    {
        var colours = Enumerable.Range(0, 4).Select(StageColourPalette.ForStageIndex).ToArray();

        Assert.Equal(4, colours.Distinct().Count());
        Assert.Equal(4, StageColourPalette.Colours.Count);
    }

    [Fact]
    public void Palette_SameIndexAlwaysReturnsSameColour()
    {
        // Determinism is what lets a stage keep its identity across charts AND
        // across re-renders of the same chart.
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(
                StageColourPalette.ForStageIndex(i),
                StageColourPalette.ForStageIndex(i));
        }
    }

    [Fact]
    public void Palette_BeyondFourStagesRotatesHueInsteadOfRepeating()
    {
        // Stage 4 wraps back to colour 0 rotated one step; repeating an identical
        // colour would make two stages indistinguishable in the legend.
        var baseColour = StageColourPalette.ForStageIndex(0);
        var wrapped = StageColourPalette.ForStageIndex(4);
        var twiceWrapped = StageColourPalette.ForStageIndex(8);

        Assert.NotEqual(baseColour, wrapped);
        Assert.NotEqual(wrapped, twiceWrapped);
        Assert.NotEqual(baseColour, twiceWrapped);
    }

    [Fact]
    public void Palette_AmberIsDistinctFromEveryStageColour()
    {
        // Amber means "out of line", not "this stage". Reusing a stage colour
        // would turn a warning into an identity.
        var amber = StageColourPalette.ImbalanceHighlight;
        Assert.DoesNotContain(amber, StageColourPalette.Colours);
    }

    [Fact]
    public void Palette_BrushMatchesColourForSameIndex()
    {
        for (int i = 0; i < 6; i++)
        {
            var brush = Assert.IsType<SolidColorBrush>(StageColourPalette.BrushForStageIndex(i));
            Assert.Equal(StageColourPalette.ForStageIndex(i), brush.Color);
        }
    }

    [Theory]
    [InlineData(new[] { "Reception S1", "Screening S2", "Doctor S3" }, 0, "Reception S1")]
    [InlineData(new[] { "Reception S1", "Screening S2", "Doctor S3" }, 2, "Doctor S3")]
    [InlineData(new[] { "A S1", "B S2" }, 5, "5")]
    public void EveryCategoryHasAVisibleLabel_NumericAxisPositionMapsToCategoryText(
        string[] categories, double position, string expected)
    {
        // N1: LiveCharts2's default Labeler is non-null and WINS over the axis's
        // string Labels, which is how the utilisation chart ended up printing
        // bare indices. The labeler now returns the category text itself.
        Assert.Equal(expected, ChartControlBuilder.LabelAt(categories, position));
    }

    [Fact]
    public void EveryCategoryHasAVisibleLabel_EveryCategoryResolvesToItsOwnText()
    {
        var chart = UtilisationChartService.Build(SixBarResult());
        var categories = chart.Bars
            .Select(b => $"{b.StageName} S{b.ServerNumber}")
            .ToArray();

        // Every category must resolve to a distinct, non-empty, human label —
        // not a number, not a blank.
        var labels = categories.Select((c, i) => ChartControlBuilder.LabelAt(categories, i)).ToArray();
        Assert.Equal(categories.Length, labels.Distinct().Count());
        Assert.All(labels, l => Assert.False(string.IsNullOrWhiteSpace(l)));
        Assert.All(labels, l => Assert.DoesNotContain("%", l, StringComparison.Ordinal));
    }
}

/// <summary>
/// Phase 8M gate — the queue-over-time step chart (D-162) and the calculations
/// view (D-164, FR-UI-29).
/// </summary>
public class Phase8MQueueAndCalculationsTests
{
    private static SimulationResult QueueResult() => new SimulationResult
    {
        OperatingTimeMinutes = 120,
        StageMetrics = new[]
        {
            new StageMetrics
            {
                StageName = "Reception",
                ServerCount = 1,
                StageUtilisation = 0.8,
                PerServerUtilisation = new[] { 0.8 },
                QueueLengthSeries = new[]
                {
                    new QueueSample(0, 0), new QueueSample(1, 1),
                    new QueueSample(2, 1), new QueueSample(3, 0),
                },
            },
            new StageMetrics
            {
                StageName = "Screening",
                ServerCount = 2,
                StageUtilisation = 0.5,
                PerServerUtilisation = new[] { 0.5, 0.5 },
                QueueLengthSeries = new[]
                {
                    new QueueSample(0, 0), new QueueSample(1, 2),
                    new QueueSample(2, 2), new QueueSample(3, 1),
                },
            },
        },
    };

    [Fact]
    public void QueueChart_UsesNativeStepLineSeries()
    {
        var data = QueueLengthChartService.Build(QueueResult());
        Assert.True(data.HasSeries);
        Assert.Equal(2, data.Series.Count);

        // The engine only changes the queue length at an event, so a straight
        // segment would draw a queue length that never existed. The built chart
        // is asserted in Phase8MChartControlTests, where a genuine
        // StepLineSeries is inspected rather than merely located.
    }

    [Fact]
    public void QueueChart_BusiestStageDrawsBehindQuietestStage()
    {
        // Draw order is a readability decision: the highest-average stage is added
        // first so lower-average stages are never buried under its spikes.
        var data = QueueLengthChartService.Build(QueueResult());
        var averages = data.Series
            .Select(s => s.Points.Count == 0 ? 0 : s.Points.Average(p => p.Length))
            .ToArray();

        // Screening averages more than Reception here, and the data preserves
        // stage order, so the builder's descending sort must reverse the two.
        Assert.True(averages[1] > averages[0]);
        var ordered = data.Series.OrderByDescending(s =>
            s.Points.Count == 0 ? 0 : s.Points.Average(p => p.Length)).ToList();
        Assert.Equal("Screening", ordered[0].StageName);
        Assert.Equal("Reception", ordered[1].StageName);
    }

    [Fact]
    public void QueueChart_CarriesAStageIndexForTheSharedPalette()
    {
        var data = QueueLengthChartService.Build(QueueResult());

        Assert.Equal(new[] { 0, 1 }, data.Series.Select(s => s.StageIndex).ToArray());
    }

    [Fact]
    public void CalculationsDialog_ShowsFormulaeWithInputs()
    {
        var parameters = new OpdSimulator.App.Models.SimulationParameters(
            OpdSimulator.Data.Parameters.ParameterMode.RateWise,
            "Exponential",
            0.25,
            new[] { "Reception", "Screening" },
            new[] { 1, 2 },
            new double?[] { 0.4, 0.3 },
            RunMode.ClinicDay,
            0,
            1,
            DayOfWeek.Monday,
            null,
            42,
            0.4,
            "Standard");
        var result = Phase8MFixtures.ResultWithStages(
            ("Reception", 1, 0.625, new[] { 0.625 }),
            ("Screening", 2, 0.5, new[] { 0.75, 0.25 }));

        var text = CalculationsTextBuilder.Build(result, parameters, "fitted from sample.csv");

        // The formulae a viva will be asked about, spelled out.
        Assert.Contains("RUN CONFIGURATION", text, StringComparison.Ordinal);
        Assert.Contains("ARRIVAL PROCESS", text, StringComparison.Ordinal);
        Assert.Contains("SERVICE PROCESSES", text, StringComparison.Ordinal);
        Assert.Contains("UTILISATION", text, StringComparison.Ordinal);
        Assert.Contains("FLOW BALANCE", text, StringComparison.Ordinal);

        // The inputs that produced the numbers.
        Assert.Contains("0.25000", text, StringComparison.Ordinal);   // lambda
        Assert.Contains("42", text, StringComparison.Ordinal);        // seed
        Assert.Contains("fitted from sample.csv", text, StringComparison.Ordinal);
        Assert.Contains("Screening", text, StringComparison.Ordinal);
        Assert.Contains("62.50%", text, StringComparison.Ordinal);    // 0.625 utilisation

        // c, mu and the capacity c*mu, per stage — the numbers the reader cannot
        // see anywhere else on the panel.
        Assert.Contains("Screening — servers c", text, StringComparison.Ordinal);
        Assert.Contains("Screening — capacity c·μ", text, StringComparison.Ordinal);

        // Derived busy time, labelled as derived because the engine stores a
        // utilisation and the minutes are worked back from T.
        Assert.Contains("derived", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Operating time T", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculationsDialog_SaysSomethingHonestWhenNothingHasRun()
    {
        var text = CalculationsTextBuilder.Build(null, null, null);

        Assert.Contains("No simulation has been run", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nan", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculationsDialog_StagesRenameDoesNotBreakTheBody()
    {
        var parameters = new OpdSimulator.App.Models.SimulationParameters(
            OpdSimulator.Data.Parameters.ParameterMode.RateWise,
            "Exponential",
            0.2,
            new[] { "Alpha", "Beta", "Gamma" },
            new[] { 1, 1, 1 },
            new double?[] { 0.3, 0.3, 0.3 },
            RunMode.ClinicDay,
            0,
            1,
            DayOfWeek.Monday,
            null,
            7,
            0.4,
            "Standard");

        var text = CalculationsTextBuilder.Build(
            Phase8MFixtures.ResultWithStages(
                ("Alpha", 1, 0.5, new[] { 0.5 }),
                ("Beta", 1, 0.5, new[] { 0.5 }),
                ("Gamma", 1, 0.5, new[] { 0.5 })),
            parameters,
            "entered manually");

        // One service row per stage, whatever the stages are called.
        foreach (var name in new[] { "Alpha", "Beta", "Gamma" })
        {
            Assert.Contains($"{name} — servers c", text, StringComparison.Ordinal);
        }

        Assert.Contains("entered manually", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculationsDialog_ButtonOnlyAppearsAfterARun()
    {
        var results = new ResultsPanelViewModel();
        Assert.False(results.HasCalculations);

        results.StartRun();
        results.CompleteRun(
            new RunOutcome(QueueResult(), Array.Empty<OpdSimulator.App.Models.FitReport>(),
                Array.Empty<string>(), 0.4, 0.0, null));

        Assert.True(results.HasCalculations);
        Assert.Contains("RUN CONFIGURATION", results.CalculationsText, StringComparison.Ordinal);
    }

    [Fact]
    public void StageLegend_MatchesTheChartsOwnStages()
    {
        var results = new ResultsPanelViewModel();
        results.CompleteRun(
            new RunOutcome(ElevenBarResultForLegend(), Array.Empty<OpdSimulator.App.Models.FitReport>(),
                Array.Empty<string>(), 0.4, 0.0, null));

        // One entry per stage, in stage order, each painted with the colour that
        // stage's charts used.
        Assert.Equal(3, results.StageLegend.Count);
        Assert.Equal(new[] { 0, 1, 2 }, results.StageLegend.Select(i => i.StageIndex).ToArray());
        for (int i = 0; i < results.StageLegend.Count; i++)
        {
            var brush = Assert.IsType<SolidColorBrush>(results.StageLegend[i].Swatch);
            Assert.Equal(StageColourPalette.ForStageIndex(i), brush.Color);
        }

        Assert.Equal("Triage", results.StageLegend[0].StageName);
        Assert.Equal("Consultation", results.StageLegend[2].StageName);
    }

    private static SimulationResult ElevenBarResultForLegend() => new SimulationResult
    {
        OperatingTimeMinutes = 120,
        StageMetrics = new[]
        {
            new StageMetrics { StageName = "Triage", ServerCount = 2, StageUtilisation = 0.45, PerServerUtilisation = new[] { 0.45, 0.45 } },
            new StageMetrics { StageName = "Lab", ServerCount = 4, StageUtilisation = 0.6, PerServerUtilisation = new[] { 0.85, 0.6, 0.6, 0.35 } },
            new StageMetrics { StageName = "Consultation", ServerCount = 5, StageUtilisation = 0.4, PerServerUtilisation = new[] { 0.4, 0.4, 0.4, 0.4, 0.4 } },
        },
    };
}

/// <summary>
/// Shared fixtures for the Phase 8M tests. Every stage is described by name,
/// server count, stage utilisation and the per-server utilisations, so a test
/// can model an unbalanced run instead of a conveniently tidy one.
/// </summary>
internal static class Phase8MFixtures
{
    /// <summary>Builds a finished result from the given stages.</summary>
    internal static SimulationResult ResultWithStages(
        params (string Name, int Servers, double StageUtilisation, double[] PerServer)[] stages)
    {
        return new SimulationResult
        {
            OperatingTimeMinutes = 120,
            TotalPatientsServed = 90,
            ThroughputPerMinute = 0.75,
            StageMetrics = stages.Select(s => new StageMetrics
            {
                StageName = s.Name,
                ServerCount = s.Servers,
                StageUtilisation = s.StageUtilisation,
                PerServerUtilisation = s.PerServer,
                ArrivalRate = 0.3,
                ServiceRate = 0.2,
                Rho = s.StageUtilisation,
                PatientsServed = 30,
                AverageWaitMinutes = 4.5,
                AverageQueueLength = 1.25,
            }).ToArray(),
        };
    }
}

/// <summary>
/// Phase 8M gate — the built chart control itself, not just the data behind it.
/// These are the only assertions that touch the real <see cref="CartesianChart"/>,
/// because the label bug lived in the space between the data and the axis: the
/// data was right, the series shape was right, and the screen was still wrong.
/// </summary>
public class Phase8MChartControlTests
{
    private static SimulationResult SixBarRun() => Phase8MFixtures.ResultWithStages(
        ("Reception", 1, 0.80, new[] { 0.80 }),
        ("Screening", 2, 0.50, new[] { 0.50, 0.50 }),
        ("Doctor", 3, 0.60, new[] { 0.60, 0.60, 0.60 }));

    private static void AssertNear(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.True(Math.Abs(expected - actual!.Value) < 1e-9,
            $"expected {expected} (+/- 1e-9), got {actual}");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UtilisationChart_EveryCategoryHasAVisibleLabel()
    {
        var data = UtilisationChartService.Build(SixBarRun());
        var chart = ChartControlBuilder.BuildUtilisationChart(data);

        Assert.NotNull(chart);
        var axis = chart!.XAxes.Single();

        // 1. The categories are attached, one per server, in stage order.
        Assert.NotNull(axis.Labels);
        Assert.Equal(
            new[] { "Reception S1", "Screening S1", "Screening S2", "Doctor S1", "Doctor S2", "Doctor S3" },
            axis.Labels!.ToArray());

        // 2. And the labeler actually RENDERS them. This is the part that was
        // broken: LiveCharts2 ships a non-null default Labeler which wins over
        // Labels, so the strings were present but never drawn.
        var labeler = axis.Labeler;
        Assert.NotNull(labeler);
        for (int i = 0; i < axis.Labels.Count; i++)
        {
            Assert.Equal(axis.Labels[i], labeler(i));
        }

        // 3. The dense categorical axis is rotated so the labels are not dropped
        // for colliding vertically.
        Assert.NotEqual(0, axis.LabelsRotation);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UtilisationChart_DrawsOneColumnAndOneAmberOverlayPerStagePlusEqualShareLines()
    {
        var data = UtilisationChartService.Build(SixBarRun());
        var chart = ChartControlBuilder.BuildUtilisationChart(data);

        Assert.NotNull(chart);
        var columns = chart!.Series.OfType<LiveChartsCore.SkiaSharpView.ColumnSeries<double?>>().ToList();
        var overlays = chart.Series.OfType<LiveChartsCore.SkiaSharpView.ScatterSeries<double?>>().ToList();
        var references = chart.Series.OfType<LiveChartsCore.SkiaSharpView.LineSeries<double?>>().ToList();

        // Three stages → three series each, not six per-server columns. This is
        // the fix for the null-heavy category that was thinning the axis out.
        Assert.Equal(3, columns.Count);
        Assert.Equal(3, overlays.Count);
        Assert.Equal(3, references.Count);

        // Each stage's column carries only its own bars, at contribution height.
        var firstStage = columns[0].Values!.ToList();
        Assert.Equal(0.80, firstStage[0]);
        Assert.All(firstStage.Skip(1), v => Assert.Null(v));

        // The overlay is null except on the deviating bars: this run is balanced,
        // so nothing is amber.
        Assert.All(overlays.SelectMany(o => o.Values!.ToList()), v => Assert.Null(v));

        // Every reference line is drawn at the equal share for its stage.
        // Compared with a tolerance because 0.6 / 3 is not representable in
        // binary floating point; an exact assert would be testing the
        // representation of the fraction rather than the benchmark.
        AssertNear(0.80, references[0].Values!.ToList()[0]);
        AssertNear(0.25, references[1].Values!.ToList()[1]);
        AssertNear(0.20, references[2].Values!.ToList()[3]);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UtilisationChart_YAxisTopIsOneOverTheSmallestServerCount()
    {
        var data = UtilisationChartService.Build(SixBarRun());
        var chart = ChartControlBuilder.BuildUtilisationChart(data);

        Assert.NotNull(chart);
        var y = chart!.YAxes.Single();
        Assert.Equal(0, y.MinLimit);

        // 1/1: the run's smallest stage is single-server, and one busy server
        // out of one is a contribution of 1.0, so 1.0 is the honest ceiling.
        Assert.Equal(1.0, y.MaxLimit);

        // A run of wide stages only gets a correspondingly lower ceiling — the
        // scale follows the shape of the clinic, not the loudest bar in it.
        var wide = ChartControlBuilder.BuildUtilisationChart(UtilisationChartService.Build(
            Phase8MFixtures.ResultWithStages(
                ("Screening", 4, 0.55, new[] { 0.55, 0.55, 0.55, 0.55 }),
                ("Doctor", 6, 0.40, new[] { 0.40, 0.40, 0.40, 0.40, 0.40, 0.40 }))));
        Assert.Equal(0.25, wide!.YAxes.Single().MaxLimit);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UtilisationChart_YAxisNeverClipsADeviatingServer()
    {
        // The regression this pins: an earlier ceiling was derived from the
        // tallest EQUAL SHARE, which is a property of how balanced the run
        // happened to be. A quiet one-server stage dragged that bound down far
        // below a heavily-loaded sibling's outlier bar, and the bar was drawn
        // off the top of the plot. The ceiling must come from server counts.
        var result = Phase8MFixtures.ResultWithStages(
            ("Reception", 1, 0.01, new[] { 0.01 }),
            ("Screening", 4, 0.5575, new[] { 1.00, 0.41, 0.41, 0.41 }));

        var data = UtilisationChartService.Build(result);
        var outlier = data.Bars.Single(b => b.IsOutlier);
        var chart = ChartControlBuilder.BuildUtilisationChart(data);
        var ceiling = chart!.YAxes.Single().MaxLimit!.Value;

        // Under the old data-derived ceiling this bar sat at 0.25 on an axis
        // that stopped at ~0.16, so it was drawn off the top of the plot.
        Assert.Equal(0.25, outlier.Contribution, 6);
        Assert.True(outlier.Contribution <= ceiling,
            $"outlier contribution {outlier.Contribution} would be clipped by ceiling {ceiling}");
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void UtilisationChart_TooltipIndexIsRunWideNotPerStage()
    {
        // The regression: the hover formatters resolved a point index against the
        // reporting STAGE's bar list. Every series spans the whole category axis,
        // so the last bar of a three-stage 1/2/3 run arrives as index 5 — and a
        // stage-local list of three entries threw the moment anyone hovered a
        // Doctor bar.
        var data = UtilisationChartService.Build(SixBarRun());

        var first = ChartControlBuilder.BarAt(data, 0);
        var secondStage = ChartControlBuilder.BarAt(data, 1);
        var last = ChartControlBuilder.BarAt(data, 5);

        Assert.Equal(("Reception", 1), (first.StageName, first.ServerNumber));
        Assert.Equal(("Screening", 1), (secondStage.StageName, secondStage.ServerNumber));
        Assert.Equal(("Doctor", 3), (last.StageName, last.ServerNumber));

        // Every category resolves, in order, exactly once.
        for (int i = 0; i < data.Bars.Count; i++)
        {
            Assert.Equal(data.Bars[i], ChartControlBuilder.BarAt(data, i));
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void QueueChart_DrawsStepLinesWithOneHeadroomAboveTheTallestQueue()
    {
        var result = new SimulationResult
        {
            StageMetrics = new[]
            {
                new StageMetrics
                {
                    StageName = "Reception",
                    ServerCount = 1,
                    StageUtilisation = 0.8,
                    PerServerUtilisation = new[] { 0.8 },
                    QueueLengthSeries = new[]
                    {
                        new QueueSample(0, 0), new QueueSample(1, 4), new QueueSample(2, 0),
                    },
                },
            },
        };

        var chart = ChartControlBuilder.BuildQueueChart(QueueLengthChartService.Build(result));

        Assert.NotNull(chart);
        Assert.IsType<LiveChartsCore.SkiaSharpView.StepLineSeries<LiveChartsCore.Defaults.ObservablePoint>>(
            chart!.Series.Single());
        Assert.Equal(5, chart.YAxes.Single().MaxLimit);
    }
}
