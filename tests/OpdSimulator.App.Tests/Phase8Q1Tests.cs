using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8Q.1 — the recorded server count and the historical utilisation computed
/// from it (D-178).
/// </summary>
/// <remarks>
/// <para>
/// The tests assert the ARITHMETIC and the BOUNDARIES, not merely that a number
/// appeared. A utilisation of some plausible value proves nothing if the same
/// value would appear with the formula deleted, so each test states the division
/// it expects and checks the figure against it.
/// </para>
/// <para>
/// No figure in this file is a literal from a data file. The fixtures are
/// constructed in-test with known service times, so the expected utilisation can
/// be written as the arithmetic it is.
/// </para>
/// </remarks>
public class HistoricalMetricsTests
{
    // ── The service ────────────────────────────────────────────────────────

    [Fact]
    public void HistoricalMetrics_ComputesStageUtilForGivenServerCount()
    {
        // 10 rows × 10 minutes = 100 minutes of recorded service. Over a
        // 100-minute observed window that is 100% at c = 1 and 50% at c = 2.
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Enumerable.Repeat(10.0, 10).ToArray());

        var at1 = HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 1 });
        var at2 = HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 2 });

        var one = Assert.Single(at1);
        Assert.Equal("Screening", one.StageName);
        Assert.Equal(100.0, one.TotalServiceMinutes, 6);
        Assert.Equal(100.0, one.OperatingMinutes, 6);
        Assert.Equal(100.0, one.AvailableServerMinutes, 6);

        // The figure is the stated division, checked rather than eyeballed.
        Assert.Equal(100.0 / (1 * 100.0), one.StageUtilisation, 9);
        Assert.Equal(100.0 / (2 * 100.0), Assert.Single(at2).StageUtilisation, 9);
    }

    [Fact]
    public void HistoricalMetrics_ServerCountDoubles_UtilisationHalves()
    {
        // The property, stated as a property: c is in the DENOMINATOR, so
        // doubling it halves the figure and nothing else changes.
        var binding = Binding(
            observedOperatingMinutes: 200.0,
            screeningService: Enumerable.Repeat(8.0, 25).ToArray()); // 200 min busy

        double at(int c) => Assert.Single(HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = c })).StageUtilisation;

        Assert.Equal(200.0 / 200.0, at(1), 9);        // 100%
        Assert.Equal(200.0 / 400.0, at(2), 9);        // 50%
        Assert.Equal(200.0 / 600.0, at(3), 9);        // 33.3%
        Assert.Equal(at(1) / 2.0, at(2), 9);
        Assert.Equal(at(1) / 3.0, at(3), 9);
    }

    [Fact]
    public void HistoricalMetrics_NoScreeningColumn_StageSkipped()
    {
        // A file with only a Doctor stage: stage detection never produces
        // "Screening", so asking for a Screening count must yield no Screening
        // row rather than a zero-service one.
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Array.Empty<double>(),
            doctorService: Enumerable.Repeat(5.0, 4).ToArray()); // 20 min

        Assert.DoesNotContain("Screening", binding.StageNames);

        var metrics = HistoricalMetricsService.ComputeFor(binding, new Dictionary<string, int>
        {
            ["Screening"] = 2,
            ["Doctor"] = 1,
        });

        var only = Assert.Single(metrics);
        Assert.Equal("Doctor", only.StageName);
        Assert.Equal(20.0 / 100.0, only.StageUtilisation, 9);
    }

    [Fact]
    public void HistoricalMetrics_StageWithoutServerCount_IsSkippedNotDefaulted()
    {
        // A stage with service times but no recorded count produces NO figure.
        // Defaulting to 1 would compute a utilisation from a number the user never
        // gave — the specific failure the input exists to prevent.
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Enumerable.Repeat(10.0, 10).ToArray());

        var metrics = HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int>());

        Assert.Empty(metrics);
    }

    [Fact]
    public void HistoricalMetrics_CountOfZeroOrNegative_IsSkipped()
    {
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Enumerable.Repeat(10.0, 10).ToArray());

        foreach (int bad in new[] { 0, -1 })
        {
            Assert.Empty(HistoricalMetricsService.ComputeFor(
                binding, new Dictionary<string, int> { ["Screening"] = bad }));
        }
    }

    [Fact]
    public void HistoricalMetrics_BusyTimeAboveCapacity_ReportsWarningAndDoesNotClamp()
    {
        // 250 minutes of service over a 100-minute window: 250% at c = 1. The raw
        // figure must survive (rulings 4) — clamping to 100% would hide exactly
        // the inconsistency the section exists to surface.
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Enumerable.Repeat(25.0, 10).ToArray());

        var metric = Assert.Single(HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 1 }));

        Assert.Equal(2.5, metric.StageUtilisation, 9);
        Assert.True(metric.ExceedsCapacity);

        // The warning names the likely causes and the remedy, per FR-UI-17.
        Assert.Contains("250.0 min of service", metric.CapacityWarning);
        Assert.Contains("more than 1 server(s) were open", metric.CapacityWarning);
        Assert.Contains("Raise the server count", metric.CapacityWarning);
    }

    [Fact]
    public void HistoricalMetrics_WithinCapacity_CarriesNoWarning()
    {
        var binding = Binding(
            observedOperatingMinutes: 100.0,
            screeningService: Enumerable.Repeat(5.0, 10).ToArray()); // 50 min → 50%

        var metric = Assert.Single(HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 1 }));

        Assert.False(metric.ExceedsCapacity);
        Assert.Equal("", metric.CapacityWarning);
    }

    // ── The operating-time basis (rulings 3) ───────────────────────────────

    [Fact]
    public void HistoricalMetrics_SessionDatesPresent_UsesObservedWindow()
    {
        var binding = Binding(
            observedOperatingMinutes: 495.0, // three sessions
            screeningService: Enumerable.Repeat(30.0, 10).ToArray()); // 300 min

        var (minutes, basis) = HistoricalMetricsService.OperatingTimeFor(binding);

        Assert.Equal(495.0, minutes);
        Assert.Equal(HistoricalOperatingBasis.ObservedWindow, basis);

        var metric = Assert.Single(HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 1 }));
        Assert.Equal(300.0 / 495.0, metric.StageUtilisation, 9);
    }

    [Fact]
    public void HistoricalMetrics_BasisLabel_NamesTheDivisorAndTheFallback()
    {
        // The observed label states the divisor, so a reader can divide the
        // printed minutes and land on the printed figure (D-176).
        var observed = HistoricalMetricsService.DescribeBasis(
            HistoricalOperatingBasis.ObservedWindow,
            new ObservationWindow(OperatingDays: 1, OperatingMinutes: 165.0));

        Assert.Contains("165 operating minutes", observed);
        Assert.Contains("the same divisor the simulation uses", observed);

        // The spanned label must SAY it is the fallback and why it differs.
        var spanned = HistoricalMetricsService.DescribeBasis(
            HistoricalOperatingBasis.SpannedWindow, null);

        Assert.Contains("single-session file", spanned);
        Assert.Contains("spanned window", spanned);
        Assert.Contains("not the clinic's operating time", spanned);

        Assert.Contains(
            "No operating time",
            HistoricalMetricsService.DescribeBasis(HistoricalOperatingBasis.None, null));
    }

    [Fact]
    public void HistoricalMetrics_NoSessionColumn_UsesSpannedWindow()
    {
        // Arrivals 08:00→10:00, last service completion 11:00, so the spanned
        // window is 180 minutes — and it is NOT 165, which is why the label
        // exists.
        var binding = Binding(
            observedOperatingMinutes: null,
            screeningService: Enumerable.Repeat(20.0, 9).ToArray(), // 180 min
            firstArrival: 8 * 60.0,
            lastServiceEnd: 11 * 60.0);

        var (minutes, basis) = HistoricalMetricsService.OperatingTimeFor(binding);

        Assert.Equal(180.0, minutes);
        Assert.Equal(HistoricalOperatingBasis.SpannedWindow, basis);
    }

    [Fact]
    public void HistoricalMetrics_NoTimesAtAll_ReportsNoBasis()
    {
        var binding = Binding(
            observedOperatingMinutes: null,
            screeningService: Array.Empty<double>(),
            firstArrival: null,
            lastServiceEnd: null);

        var (minutes, basis) = HistoricalMetricsService.OperatingTimeFor(binding);

        Assert.Null(minutes);
        Assert.Equal(HistoricalOperatingBasis.None, basis);
        Assert.Empty(HistoricalMetricsService.ComputeFor(
            binding, new Dictionary<string, int> { ["Screening"] = 1 }));
    }

    // ── The Input tab's fields ─────────────────────────────────────────────

    [Fact]
    public void InputTab_ServerCountFields_PerDetectedStage()
    {
        var tab = new InputTabViewModel();
        Assert.False(tab.HasServerCounts);

        // Three detected stages → three fields, in the file's stage order.
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        Assert.True(tab.HasServerCounts);
        Assert.Equal(
            new[] { "Reception", "Screening", "Doctor" },
            tab.ServerCounts.Select(r => r.StageName).ToArray());

        // Each defaults to 1 and is immediately usable.
        Assert.All(tab.ServerCounts, r => Assert.Equal(1, r.ParsedServers));
        Assert.All(tab.ServerCounts, r => Assert.False(r.HasError));
        Assert.Equal(3, tab.ValidServerCounts().Count);
    }

    [Fact]
    public void InputTab_ServerCounts_EmptySectionWithoutFile()
    {
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));
        Assert.NotEmpty(tab.ServerCounts);

        tab.Clear();

        Assert.Empty(tab.ServerCounts);
        Assert.False(tab.HasServerCounts);
        Assert.Empty(tab.ValidServerCounts());
        Assert.False(tab.HistoricalMetrics.HasMetrics);
    }

    [Fact]
    public void InputTab_ServerCountFields_FollowTheLoadedFileNotAHardcodedList()
    {
        // The clinic capture has no reception columns, so it must offer two
        // fields, not three. This is the test that would fail if the fields were
        // built from a fixed stage list.
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_bypass.csv")));

        Assert.Equal(
            new[] { "Reception", "Screening", "Doctor" },
            tab.ServerCounts.Select(r => r.StageName).ToArray());
    }

    [Fact]
    public void InputTab_ServerCountRejects_NonPositiveAndNonInteger()
    {
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        var screening = tab.ServerCounts.Single(r => r.StageName == "Screening");

        foreach (string bad in new[] { "0", "-2", "two", "", "1.5" })
        {
            screening.Servers = bad;
            screening.Validate();

            Assert.True(screening.HasError, $"'{bad}' should be rejected");
            Assert.Null(screening.ParsedServers);

            // Cause AND expected form (FR-UI-17).
            Assert.Contains("whole number of at least 1", screening.ErrorMessage);
            Assert.Contains($"You entered \"{bad}\"", screening.ErrorMessage);
        }

        // And a corrected entry clears the error immediately.
        screening.Servers = "3";
        Assert.False(screening.HasError);
        Assert.Equal("", screening.ErrorMessage);
        Assert.Equal(3, screening.ParsedServers);
    }

    [Fact]
    public void InputTab_HistoricalUtilisation_RecomputesWhenACountChanges()
    {
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        var atOne = tab.HistoricalMetrics.Stages.Single(r => r.StageName == "Screening").Utilisation;
        tab.ServerCounts.Single(r => r.StageName == "Screening").Servers = "4";
        var atFour = tab.HistoricalMetrics.Stages.Single(r => r.StageName == "Screening").Utilisation;

        // Editing the historical field moves the historical figure.
        Assert.Equal(atOne / 4.0, atFour, 9);
    }

    [Fact]
    public void InputTab_InvalidCount_OmitsTheStageRatherThanDefaultingIt()
    {
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        var screening = tab.ServerCounts.Single(r => r.StageName == "Screening");
        screening.Servers = "0";

        // The stage drops out of the computed figures entirely: no fabricated
        // figure from a count the user has said is invalid.
        Assert.DoesNotContain(tab.HistoricalMetrics.Stages, r => r.StageName == "Screening");
        Assert.False(screening.HasError); // not yet validated on blur
    }

    [Fact]
    public void InputTab_HistoricalUtilisation_DivisorsAreShownWithEachFigure()
    {
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        Assert.True(tab.HistoricalMetrics.HasMetrics);
        Assert.NotEmpty(tab.HistoricalMetrics.BasisText);

        foreach (var row in tab.HistoricalMetrics.Stages)
        {
            // The reader must be able to reproduce the figure from the row alone:
            // the busy minutes, the server count and the operating minutes are all
            // printed, and the line is the division of them.
            Assert.Contains("÷", row.CalculationText);
            Assert.Contains($"{row.ServerCount} ×", row.CalculationText);
            Assert.Contains(
                row.TotalServiceMinutes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                row.CalculationText);
            Assert.Contains(
                row.OperatingMinutes.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                row.CalculationText);

            // ...and it must actually be the division it claims to be.
            Assert.Equal(
                row.TotalServiceMinutes / (row.ServerCount * row.OperatingMinutes),
                row.Utilisation,
                9);
        }

        // The per-server assumption is stated, not implied.
        Assert.Contains("server-ID columns are not present", tab.HistoricalMetrics.PerServerNote);
    }

    // ── Seeding into the Stages section (rulings 6) ────────────────────────

    [Fact]
    public void InputTab_ServerCounts_SeedConfigStages_OnUseForSimulation()
    {
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));
        config.SyncStagesToData();

        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));

        // The user records two screening tables, one doctor.
        tab.ServerCounts.Single(r => r.StageName == "Screening").Servers = "2";
        tab.ServerCounts.Single(r => r.StageName == "Doctor").Servers = "1";
        tab.ServerCounts.Single(r => r.StageName == "Reception").Servers = "1";

        bool fired = false;
        tab.UseForSimulationRequested += (_, _) => fired = true;
        tab.UseForSimulationCommand.Execute(null);

        Assert.True(fired);
        Assert.NotNull(tab.SeedableServerCounts);

        int applied = config.SeedServerCountsFromHistory(tab.SeedableServerCounts!);

        Assert.Equal(3, applied);
        Assert.Equal("2", config.StageRows.Single(r => r.StageName == "Screening").Servers.Value);
        Assert.Equal("1", config.StageRows.Single(r => r.StageName == "Doctor").Servers.Value);
    }

    [Fact]
    public void Config_StagesSeed_ThenInputEditsDoNotRewriteThem()
    {
        // The separation ruling 6 exists for: after seeding, the user hand-tunes
        // the Stages section, and a later Input-tab edit must not undo it.
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));
        config.SyncStagesToData();

        var tab = new InputTabViewModel();
        tab.SetLoadedFile(DataAnalyzerBridge.Analyze(Sample("sample_3stage_clinic.csv")));
        tab.ServerCounts.Single(r => r.StageName == "Screening").Servers = "2";

        tab.UseForSimulationCommand.Execute(null);
        config.SeedServerCountsFromHistory(tab.SeedableServerCounts!);
        tab.AcknowledgeSeededServerCounts();

        // The user then changes the SIMULATION count by hand.
        config.StageRows.Single(r => r.StageName == "Screening").Servers.Value = "5";

        // Editing the historical field changes nothing in Stages.
        tab.ServerCounts.Single(r => r.StageName == "Screening").Servers = "7";
        Assert.Equal("5", config.StageRows.Single(r => r.StageName == "Screening").Servers.Value);

        // And a second click with no intervening seed carries no counts, so it
        // cannot overwrite the hand-tuned value either.
        Assert.Null(tab.SeedableServerCounts);
    }

    [Fact]
    public void Config_SeedServerCounts_LeavesStagesWithoutARecordedCountAlone()
    {
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));
        config.SyncStagesToData();
        config.StageRows.Single(r => r.StageName == "Doctor").Servers.Value = "4";

        // Only Screening has a recorded count.
        int applied = config.SeedServerCountsFromHistory(
            new Dictionary<string, int> { ["Screening"] = 3 });

        Assert.Equal(1, applied);
        Assert.Equal("3", config.StageRows.Single(r => r.StageName == "Screening").Servers.Value);

        // The untouched row keeps the user's own value — not reset to 1.
        Assert.Equal("4", config.StageRows.Single(r => r.StageName == "Doctor").Servers.Value);
    }

    [Fact]
    public void Config_SeedServerCounts_IgnoresCountsBelowOne()
    {
        var config = new ConfigPanelViewModel();
        config.ApplyLoadedFile(Sample("sample_3stage_clinic.csv"));
        config.SyncStagesToData();
        config.StageRows.Single(r => r.StageName == "Screening").Servers.Value = "2";

        int applied = config.SeedServerCountsFromHistory(new Dictionary<string, int>
        {
            ["Screening"] = 0,
            ["Doctor"] = 2,
        });

        Assert.Equal(1, applied);
        Assert.Equal("2", config.StageRows.Single(r => r.StageName == "Screening").Servers.Value);
        Assert.Equal("2", config.StageRows.Single(r => r.StageName == "Doctor").Servers.Value);
    }

    // ── Fixture integrity ──────────────────────────────────────────────────

    [Fact]
    public void SampleBypassFixture_CarriesBypassRowsAndKeepsTheRestValid()
    {
        // sample_bypass.csv is committed for CI (the real clinic capture is not),
        // so the shape 8Q.2 will detect is locked here.
        var binding = DataAnalyzerBridge.Analyze(Sample("sample_bypass.csv"));

        Assert.Null(binding.ErrorMessage);

        // 8Q.2 closed this gap. The validator now reads a blank screening cell on
        // a Doctor-departure row as a BYPASS, not a missing value, because the
        // filled doctor pair is the evidence that the patient really did reach a
        // doctor. Before 8Q.2 these four rows were reported as 8 errors and the
        // whole file was unusable — which meant the clinic's direct-to-doctor
        // traffic could not be loaded at all.
        Assert.Empty(binding.Issues);
        Assert.True(binding.IsUsable);

        // The measurement must agree with the rows it counted.
        Assert.Equal(4, binding.BypassExits);
        Assert.Equal(20 - 4, binding.ScreenedPatients);
        Assert.Equal(0.20, binding.FittedBypassProbability!.Value, 6); // 4 of 20 arrivals

        var rows = binding.DataSet!.Rows;
        Assert.Equal(20, rows.Count);

        // A bypass row is the shape 8Q.2 will detect: no screening_start, a
        // filled doctor pair, and departure_stage = Doctor.
        int bypass = rows.Count(r =>
            !(r.TryGetValue("screening_start", out string? s) && !string.IsNullOrWhiteSpace(s))
            && r.TryGetValue("doctor_start", out string? d) && !string.IsNullOrWhiteSpace(d)
            && r.TryGetValue("departure_stage", out string? stage)
            && string.Equals(stage?.Trim(), "Doctor", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(4, bypass);

        // The rest of the file still carries screening times, so the fixture does
        // not degenerate into an all-bypass file.
        Assert.Equal(16, rows.Count(r =>
            r.TryGetValue("screening_start", out string? s) && !string.IsNullOrWhiteSpace(s)));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a binding with known service times, so an expected utilisation can
    /// be written as the arithmetic it is rather than as a captured number.
    /// </summary>
    private static DataBindingResult Binding(
        double? observedOperatingMinutes,
        double[] screeningService,
        double[]? doctorService = null,
        double? firstArrival = null,
        double? lastServiceEnd = null)
    {
        var stageNames = new List<string>();
        var fitted = new List<double>();
        var serviceByStage = new Dictionary<string, IReadOnlyList<double>>(StringComparer.OrdinalIgnoreCase);

        if (screeningService.Length > 0)
        {
            stageNames.Add("Screening");
            fitted.Add(1.0 / screeningService.Average());
            serviceByStage["Screening"] = screeningService;
        }

        if (doctorService is { Length: > 0 })
        {
            stageNames.Add("Doctor");
            fitted.Add(1.0 / doctorService.Average());
            serviceByStage["Doctor"] = doctorService;
        }

        var dataSet = new OpdSimulator.Data.Loaders.DataSet(
            sourcePath: "synthetic",
            loadedAt: DateTime.UnixEpoch,
            columns: new[] { "arrival_time", "screening_start", "screening_end", "doctor_start", "doctor_end" },
            rows: BuildRows(screeningService, doctorService, firstArrival, lastServiceEnd));

        return new DataBindingResult(
            SourcePath: null,
            DataSet: dataSet,
            Issues: Array.Empty<OpdSimulator.Data.Validation.ValidationIssue>(),
            ErrorMessage: null,
            FittedArrivalRate: 0.5,
            StageNames: stageNames,
            FittedServiceRates: fitted,
            FittedExitProbability: 0.5,
            ScreeningExits: 1,
            DoctorExits: 1,
            ReceptionExcluded: 0,
            InterArrivalMinutes: Array.Empty<double>(),
            ServiceMinutesByStage: serviceByStage)
        {
            ObservedWindow = observedOperatingMinutes is > 0
                ? new ObservationWindow(1, observedOperatingMinutes.Value)
                : null,
        };
    }

    /// <summary>
    /// Formats minutes-since-midnight as HH:MM.
    /// </summary>
    /// <remarks>
    /// Zero-padded to two digits because <c>TimeParser</c> requires it: "8:0"
    /// does not parse, and a helper that emitted it would produce a synthetic
    /// fixture the loader rejects — a failure that looks like a service bug and
    /// is not one.
    /// </remarks>
    private static string Clock(double minutes) =>
        $"{(int)(minutes / 60):D2}:{(int)(minutes % 60):D2}";

    private static List<Dictionary<string, string>> BuildRows(
        double[] screening,
        double[]? doctor,
        double? firstArrival,
        double? lastServiceEnd)
    {
        var rows = new List<Dictionary<string, string>>();
        double clock = firstArrival ?? 8 * 60.0;
        int total = Math.Max(screening.Length, doctor?.Length ?? 0);

        for (int i = 0; i < total; i++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (firstArrival is not null || lastServiceEnd is not null)
            {
                row["arrival_time"] = Clock(clock);
            }

            if (i < screening.Length)
            {
                row["screening_start"] = Clock(clock);
                clock += screening[i];
                row["screening_end"] = Clock(clock);
            }

            if (doctor is not null && i < doctor.Length)
            {
                clock += 1;
                row["doctor_start"] = Clock(clock);
                clock += doctor[i];
                row["doctor_end"] = Clock(clock);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string Sample(string fileName) =>
        Path.Combine(FindRepoRoot(AppContext.BaseDirectory), "samples", fileName);

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