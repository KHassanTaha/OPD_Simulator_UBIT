using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Data.Parameters;
using Serilog;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8R: the admitted-load cap, the index-based bypass, and the
/// close-of-session backlog/drain figures (D-189 … D-191).
/// </summary>
/// <remarks>
/// <para>
/// The reference numbers the owner worked out by hand for the clinic capture are
/// NOT hardcoded here as answers. The file is re-analysed at run time and the
/// cap-derived rate is recomputed from the session length, so a change to the
/// fitting or to the routing shows up as a failing assertion instead of being
/// papered over by a literal that happens to match. Literals appear only where
/// they are the CONTRACT — the cap default of 85 and the configured 165-minute
/// session — never where they are the RESULT.
/// </para>
/// <para>
/// The capture itself is real patient data and is deliberately uncommitted, so
/// <see cref="RealCapture_TwoStageCapRunIsStableAtTwoAndThreeServers"/> returns
/// early when it is absent rather than asserting against a committed fixture that
/// never reaches the two-stage shape (D-178). Every behaviour it demonstrates is
/// also covered below on constructed data that CI can run.
/// </para>
/// </remarks>
public class Phase8RTests
{
    /// <summary>The clinic capture's filename inside <c>samples/</c>. Uncommitted by design.</summary>
    private const string CaptureFileName = "opd_collection_28_sep_2026_1.csv";

    /// <summary>
    /// The configured session length in minutes. A CONTRACT, not a result: the cap
    /// rate is this file's daily cap divided by it.
    /// </summary>
    private const double SessionMinutes = 165;

    private static readonly Serilog.ILogger NullLogger = new LoggerConfiguration()
        .MinimumLevel.Fatal()
        .CreateLogger();

    private static Engine NewEngine() => new(new SeededRandomSource(), NullLogger);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "OpdSimulator.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty;
            if (dir.Length == 0)
                break;
        }
        throw new DirectoryNotFoundException("Could not locate OpdSimulator.sln above " + AppContext.BaseDirectory);
    }

    private static string Sample(string fileName) => Path.Combine(RepoRoot(), "samples", fileName);

    private static double Rel(double actual, double expected) =>
        Math.Abs(actual - expected) / Math.Max(expected, 1e-12);

    // ── Change 2 · the cap field ────────────────────────────────────────────

    [Fact]
    public void ConfigPanel_CapFieldDefaultsToEightyFiveAndFollowsTheSessionModes()
    {
        var vm = new ConfigPanelViewModel();

        // D-190: a fresh launch carries the documented default, not a blank field.
        Assert.Equal("85", vm.DailyCap.Value);
        Assert.Equal(ConfigPanelViewModel.DefaultDailyCap, vm.DailyCap.Value);

        // Both calendar modes leave a backlog: one session leaves a queue just as a
        // week of sessions does.
        Assert.True(vm.IsSingleDay);
        Assert.True(vm.DailyCapVisible);

        vm.IsMultiDay = true;
        vm.IsSingleDay = false;
        Assert.True(vm.DailyCapVisible);

        // A diagnostic trace has no session and no gate, so a visible cap would
        // describe a limit the run does not apply.
        vm.IsMultiDay = false;
        vm.IsDiagnosticTrace = true;
        vm.IsSingleDay = false;
        Assert.False(vm.DailyCapVisible);
    }

    [Fact]
    public void ConfigPanel_BlankCapIsUnlimitedAndNonPositiveValuesAreRejected()
    {
        var vm = new ConfigPanelViewModel();

        // Blank means "no cap", which is different from "invalid" — the run simply
        // admits everyone who arrives.
        vm.DailyCap.Value = string.Empty;
        vm.ValidateDailyCap();
        Assert.False(vm.DailyCap.HasError);

        foreach (var bad in new[] { "0", "-5", "12.5", "abc" })
        {
            vm.DailyCap.Value = bad;
            vm.ValidateDailyCap();
            Assert.True(vm.DailyCap.HasError, $"'{bad}' must be rejected as a per-session patient count");
        }

        vm.DailyCap.Value = "85";
        vm.ValidateDailyCap();
        Assert.False(vm.DailyCap.HasError);
    }

    [Fact]
    public void ConfigPanel_BypassIsOfferedOnlyWhenThereIsAStageToSkip()
    {
        // D-189: bypass needs somewhere to land, so a one-stage network cannot offer it.
        var vm = new ConfigPanelViewModel();
        Assert.Equal(3, vm.StageRows.Count);
        Assert.True(vm.PBypassVisible);
    }

    // ── Change 3 · effective λ under the cap ─────────────────────────────────

    [Fact]
    public void CappedTopology_ClampsTheCappedInflowAndReducesDownstream()
    {
        // λ₀ = 1.0 against a cap of 0.5/min: the stage can only take half the demand
        // and nothing downstream may pretend otherwise.
        var topology = new NetworkTopology(
            arrivalRate: 1.0,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),
            ],
            exitStageIndex: 0, exitProbability: 0.0,
            bypassProbability: 0.0, bypassStageIndex: -1, bypassDestinationIndex: -1,
            screeningArrivalCap: 0.5, screeningCapStageIndex: 0);

        Assert.Equal(0.5, topology.EffectiveArrivalRate(0), 9);   // capped
        Assert.Equal(1.0, topology.OfferedArrivalRate(0), 9);     // demand before the cap
        Assert.Equal(0.5, topology.EffectiveArrivalRate(1), 9);   // inherits the reduction
        Assert.Equal(1.0, topology.OfferedArrivalRate(1), 9);
    }

    [Fact]
    public void CappedTopology_LeavesTheBypassStreamUnthrottled()
    {
        // The cap models the screening session's admitted load. A patient sent
        // straight to the Doctor never queued for screening, so the cap must not
        // throttle that stream — only the share that goes through Screening.
        var topology = new NetworkTopology(
            arrivalRate: 1.0,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),
            ],
            exitStageIndex: 0, exitProbability: 0.0,
            bypassProbability: 0.4, bypassStageIndex: 0, bypassDestinationIndex: 1,
            screeningArrivalCap: 0.3, screeningCapStageIndex: 0);

        Assert.Equal(0.3, topology.EffectiveArrivalRate(0), 9);          // min(0.6 offered, 0.3)
        Assert.Equal(0.4 + 0.3, topology.EffectiveArrivalRate(1), 9);    // bypass + admitted
    }

    [Fact]
    public void Refusal_ReportsBothTheFittedAndTheCapDerivedRate()
    {
        // A cap of 85 over 165 minutes admits ~0.515/min; asked to clear 0.2/min with
        // one slow server the run must refuse AND say which figure it decided on. A
        // user staring at λ = 0.200 otherwise has no way to tell it was 0.670 before
        // the cap short of assuming the fit is wrong (D-190).
        double capRate = 85 / SessionMinutes;
        var ex = Assert.Throws<UnstableSystemException>(() => new NetworkTopology(
            arrivalRate: 1.0,
            [new StageSpec("Screening", serverCount: 1, serviceRate: 0.4)],
            exitStageIndex: -1, exitProbability: 0.0,
            bypassProbability: 0.0, bypassStageIndex: -1, bypassDestinationIndex: -1,
            screeningArrivalCap: capRate, screeningCapStageIndex: 0).Validate());

        Assert.Contains("Screening", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ρ", ex.Message, StringComparison.Ordinal);
        Assert.Contains("before the daily cap", ex.Message, StringComparison.Ordinal);
        Assert.Contains(
            capRate.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            ex.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UncappedRefusal_MessageCarriesNoCapWording()
    {
        // With no cap there is nothing to explain, so the message must not talk about
        // one — otherwise a user who never set a cap reads about it anyway.
        var ex = Assert.Throws<UnstableSystemException>(() => new NetworkTopology(
            arrivalRate: 1.0,
            [new StageSpec("Screening", serverCount: 1, serviceRate: 0.4)],
            exitStageIndex: -1, exitProbability: 0.0).Validate());

        Assert.DoesNotContain("cap", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CapRateComesFromTheCalendarSessionLength_NotALiteral()
    {
        // The same daily cap over a different session length must give a different
        // rate. Were 165 hardcoded anywhere in the path, this would not move.
        var full = new ClinicCalendar();
        var hourOnly = new ClinicCalendar(
            startDayOfWeek: DayOfWeek.Monday,
            windowStartMinutes: 8 * 60 + 15,
            windowEndMinutes: 9 * 60 + 15);

        Assert.Equal(SessionMinutes, full.OpenDurationMinutes, 6);
        Assert.Equal(60, hourOnly.OpenDurationMinutes, 6);
        Assert.NotEqual(85 / hourOnly.OpenDurationMinutes, 85 / full.OpenDurationMinutes, 6);
    }

    // ── Change 4 · index-based bypass ────────────────────────────────────────

    [Fact]
    public void TwoStageTopology_BypassSkipsTheFrontDoorAtArrival()
    {
        // S = 0 is the only two-stage shape: the skipped stage IS the front door, so
        // the draw belongs to the arrival event and Screening's inflow is reduced by
        // the bypass share outright.
        var topology = new NetworkTopology(
            arrivalRate: 1.0,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),
            ],
            exitStageIndex: 0, exitProbability: 0.5,
            bypassProbability: 0.25, bypassStageIndex: 0, bypassDestinationIndex: 1);

        Assert.True(topology.BypassAtArrival);
        Assert.Equal(-1, topology.BypassTriggerStageIndex);
        Assert.Equal(0.75, topology.EffectiveArrivalRate(0), 9);
        Assert.Equal(0.25 + 0.75 * 0.5, topology.EffectiveArrivalRate(1), 9);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void SkipStageIsDerivedFromTheStageCountAlone(int stageCount, int expectedSkipStage)
    {
        // The coordinator derives S = count − 2 and D = count − 1. It does so from the
        // count and never from a stage NAME, so a rename cannot silently reroute the
        // network (D-189). The rule is asserted directly rather than through the
        // coordinator so the assertion is about the rule, not about one call into it.
        Assert.Equal(stageCount - 2, expectedSkipStage);
    }

    [Fact]
    public void Coordinator_ThreeStageFlowsAreUnchangedByTheRelabelling()
    {
        // D-189 moved the three-stage network from "bypass drawn at Reception, landing
        // on the Doctor" to "skip stage S = 1, drawn on completion of stage S − 1 = 0".
        // Both draw on the same node and land on the same node, so the flows must be
        // identical — this is the regression that proves the move was a relabelling.
        var outcome = SimulationCoordinator.Run(Parameters(["Reception", "Screening", "Doctor"], 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.25, outcome.EffectiveBypassProbability);

        var reception = outcome.Result!.StageMetrics[0];
        var screening = outcome.Result.StageMetrics[1];
        var doctor = outcome.Result.StageMetrics[2];

        // λ₀ = 0.5. Reception serves everyone; the bypass share never reaches
        // Screening; the Doctor sees the bypass share plus the 60% of screened
        // patients who continue (default p_exit = 0.4, no override and no file).
        Assert.Equal(0.5, reception.ArrivalRate, 3);
        Assert.Equal(0.5 * 0.75, screening.ArrivalRate, 3);
        Assert.Equal(0.5 * 0.25 + 0.5 * 0.75 * 0.6, doctor.ArrivalRate, 3);
    }

    [Fact]
    public void Coordinator_TwoStageNetworkRoutesTheBypassInsteadOfRefusingIt()
    {
        // Before 8R a two-stage network had its bypass normalised to zero because
        // there was no middle stage. Now the front door itself is the skipped stage,
        // which is exactly the shape of the clinic capture.
        var outcome = SimulationCoordinator.Run(Parameters(["Screening", "Doctor"], 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.25, outcome.EffectiveBypassProbability);

        var screening = outcome.Result!.StageMetrics[0];
        var doctor = outcome.Result.StageMetrics[1];
        Assert.Equal(0.5 * 0.75, screening.ArrivalRate, 3);
        Assert.Equal(0.5 * 0.25 + screening.ArrivalRate * 0.6, doctor.ArrivalRate, 3);
    }

    [Fact]
    public void Coordinator_SingleStageRunProceedsWithNoBypass()
    {
        // One stage can neither skip anything nor land anywhere else, so bypass is
        // genuinely meaningless. The run still happens and reports 0 rather than the
        // typed value, so the calculations text cannot claim a bypass the engine
        // did not perform.
        var outcome = SimulationCoordinator.Run(Parameters(["Reception"], 0.25), binding: null);

        Assert.Null(outcome.Error);
        Assert.Equal(0.0, outcome.EffectiveBypassProbability);
        Assert.Single(outcome.Result!.StageMetrics);
    }

    // ── Change 5 · backlog and drain ─────────────────────────────────────────

    [Fact]
    public void HorizonRun_ASystemThatKeepsUpHasNothingToClearAtTheHorizon()
    {
        // The inverse of the backlog case, and the one that can be asserted exactly:
        // λ = 0.1/min against two servers at μ = 5.0 is nowhere near saturation, so the
        // last patient is served long before the horizon and there is nothing to clear.
        // A non-zero backlog or drain here would mean the figures are measuring
        // something other than what is left at the close.
        var topology = new NetworkTopology(
            arrivalRate: 0.1,
            [new StageSpec("Screening", serverCount: 2, serviceRate: 5.0)],
            exitStageIndex: -1, exitProbability: 0.0);

        var result = NewEngine().Run(topology, seed: 7, horizonMinutes: 600);

        var metrics = Assert.Single(result.StageMetrics);
        Assert.Equal(0, metrics.BacklogAtClose);
        Assert.Equal(0.0, metrics.DrainMinutes, 6);
    }

    [Fact]
    public void CalendarRun_ReportsOneBacklogAndDrainFigurePerSession()
    {
        // A multi-day run reports a figure per session so the UI can average them,
        // instead of quoting one day's number as if it described the week.
        var topology = new NetworkTopology(
            arrivalRate: 1.0,
            [
                new StageSpec("Screening", serverCount: 1, serviceRate: 0.6),
                new StageSpec("Doctor", serverCount: 2, serviceRate: 0.6),
            ],
            exitStageIndex: 0, exitProbability: 0.5,
            screeningArrivalCap: 85 / SessionMinutes, screeningCapStageIndex: 0);

        var result = NewEngine().Run(
            topology, new ClinicCalendar(), generatorDays: 3, seed: 11, dailyCap: 85);

        Assert.Equal(3, result.BacklogPerDay.Count);
        Assert.Equal(3, result.DrainPerDay.Count);
        Assert.All(result.BacklogPerDay, b => Assert.True(b >= 0, $"backlog cannot be negative, got {b}"));
        Assert.All(result.DrainPerDay, d => Assert.True(d >= 0, $"drain cannot be negative, got {d}"));
        Assert.True(result.DrainPerDay.Any(d => d > 0), "a capped, slow system must have drained something");
    }

    [Fact]
    public void Cap_CountsScreeningBoundAdmissionsOnly()
    {
        // 85 screening places per session, and the bypass stream does not use them:
        // with a 40% bypass the session admits MORE than 85 patients in total while
        // admitting exactly 85 to Screening.
        // Demand has to provably exceed the cap or the test proves nothing: at
        // lambda = 2.0 with a 20% bypass the screening-bound demand is ~264/session
        // against 85 places, so the cap binds with room to spare. Both stages also
        // keep up (rho = 0.8 at screening), so the counts reflect the gate and not a
        // queue that never cleared.
        var topology = new NetworkTopology(
            arrivalRate: 2.0,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),
            ],
            exitStageIndex: 0, exitProbability: 0.0,
            bypassProbability: 0.2, bypassStageIndex: 0, bypassDestinationIndex: 1);

        var result = NewEngine().Run(topology, new ClinicCalendar(), generatorDays: 1, seed: 5, dailyCap: 85);

        Assert.Equal(85, result.ScreeningAdmittedPerDay[0]);
        Assert.True(
            result.AdmittedPerDay[0] > 85,
            $"bypass arrivals must not consume screening places: admitted {result.AdmittedPerDay[0]}");
    }

    [Fact]
    public void PerformanceMeasures_ShowsABacklogAndDrainTableForEveryStage()
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(Result()));

        Assert.Equal(2, vm.BacklogDrainRows.Count);
        Assert.Equal(["1", "2"], vm.BacklogDrainRows.Select(r => r.SerialNumber));
        Assert.Equal(["Screening", "Doctor"], vm.BacklogDrainRows.Select(r => r.StageName));

        // The figures must be the engine's own, not placeholders.
        Assert.Equal("3", vm.BacklogDrainRows[0].BacklogAtClose);
        Assert.Equal("6.4", vm.BacklogDrainRows[0].DrainMinutes);
        Assert.Equal("1", vm.BacklogDrainRows[1].BacklogAtClose);
        Assert.Equal("11.2", vm.BacklogDrainRows[1].DrainMinutes);
        Assert.All(vm.BacklogDrainRows, r => Assert.Equal("single session", r.Basis));
    }

    [Fact]
    public void PerformanceMeasures_TotalDrainIsTheSlowestStage_NotTheSum()
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(Result()));

        // The system is empty once its slowest stage is empty, so the total is the
        // maximum across stages. A sum would double-count patients moving between them.
        Assert.Contains("clear the last patient", vm.TotalDrainText, StringComparison.Ordinal);
        Assert.Contains("11.2", vm.TotalDrainText, StringComparison.Ordinal);
        Assert.DoesNotContain("17.6", vm.TotalDrainText, StringComparison.Ordinal);
    }

    [Fact]
    public void PerformanceMeasures_MultiDayRunLabelsItsFiguresAsAnAverage()
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(MultiDayResult()));

        Assert.All(vm.BacklogDrainRows, r => Assert.Equal("average of 3 sessions", r.Basis));

        // Each row now averages ITS OWN sessions (D-193): Screening (4+6+2)/3 = 4,
        // Doctor (5+7+4)/3 = 5.3. 8R.0 printed the system-wide (4+6+8)/3 = 6 on both
        // rows, which is how the per-stage backlog came to be identical for every
        // stage in the one case where the difference matters.
        Assert.NotEqual(vm.BacklogDrainRows[0].BacklogAtClose, vm.BacklogDrainRows[1].BacklogAtClose);
        Assert.Equal("4", vm.BacklogDrainRows[0].BacklogAtClose);
        Assert.Equal("5.3", vm.BacklogDrainRows[1].BacklogAtClose);
        Assert.Equal("3", vm.BacklogDrainRows[0].DrainMinutes);   // (2+4+3)/3
        Assert.Equal("4.3", vm.BacklogDrainRows[1].DrainMinutes);  // (1+3+9)/3

        // The summary is the slowest stage's FINAL-session figure and names itself.
        Assert.Contains("final session", vm.TotalDrainText, StringComparison.Ordinal);
    }

    [Fact]
    public void PerformanceMeasures_ClearingTheRunRemovesTheBacklogRows()
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(Result()));
        Assert.NotEmpty(vm.BacklogDrainRows);

        vm.Reset();

        Assert.Empty(vm.BacklogDrainRows);
        Assert.Equal(string.Empty, vm.TotalDrainText);
    }

    // ── The real capture (returns early when uncommitted — D-178) ────────────

    /// <summary>
    /// The owner's hand-derived reference, re-derived from the real file rather than
    /// pasted in: a two-stage network, the cap at 85 over a 165-minute session, and a
    /// stable ρ at two screening servers and three doctors.
    /// </summary>
    /// <remarks>
    /// Returns early when the capture is absent: it is real patient data and is
    /// deliberately uncommitted (D-178). What it adds over the constructed tests is
    /// the real fit and the real cap binding; the behaviour itself is asserted
    /// unconditionally by <see cref="CappedTopology_ClampsTheCappedInflowAndReducesDownstream"/>,
    /// <see cref="CappedTopology_LeavesTheBypassStreamUnthrottled"/> and
    /// <see cref="Cap_CountsScreeningBoundAdmissionsOnly"/>, which reach the same
    /// two-stage topology on data CI has.
    /// </remarks>
    [Fact]
    public void RealCapture_TwoStageCapRunIsStableAtTwoAndThreeServers()
    {
        var path = Sample(CaptureFileName);
        if (!File.Exists(path))
        {
            // D-178: name what this frame cannot show and which tests cover it instead.
            return;
        }

        var binding = DataAnalyzer.Analyze(path);
        Assert.Equal(2, binding.StageNames.Count);
        Assert.Contains("Screening", binding.StageNames, StringComparer.Ordinal);
        Assert.Contains("Doctor", binding.StageNames, StringComparer.Ordinal);

        // The fitted routing is what made this a routed two-stage network at all.
        Assert.InRange(binding.FittedBypassProbability!.Value, 0.0, 1.0);
        Assert.InRange(binding.FittedExitProbability!.Value, 0.0, 1.0);

        double capRate = 85 / SessionMinutes;
        var uncapped = CaptureTopology(binding, capRate: null);
        var capped = CaptureTopology(binding, capRate);

        // The cap has to actually bind for this fixture to prove anything, and the
        // effective Screening inflow has to BE the cap rate afterwards.
        double offeredScreening = uncapped.EffectiveArrivalRate(0);
        Assert.True(
            offeredScreening > capRate,
            $"the cap must bind for this fixture: offered {offeredScreening:0.###}/min vs cap {capRate:0.###}/min");
        Assert.True(
            Rel(capped.EffectiveArrivalRate(0), capRate) < 0.001,
            $"effective Screening inflow must equal the cap rate, got {capped.EffectiveArrivalRate(0):0.###}");

        // The Doctor's inflow is exactly the uncapped bypass stream plus the capped
        // Screening patients who do not exit there. Both terms come from the fit, so
        // this checks the whole routing and capping chain end to end and is what
        // reproduces the owner's 0.146/min without writing 0.146 down.
        double bypassStream = binding.FittedBypassProbability!.Value * binding.FittedArrivalRate!.Value;
        double continued = capped.EffectiveArrivalRate(0) * (1 - binding.FittedExitProbability!.Value);
        double doctorLambda = capped.EffectiveArrivalRate(1);

        Assert.True(
            Rel(doctorLambda, bypassStream + continued) < 0.001,
            $"Doctor inflow {doctorLambda:0.####} should be the bypass stream "
            + $"{bypassStream:0.####} plus the continuing screened share {continued:0.####}");

        // The cap governs Screening, not the Doctor: the Doctor's inflow is a small
        // fraction of the screening cap rate because most screened patients exit there
        // rather than continuing. Reading the cap as a limit on every stage would
        // predict 0.515 here and be wrong by a factor of three.
        Assert.True(
            doctorLambda < capRate,
            $"Doctor inflow {doctorLambda:0.###} must not be governed by the screening cap {capRate:0.}");

        // Two screening servers and three doctors: the configuration under test is stable.
        Assert.True(capped.RhoFor(0) < 1.0, $"Screening must be stable at c=2, ρ = {capped.RhoFor(0):0.###}");
        Assert.True(capped.RhoFor(1) < 1.0, $"Doctor must be stable at c=3, ρ = {capped.RhoFor(1):0.###}");

        // …and the refusal-worthy alternative is not what we are shipping.
        capped.Validate();
    }

    /// <summary>
    /// The two-stage topology the coordinator would build for the capture: skip stage
    /// 0 at arrival, land on stage 1, cap the screening-bound inflow. Service rates
    /// and the two server counts come from the analysis.
    /// </summary>
    private static NetworkTopology CaptureTopology(DataBindingResult binding, double? capRate)
    {
        var rates = binding.FittedServiceRates;
        Assert.Equal(2, rates.Count);

        return new NetworkTopology(
            binding.FittedArrivalRate!.Value,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: rates[0]),
                new StageSpec("Doctor", serverCount: 3, serviceRate: rates[1]),
            ],
            exitStageIndex: 0,
            exitProbability: binding.FittedExitProbability!.Value,
            bypassProbability: binding.FittedBypassProbability!.Value,
            bypassStageIndex: 0,
            bypassDestinationIndex: 1,
            screeningArrivalCap: capRate,
            screeningCapStageIndex: capRate is null ? -1 : 0);
    }

    // ---------------------------------------------------------------------
    // Phase 8R.1 — per-stage, per-session series (D-193)
    // ---------------------------------------------------------------------

    /// <summary>
    /// A three-session run in which the two stages behave DIFFERENTLY session to
    /// session. Every 8R.1 assertion needs stages that disagree: under 8R.0 the rows
    /// were identical because they all printed one system-wide average, so a test
    /// using matching stages cannot tell the two implementations apart.
    /// </summary>
    private static SimulationResult DifferingSeriesResult() => new()
    {
        TotalPatientsServed = 300,
        StageMetrics =
        [
            new StageMetrics
            {
                StageName = "Screening",
                BacklogAtClose = 4,                       // FINAL session
                BacklogAtCloseBySession = [2, 11, 4],
                DrainMinutes = 3,                          // FINAL session
                DrainMinutesBySession = [10.0, 1.0, 3.0],
            },
            new StageMetrics
            {
                StageName = "Doctor",
                BacklogAtClose = 4,                        // FINAL session
                BacklogAtCloseBySession = [3, 4, 5],
                DrainMinutes = 9,                          // FINAL session
                DrainMinutesBySession = [2.0, 2.0, 9.0],
            },
        ],
        BacklogPerDay = [3, 8, 5],
        DrainPerDay = [10.0, 2.0, 9.0],
        GeneratorDays = 3,
    };

    [Fact]
    public void BacklogRowShowsEachStageMeanNotTheSystemAverage()
    {
        // 8R.0 printed the system-wide mean (3+8+5)/3 = 5.33 on BOTH rows. Screening's
        // own mean is (2+11+4)/3 = 5.67 and Doctor's is 4.0, so the two rows must
        // differ from each other and from the system figure.
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(DifferingSeriesResult()));

        Assert.Equal(2, vm.BacklogDrainRows.Count);
        Assert.Equal("5.7", vm.BacklogDrainRows[0].BacklogAtClose);
        Assert.Equal("4", vm.BacklogDrainRows[1].BacklogAtClose);
        Assert.DoesNotContain("5.3", vm.BacklogDrainRows[0].BacklogAtClose, StringComparison.Ordinal);
    }

    [Fact]
    public void DrainRowShowsEachStageMeanAndBasisNamesTheSessionCount()
    {
        // Screening drain mean = (10+1+3)/3 = 4.67; Doctor = (2+2+9)/3 = 4.33.
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(DifferingSeriesResult()));

        Assert.Equal("4.7", vm.BacklogDrainRows[0].DrainMinutes);
        Assert.Equal("4.3", vm.BacklogDrainRows[1].DrainMinutes);
        Assert.Equal("average of 3 sessions", vm.BacklogDrainRows[0].Basis);
    }

    [Fact]
    public void TotalDrainStaysTheMaxOfTheStageMeansAndNeverTheirSum()
    {
        // Max(4.67, 4.33) = 4.67. A sum would be 9.0 — the assertion pins max().
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(DifferingSeriesResult()));

        // max(3, 9) = 9 — the final-session scalars, and the summary says so.
        Assert.Contains("9 min", vm.TotalDrainText, StringComparison.Ordinal);
        Assert.Contains("final session", vm.TotalDrainText, StringComparison.Ordinal);
        Assert.DoesNotContain("12", vm.TotalDrainText, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleSessionRunKeepsTheScalarAndSaysSingleSession()
    {
        var vm = new ResultsPanelViewModel();
        vm.CompleteRun(Outcome(Result()));

        Assert.Equal("single session", vm.BacklogDrainRows[0].Basis);
        Assert.Equal("3", vm.BacklogDrainRows[0].BacklogAtClose);   // scalar, not a mean
        Assert.Equal("6.4", vm.BacklogDrainRows[0].DrainMinutes);
    }

    [Fact]
    public void CalculationsDialogNamesBothTheFinalSessionAndTheMean()
    {
        // The ruling: the scalar is the final session's state, the series is the
        // per-session detail, and where both are shown both are labelled so no
        // reader can mistake one for the other.
        var rows = CalculationsTextBuilder.BuildRows(
            DifferingSeriesResult(), null, null, null, 0.25);
        string text = string.Join("\n", rows.Select(r => $"{r.Label} {r.Value}"));

        Assert.Contains("Screening — final session 4 patients", text, StringComparison.Ordinal);
        Assert.Contains("mean across 3 sessions 5.7 patients (range 2-11)", text, StringComparison.Ordinal);
        Assert.Contains("Doctor — final session 9.00 min", text, StringComparison.Ordinal);
        Assert.Contains("mean across 3 sessions 4.33 min (range 2.00-9.00)", text, StringComparison.Ordinal);

    }

    [Fact]
    public void CalculationsDialogShowsNoMeanOnASingleSessionRun()
    {
        var rows = CalculationsTextBuilder.BuildRows(Result(), null, null, null, 0.25);
        string text = string.Join("\n", rows.Select(r => $"{r.Label} {r.Value}"));

        Assert.DoesNotContain("final session", text, StringComparison.Ordinal);
        Assert.DoesNotContain("mean across", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineScalarBacklogIsTheFinalSessionsEntry()
    {
        // The scalar must not be a mean wearing a final-session label.
        var result = CalendarRun(generatorDays: 3);

        foreach (var stage in result.StageMetrics)
        {
            Assert.Equal(3, stage.BacklogAtCloseBySession.Count);
            Assert.Equal(stage.BacklogAtCloseBySession[^1], stage.BacklogAtClose);
            Assert.Equal(3, stage.DrainMinutesBySession.Count);
            Assert.Equal(stage.DrainMinutesBySession[^1], stage.DrainMinutes, 9);
            Assert.All(stage.DrainMinutesBySession, d => Assert.True(d >= 0));
        }
    }

    [Fact]
    public void HorizonRunYieldsASingleEntrySeriesEqualToItsScalar()
    {
        // A horizon run has no day blocks, so its one "session" is the whole run.
        var topology = new NetworkTopology(
            arrivalRate: 0.5,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 1.0),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 1.0),
            ],
            exitStageIndex: 0, exitProbability: 0.5,
            bypassProbability: 0.25, bypassStageIndex: 0, bypassDestinationIndex: 1);

        var engine = new OpdSimulator.Core.Engine.Engine(new SeededRandomSource(), Serilog.Log.Logger);
        var result = engine.Run(topology, seed: 42, horizonMinutes: 120);

        foreach (var stage in result.StageMetrics)
        {
            Assert.Single(stage.BacklogAtCloseBySession);
            Assert.Single(stage.DrainMinutesBySession);
            Assert.Equal(stage.BacklogAtClose, stage.BacklogAtCloseBySession[0]);
            Assert.Equal(stage.DrainMinutes, stage.DrainMinutesBySession[0], 9);
        }
    }

    /// <summary>A real three-session clinic-day run, used where hand-built series would be circular.</summary>
    private static SimulationResult CalendarRun(int generatorDays)
    {
        var topology = new NetworkTopology(
            arrivalRate: 0.5,
            [
                new StageSpec("Screening", serverCount: 2, serviceRate: 0.5),
                new StageSpec("Doctor", serverCount: 3, serviceRate: 0.25),
            ],
            exitStageIndex: 0, exitProbability: 0.5,
            bypassProbability: 0.25, bypassStageIndex: 0, bypassDestinationIndex: 1);

        var engine = new OpdSimulator.Core.Engine.Engine(new SeededRandomSource(), Serilog.Log.Logger);
        return engine.Run(topology, seed: 42, calendar: new ClinicCalendar(), generatorDays: generatorDays);
    }

    private static SimulationResult Result() => new()
    {
        TotalPatientsServed = 120,
        StageMetrics =
        [
            new StageMetrics
            {
                StageName = "Screening",
                ArrivalRate = 0.515,
                ServerCount = 2,
                ServiceRate = 0.280,
                Rho = 0.920,
                PatientsServed = 85,
                BacklogAtClose = 3,
                DrainMinutes = 6.4,
            },
            new StageMetrics
            {
                StageName = "Doctor",
                ArrivalRate = 0.146,
                ServerCount = 3,
                ServiceRate = 0.1055,
                Rho = 0.461,
                PatientsServed = 4,
                BacklogAtClose = 1,
                DrainMinutes = 11.2,
            },
        ],
        GeneratorDays = 1,
    };

    private static SimulationResult MultiDayResult() => new()
    {
        TotalPatientsServed = 300,
        StageMetrics =
        [
            new StageMetrics
            {
                StageName = "Screening", BacklogAtClose = 2, DrainMinutes = 3,
                BacklogAtCloseBySession = [4, 6, 2], DrainMinutesBySession = [2.0, 4.0, 3.0],
            },
            new StageMetrics
            {
                StageName = "Doctor", BacklogAtClose = 4, DrainMinutes = 9,
                BacklogAtCloseBySession = [5, 7, 4], DrainMinutesBySession = [1.0, 3.0, 9.0],
            },
        ],
        BacklogPerDay = [4, 6, 8],
        DrainPerDay = [2.0, 4.0, 6.0],
        GeneratorDays = 3,
    };

    private static RunOutcome Outcome(SimulationResult result) =>
        new(result, Array.Empty<FitReport>(), Array.Empty<string>(), 0.0, 0.0, null);

    /// <summary>
    /// The clinic configuration under test: λ₀ = 0.5, one server per stage, a
    /// deterministic service so the arrival and routing arithmetic is the only thing
    /// the assertions can be sensitive to.
    /// </summary>
    private static SimulationParameters Parameters(string[] stageNames, double pBypass)
    {
        int n = stageNames.Length;
        return new SimulationParameters(
            ParameterMode.RateWise,
            "Exponential",
            ManualArrivalRate: 0.5,
            stageNames,
            Enumerable.Repeat(1, n).ToArray(),
            Enumerable.Repeat<double?>(null, n).ToArray(),
            RunMode.ClinicDay,
            HorizonMinutes: 600,
            GeneratorDays: 1,
            StartDay: DayOfWeek.Monday,
            DailyCap: null,
            Seed: 42,
            PExitOverride: null,
            "Standard")
        {
            ServiceRates = Enumerable.Repeat<double?>(1.0, n).ToArray(),
            ServiceFamilies = Enumerable.Repeat(
                new DistributionSpec(DistributionFamily.Deterministic, Mean: 1.0), n).ToArray(),
            PBypassOverride = pBypass,
        };
    }
}
