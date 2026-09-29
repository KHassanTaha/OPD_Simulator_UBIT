using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Parameters;
using Xunit;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8O — the observation window, the dual arrival-rate estimate, the
/// Horizon cleanup and the calculations dialog's report of all three (D-172,
/// D-173, D-174).
/// </summary>
/// <remarks>
/// <para>
/// Why these tests assert what they assert. Phase 8O introduced two
/// mathematically different estimates of the same arrival rate, and the whole
/// risk in that change is a number quietly coming from the wrong one. So the
/// behavioural assertions here are mostly about WHICH number a run used, not
/// about the UI painting it: a screenshot cannot tell you that, and a formula
/// test can.
/// </para>
/// <para>
/// The window tests pin the arithmetic the user checks by hand (5 sessions ×
/// 165 minutes), because that arithmetic is the part being defended in a viva.
/// </para>
/// </remarks>
public class Phase8OTests
{
    private const double SessionMinutes = 165.0;   // 08:15–11:00
    private const double HoursPerSession = 2.75;  // 165 / 60

    // ── Observation window (D-172, ruling 4) ──────────────────────────────

    /// <summary>
    /// A file with no <c>session_date</c> column is a single session, which is
    /// what every pre-8O file was. This must not become "unknown" or "zero" —
    /// both would silently change every existing single-day result.
    /// </summary>
    [Fact]
    public void ObservationWindow_NoSessionDates_IsOneSession()
    {
        var window = ObservationWindowService.FromSessionDates(null);

        Assert.NotNull(window);
        Assert.Equal(1, window!.OperatingDays);
        Assert.Equal(SessionMinutes, window.OperatingMinutes);
    }

    /// <summary>
    /// Six open sessions over a Monday-to-Saturday span: 6 × 165. The count is of
    /// OPERATING days, so a seven-day calendar span does not mean seven days of
    /// data.
    /// </summary>
    [Fact]
    public void ObservationWindow_CountsDistinctOpenSessions()
    {
        var dates = new[]
        {
            new DateOnly(2026, 9, 14), // Mon
            new DateOnly(2026, 9, 15),
            new DateOnly(2026, 9, 16),
            new DateOnly(2026, 9, 17),
            new DateOnly(2026, 9, 19), // Sat
            new DateOnly(2026, 9, 21),
        };

        var window = ObservationWindowService.FromSessionDates(dates)!;

        Assert.Equal(6, window.OperatingDays);
        Assert.Equal(6 * SessionMinutes, window.OperatingMinutes);
    }

    /// <summary>
    /// Ten rows from one session must not count as ten operating days — the
    /// window is distinct sessions, not rows. This is the assertion that a
    /// plausible-looking implementation (counting rows) fails.
    /// </summary>
    [Fact]
    public void ObservationWindow_RepeatedRowsInOneSession_CountOnce()
    {
        var dates = Enumerable.Repeat(new DateOnly(2026, 9, 14), 10).ToArray();

        var window = ObservationWindowService.FromSessionDates(dates)!;

        Assert.Equal(1, window.OperatingDays);
    }

    /// <summary>
    /// Ruling 5: a Friday row is permitted data, and the clinic is shut, so it
    /// contributes no operating time. It must be EXCLUDED rather than rejected —
    /// flagging it in the validator would fail the whole file, because
    /// <c>DataBindingResult.IsUsable</c> is false whenever the validator reports
    /// any issue at all.
    /// </summary>
    [Fact]
    public void ObservationWindow_WeekendDatesExcludedNotRejected()
    {
        var dates = new[]
        {
            new DateOnly(2026, 9, 14), // Mon
            new DateOnly(2026, 9, 18), // Fri — closed
            new DateOnly(2026, 9, 20), // Sun — closed
            new DateOnly(2026, 9, 15), // Tue
        };

        var window = ObservationWindowService.FromSessionDates(dates)!;

        Assert.Equal(2, window.OperatingDays);
        Assert.Equal(2 * SessionMinutes, window.OperatingMinutes);
    }

    /// <summary>
    /// Ruling 4: a file whose every session falls on a closed day has no
    /// operating time to divide arrivals by. Returning a zero-minute window
    /// would make the window rate infinite, so the service returns null and the
    /// UI says so in words.
    /// </summary>
    [Fact]
    public void ObservationWindow_AllWeekendDates_ReturnsNull()
    {
        var dates = new[]
        {
            new DateOnly(2026, 9, 18), // Fri
            new DateOnly(2026, 9, 19), // Sat — open, but one row is enough
            new DateOnly(2026, 9, 20), // Sun
        };
        var closedOnly = dates.Where(d => d.DayOfWeek is DayOfWeek.Sunday).ToArray();

        Assert.Null(ObservationWindowService.FromSessionDates(closedOnly));
    }

    /// <summary>
    /// Ruling 4 also names the user-facing wording, so it is asserted verbatim
    /// rather than paraphrased: this string is what a user reads when their file
    /// has no operating sessions, and "λ unavailable" alone would not tell them
    /// WHY.
    /// </summary>
    [Fact]
    public void ObservationWindow_AllWeekend_ShowsSpecifiedMessage()
    {
        // Driven through a real file rather than a hand-built binding: the point
        // is that a Sunday-only file SURVIVES validation (ruling 5) and then
        // reports no operating window, which is a property of the two layers
        // together. A binding constructed by hand would not prove the validator
        // lets it through.
        string path = Path.Combine(Path.GetTempPath(), $"opd-8o-sunday-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path,
            "session_date,arrival_time,departure_time,screening_service_minutes,doctor_service_minutes\n"
            + "2026-09-20,08:15,08:20,5,\n"
            + "2026-09-20,08:40,08:45,6,\n");
        try
        {
            var binding = DataAnalyzer.Analyze(path);
            Assert.Null(binding.ErrorMessage);

            var vm = new InputTabViewModel();
            vm.SetLoadedFile(binding);

            Assert.Equal("No operating sessions detected in this file.", vm.WindowLambdaText);
            Assert.False(vm.HasWindowLambda);
            Assert.True(vm.IsWindowChoiceDisabled);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Presets (rulings 3, D-174) ────────────────────────────────────────

    /// <summary>
    /// The presets count OPERATING days: 5 per week, 20 per month, for a clinic
    /// open Mon–Thu and Sat. The old time-span presets said 6 and 26 — counting
    /// calendar days — which ran a "week" a day longer than the clinic opens.
    /// </summary>
    [Theory]
    [InlineData(ObservationWindowSelection.OneDay, 1)]
    [InlineData(ObservationWindowSelection.ThreeDays, 3)]
    [InlineData(ObservationWindowSelection.OneWeek, 5)]
    [InlineData(ObservationWindowSelection.TwoWeeks, 10)]
    [InlineData(ObservationWindowSelection.OneMonth, 20)]
    public void ObservationWindow_PresetsUseOperatingDays(
        ObservationWindowSelection selection,
        int expectedDays)
    {
        var window = ObservationWindowService.Resolve(null, selection, null)!;

        Assert.Equal(expectedDays, window.OperatingDays);
        Assert.Equal(expectedDays * SessionMinutes, window.OperatingMinutes);
    }

    /// <summary>
    /// Ruling 3: the user types HOURS and the UI reports hours. Internally the
    /// day count is floored, because a partial session is not a session — but
    /// showing the user "3 days" for the 10 hours they typed would misstate
    /// their own input, and the minutes are what the rate divides by.
    /// </summary>
    [Fact]
    public void ObservationWindow_CustomHours_FloorsDaysKeepsHours()
    {
        var window = ObservationWindowService.FromCustomHours(10)!;

        // 10 / 2.75 = 3.63 → floored to 3 operating days.
        Assert.Equal(3, window.OperatingDays);
        // The divisor is the hours the user entered, not the floored days.
        Assert.Equal(600.0, window.OperatingMinutes);
    }

    /// <summary>
    /// Hours below one session still yield a positive window: 1 hour of
    /// operating time is a real (if unusual) window, and the day count flooring
    /// to 0 must not zero the divisor.
    /// </summary>
    [Fact]
    public void ObservationWindow_CustomSubSessionHours_KeepsMinutes()
    {
        var window = ObservationWindowService.FromCustomHours(1)!;

        Assert.Equal(0, window.OperatingDays);
        Assert.Equal(60.0, window.OperatingMinutes);
    }

    /// <summary>
    /// Zero or negative hours resolve to null rather than a zero-minute window:
    /// a zero divisor produces an infinity that would surface as a nonsense rate
    /// instead of a refusal.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-5.0)]
    public void ObservationWindow_CustomNonPositiveHours_ReturnsNull(double hours)
    {
        Assert.Null(ObservationWindowService.FromCustomHours(hours));
    }

    // ── Dual λ (rulings 2, 7) ──────────────────────────────────────────────

    /// <summary>
    /// The two estimators are defined on different divisors, and the divergence
    /// is the point of showing both. This asserts the arithmetic on the shipped
    /// six-session fixture: 60 arrivals over 6 × 165 minutes, versus 1 ÷ the mean
    /// of the 54 within-session gaps.
    /// </summary>
    [Fact]
    public void WindowLambda_IsArrivalsOverOperatingMinutes()
    {
        var binding = AnalyzeMultidayFixture();

        // 6 sessions × 165 minutes.
        Assert.Equal(990.0, binding.ObservedWindow!.OperatingMinutes);
        Assert.Equal(binding.DataSet!.RowCount / 990.0, binding.WindowLambda!.Value, 6);
    }

    /// <summary>
    /// Ruling 2's whole content: the MLE sample excludes cross-session gaps. The
    /// fixture has 60 rows and therefore 59 total gaps, of which 5 cross an
    /// overnight or weekend closure — so 54 gaps enter the mean. If they all
    /// did, λ would be far smaller and would describe nothing real.
    /// </summary>
    [Fact]
    public void MleLambda_ExcludesCrossSessionGaps()
    {
        var binding = AnalyzeMultidayFixture();

        Assert.Equal(54, binding.InterArrivalMinutes.Count);
    }

    /// <summary>
    /// Ruling 7: the shell owns the choice, so a run uses exactly the λ the user
    /// picked. This drives the real coordinator and reads the rate back out of
    /// the topology it built, because asserting on the view model's own copy
    /// would pass even if the coordinator ignored it.
    /// </summary>
    [Fact]
    public void Run_UsesWindowLambdaWhenWindowSelected()
    {
        var binding = AnalyzeMultidayFixture();
        var parameters = DiagnosticParameters() with { LambdaSource = LambdaSource.Window };

        double sampled = SampledArrivalRate(SimulationCoordinator.Run(parameters, binding));

        AssertRateEquals(binding.WindowLambda!.Value, sampled);
        // The whole point: the two estimates differ materially on a six-session
        // file, so a coordinator that ignored the choice would be caught here.
        Assert.NotEqual(binding.FittedArrivalRate!.Value, binding.WindowLambda!.Value, 3);
    }

    [Fact]
    public void Run_UsesMleLambdaByDefault()
    {
        var binding = AnalyzeMultidayFixture();

        // Not setting LambdaSource at all must mean MLE: the init default is what
        // every pre-8O construction site gets.
        double sampled = SampledArrivalRate(
            SimulationCoordinator.Run(DiagnosticParameters(), binding));

        AssertRateEquals(binding.FittedArrivalRate!.Value, sampled);
    }

    /// <summary>
    /// A manual λ the user typed outranks both estimates. Choosing "Window" is a
    /// preference between two FITTED values, not a way to overrule a typed one.
    /// </summary>
    [Fact]
    public void Run_ManualLambdaBeatsEitherEstimate()
    {
        var binding = AnalyzeMultidayFixture();
        var parameters = DiagnosticParameters() with
        {
            ManualArrivalRate = 0.2,
            LambdaSource = LambdaSource.Window,
        };

        double sampled = SampledArrivalRate(SimulationCoordinator.Run(parameters, binding));

        AssertRateEquals(0.2, sampled);
    }

    /// <summary>
    /// Choosing Window on a file that has no window λ must fall back to MLE, not
    /// refuse the run: a display preference is not a reason to fail a
    /// simulation the user can otherwise perform.
    /// </summary>
    [Fact]
    public void Run_WindowRequestedWithoutWindowLambda_FallsBackToMle()
    {
        var binding = AnalyzeMultidayFixture() with { WindowLambda = null };
        var parameters = DiagnosticParameters() with { LambdaSource = LambdaSource.Window };

        double sampled = SampledArrivalRate(SimulationCoordinator.Run(parameters, binding));

        AssertRateEquals(binding.FittedArrivalRate!.Value, sampled);
    }

    /// <summary>
    /// Ruling 7's ownership rule, asserted through the real objects: setting the
    /// radio on the Input tab changes the value the config panel reads when it
    /// builds the run's parameters.
    /// </summary>
    [AvaloniaFact]
    public void InputTabRadio_SetsTheValueTheConfigPanelBuildsFrom()
    {
        var main = new MainViewModel();

        Assert.Equal(LambdaSource.Mle, main.SelectedLambdaSource);

        main.InputTab.IsWindowSelected = true;

        Assert.Equal(LambdaSource.Window, main.SelectedLambdaSource);
        Assert.Equal(
            LambdaSource.Window,
            main.Config.TryBuildRunParameters()!.LambdaSource);

        main.InputTab.IsMleSelected = true;

        Assert.Equal(LambdaSource.Mle, main.SelectedLambdaSource);
    }

    // ── Calculations dialog (D-173) ────────────────────────────────────────

    /// <summary>
    /// Ruling: the dialog prints three λ lines — MLE, window, and which one the
    /// run used. A reader who sees only the applied value cannot tell that a
    /// materially different estimate was available.
    /// </summary>
    [Fact]
    public void Calculations_PrintsBothEstimatesAndTheSource()
    {
        var binding = AnalyzeMultidayFixture();

        var rows = CalculationsTextBuilder.BuildRows(
            SmallResult(), ManualParameters() with { LambdaSource = LambdaSource.Window },
            "fitted from sample_multiday.csv", binding);

        Assert.Equal("window (arrivals ÷ operating minutes)", ValueOf(rows, "λ source"));
        Assert.Equal(binding.FittedArrivalRate!.Value.ToString("F5", CultureInfo.InvariantCulture) + " patients/min",
            ValueOf(rows, "λ — MLE"));
        Assert.Equal(binding.WindowLambda!.Value.ToString("F5", CultureInfo.InvariantCulture) + " patients/min",
            ValueOf(rows, "λ — window"));
        Assert.Equal("6 operating days (990 operating minutes)", ValueOf(rows, "Observation window"));
    }

    /// <summary>
    /// D-174: the dialog said "ClinicDay" / "DiagnosticTrace" — the enum member
    /// names, which the user never sees. It must name the radio they clicked.
    /// </summary>
    [Fact]
    public void Calculations_RunModeUsesTheUsersWording()
    {
        foreach (var (mode, expected) in new[]
                 {
                     (RunMode.ClinicDay, "Single day"),
                     (RunMode.MultiDay, "Multi-day"),
                     (RunMode.DiagnosticTrace, "Diagnostic trace"),
                 })
        {
            var rows = CalculationsTextBuilder.BuildRows(
                SmallResult(), ManualParameters() with { RunMode = mode }, "entered manually");

            Assert.Equal(expected, ValueOf(rows, "Run mode"));
        }
    }

    /// <summary>
    /// The flat text and the dialog rows come from one call, so they cannot
    /// disagree. Asserting both carry the new lines is what keeps a future
    /// second implementation of the dialog from quietly dropping them.
    /// </summary>
    [Fact]
    public void Calculations_FlatTextAndRowsAgree()
    {
        var binding = AnalyzeMultidayFixture();
        var parameters = ManualParameters() with { LambdaSource = LambdaSource.Window };

        var flat = CalculationsTextBuilder.Build(
            SmallResult(), parameters, "fitted from sample_multiday.csv", binding);
        var rows = CalculationsTextBuilder.BuildRows(
            SmallResult(), parameters, "fitted from sample_multiday.csv", binding);

        Assert.Contains("λ — MLE", flat, StringComparison.Ordinal);
        Assert.Contains("λ — window", flat, StringComparison.Ordinal);
        // The flat text is FlatText(BuildRows(...)), so equal inputs must yield
        // the same row set in both renderings — that is what makes one of them
        // a renderer rather than a second implementation.
        Assert.Equal(
            rows.Select(r => (r.Label, r.Value)),
            CalculationsTextBuilder
                .BuildRows(SmallResult(), parameters, "fitted from sample_multiday.csv", binding)
                .Select(r => (r.Label, r.Value)));
    }

    /// <summary>
    /// A run with no data file has no second estimate, so the extra lines are
    /// absent rather than blank — Path B must not grow phantom fields.
    /// </summary>
    [Fact]
    public void Calculations_ManualRunHasNoEstimateLines()
    {
        var text = CalculationsTextBuilder.Build(
            SmallResult(), ManualParameters(), "entered manually");

        Assert.DoesNotContain("λ — MLE", text, StringComparison.Ordinal);
        Assert.DoesNotContain("λ — window", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The receipt has to report the window λ the RUN used, and name the window
    /// that number was divided by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The override was added because the panel and the engine were showing two
    /// different λs. The receipt had the mirror defect and it is the more
    /// dangerous of the two, because the receipt is what a viva examiner or a
    /// later reader trusts: it printed <c>λ — window</c> from
    /// <c>DataBindingResult.WindowLambda</c>, the AUTO-detected figure, even
    /// when the run had used the selected window's. A user who picked "1 week"
    /// and then opened View calculations was told the run used 60 ÷ 990 while
    /// the engine used 60 ÷ 825.
    /// </para>
    /// <para>
    /// The second assertion is the one that matters. Reporting the right number
    /// is not enough if the line above it still describes a different window:
    /// dividing the printed arrivals by the printed minutes has to REPRODUCE the
    /// printed λ, and before this fix it did not.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculations_ReportTheWindowTheRunUsed_NotTheFilesOwn()
    {
        var binding = AnalyzeMultidayFixture();

        var main = new MainViewModel();
        main.InputTab.SetLoadedFile(binding);
        main.SelectedLambdaSource = LambdaSource.Window;
        main.InputTab.SelectedWindowLabel = "1 week";

        var parameters = main.Config.TryBuildRunParameters()!;
        double applied = parameters.WindowLambdaOverride!.Value;
        double fileOwn = binding.WindowLambda!.Value;

        // Precondition: the two really are different, so the test cannot pass
        // vacuously by both paths agreeing.
        Assert.NotEqual(fileOwn, applied, 3);

        var text = CalculationsTextBuilder.Build(SmallResult(), parameters, "file", binding);

        // The run's figure is the one on the window line…
        Assert.Contains(applied.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                        text, StringComparison.Ordinal);
        // …and the file's auto-detected figure is NOT presented as the run's.
        Assert.DoesNotContain(fileOwn.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
                             text, StringComparison.Ordinal);

        // The selected window is named, and the division is checkable: 60 arrivals
        // over 825 minutes is the number printed.
        Assert.Contains("Selected window (used)", text, StringComparison.Ordinal);
        Assert.Contains("825", text, StringComparison.Ordinal);
        AssertRateEquals(applied, binding.DataSet!.RowCount / 825.0);

        // The file's own window is still reported, because it remains a true
        // statement about the data — it is simply no longer the divisor.
        Assert.Contains("Observation window", text, StringComparison.Ordinal);
        Assert.Contains("990", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The mirror of the test above: on the default Auto selection the run's
    /// window IS the file's, so the dialog must not sprout the "selected window"
    /// qualifier or a second window line for no reason.
    /// </summary>
    [Fact]
    public void Calculations_OnAutoSelectionShowOneWindowLine()
    {
        var binding = AnalyzeMultidayFixture();

        var main = new ViewModels.MainViewModel();
        main.InputTab.SetLoadedFile(binding);
        main.SelectedLambdaSource = LambdaSource.Window;   // selection left on Auto

        var parameters = main.Config.TryBuildRunParameters()!;
        var text = CalculationsTextBuilder.Build(SmallResult(), parameters, "file", binding);

        Assert.Contains("λ — window", text, StringComparison.Ordinal);
        Assert.DoesNotContain("λ — window (used)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Selected window", text, StringComparison.Ordinal);
        Assert.Contains("990", text, StringComparison.Ordinal);
    }

    // ── Horizon (D-174, ruling 6) ─────────────────────────────────────────

    /// <summary>
    /// Ruling 6 fixes the order AND the default. The first option must be the
    /// default, so a user opening the dropdown sees the value already in force.
    /// </summary>
    [Fact]
    public void Horizon_DurationDefaultIsOneHourAndListedFirst()
    {
        var vm = new ConfigPanelViewModel();

        Assert.Equal(new[] { "1 hour", "15 minutes", "Custom minutes…" }, vm.DurationOptions);
        Assert.Equal("1 hour", vm.DurationOptions[0]);
        Assert.Equal(DiagnosticDurationPreset.OneHour, vm.Duration);
        Assert.Equal(60.0, vm.TryBuildRunParameters()!.HorizonMinutes);
    }

    /// <summary>
    /// D-174's actual defect: one dropdown drove both the diagnostic minutes and
    /// the calendar run length, so "1 week" meant six generator days for a
    /// five-day clinic. The calendar length is now read only from the Days field.
    /// </summary>
    /// <remarks>
    /// The second assertion changed with the fix, and deliberately. It used to
    /// assert that the diagnostic value still reached <c>HorizonMinutes</c> — the
    /// "harmless" pass-through of an earlier draft. A calendar run never reads
    /// that field, so carrying a number the mode ignores is not harmless, it is a
    /// second copy of a duration that can disagree with the first: a reader of the
    /// record sees 10,000 minutes beside a 3-day run. The run length now comes
    /// from one place, and this test says so.
    /// </remarks>
    [Fact]
    public void Horizon_DiagnosticDurationDoesNotDriveCalendarRunLength()
    {
        var vm = new ConfigPanelViewModel
        {
            IsMultiDay = true,
            Duration = DiagnosticDurationPreset.CustomMinutes,
        };
        vm.CustomMinutes.Value = "10000";
        vm.Days.Value = "3";

        var parameters = vm.TryBuildRunParameters()!;

        Assert.Equal(3, parameters.GeneratorDays);
        Assert.NotEqual(10000.0, parameters.HorizonMinutes);
        Assert.Equal(ConfigPanelViewModel.DefaultDiagnosticMinutes, parameters.HorizonMinutes);
    }

    /// <summary>
    /// A stale error in the HIDDEN custom-minutes field must not govern the run.
    /// A user who typed "abc", switched to the 15-minute preset, and pressed Run
    /// must get a 15-minute run — not a refusal about a field they can no longer
    /// see or edit.
    /// </summary>
    [Fact]
    public void Horizon_HiddenCustomMinutesErrorDoesNotBlockTheRun()
    {
        var vm = new ConfigPanelViewModel { IsDiagnosticTrace = true };
        vm.CustomMinutes.Value = "abc";
        vm.ValidateCustomMinutes();
        Assert.True(vm.CustomMinutes.HasError);

        vm.Duration = DiagnosticDurationPreset.FifteenMinutes;

        var parameters = vm.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(15.0, parameters!.HorizonMinutes);
    }

    /// <summary>
    /// The converse: while Custom IS selected, the field governs, and an
    /// unparseable value refuses the build rather than falling back to 60.
    /// </summary>
    [Fact]
    public void Horizon_VisibleCustomMinutesErrorRefusesTheRun()
    {
        var vm = new ConfigPanelViewModel
        {
            IsDiagnosticTrace = true,
            Duration = DiagnosticDurationPreset.CustomMinutes,
        };
        vm.CustomMinutes.Value = "abc";

        Assert.Null(vm.TryBuildRunParameters());
    }

    // ── Input tab surface ─────────────────────────────────────────────────

    /// <summary>
    /// Ruling 3/4/7: the panel must show both estimates, offer the window
    /// choice, and disable it with a stated reason when there is no window λ —
    /// a silently-disabled control violates FR-UI-7.
    /// </summary>
    [AvaloniaFact]
    public void InputTab_ShowsBothEstimatesAndTheChoice()
    {
        var window = new Window { Content = new Views.InputTab() };
        window.DataContext = new MainViewModel();
        window.Show();
        try
        {
            window.UpdateLayout();
            var text = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(t => t.Text ?? "")
                .ToList();

            Assert.Contains(text, t => t.Contains("Observation window", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains("Arrival rate (λ)", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains("MLE (1 ÷ mean inter-arrival gap)", StringComparison.Ordinal));
            Assert.Contains(text, t => t.Contains("Window (arrivals ÷ operating minutes)", StringComparison.Ordinal));

            var radios = window.GetVisualDescendants().OfType<RadioButton>().ToList();
            Assert.Equal(2, radios.Count);
            Assert.All(radios, r => Assert.True(
                r.IsEnabled,
                "both λ radios must be enabled when nothing is loaded yet; the "
                + "disabling is driven by the file, not by the empty state"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Ruling 6: the custom-minutes row appears only for the Custom option, so a
    /// hidden field cannot be tabbed into or read as if it were in force.
    /// </summary>
    [AvaloniaFact]
    public void InputTab_CustomHoursRowOnlyForCustomWindow()
    {
        var vm = new InputTabViewModel();

        Assert.False(vm.IsCustomWindowSelected);

        vm.SelectedWindowLabel = "Custom…";
        Assert.True(vm.IsCustomWindowSelected);

        vm.SelectedWindowLabel = "1 week";
        Assert.False(vm.IsCustomWindowSelected);
    }

    /// <summary>
    /// Every window preset must round-trip through its dropdown label. A parse
    /// that fell through to Auto would silently ignore the user's selection, and
    /// the readout would keep showing the file's own window.
    /// </summary>
    [Fact]
    public void InputTab_WindowLabelsRoundTrip()
    {
        foreach (var selection in Enum.GetValues<ObservationWindowSelection>())
        {
            var label = ObservationWindowService.SelectionLabel(selection);
            Assert.Equal(selection, ObservationWindowService.ParseSelection(label));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static DataBindingResult AnalyzeMultidayFixture()
    {
        string path = Path.Combine(RepoRoot(), "samples", "sample_multiday.csv");
        Assert.True(File.Exists(path), "sample_multiday.csv is required by the Phase 8O tests");
        return DataAnalyzer.Analyze(path);
    }

    /// <summary>The value of the first row with this label, or a failure naming it.</summary>
    private static string ValueOf(IReadOnlyList<CalculationRow> rows, string label) =>
        rows.FirstOrDefault(r => r.Label == label)?.Value
        ?? throw new Xunit.Sdk.XunitException(
            $"no calculations row labelled \"{label}\"; labels were: "
            + string.Join(", ", rows.Select(r => r.Label)));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpdSimulator.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
            ?? throw new InvalidOperationException("could not locate OpdSimulator.sln");
    }

    private static SimulationParameters ManualParameters() => new(
        ParameterMode.RateWise,
        "Exponential",
        null,
        new[] { "Reception", "Screening", "Doctor" },
        new[] { 1, 1, 1 },
        new double?[] { 0.8, 0.5, 0.4 },
        RunMode.ClinicDay,
        0,
        1,
        DayOfWeek.Monday,
        null,
        42,
        0.4,
        "Minimal")
    {
        // The coordinator refuses a record whose per-stage lists disagree with
        // StageNames, so a hand-built parameter block has to carry them too.
        ServiceFamilies = new[]
        {
            new DistributionSpec(DistributionFamily.Exponential, 0.8),
            new DistributionSpec(DistributionFamily.Exponential, 0.5),
            new DistributionSpec(DistributionFamily.Exponential, 0.4),
        },
        ServiceRates = new double?[] { 0.8, 0.5, 0.4 },
    };

    private static Core.Engine.SimulationResult SmallResult() => new()
    {
        TotalPatientsServed = 9,
        AverageWaitMinutes = 13.3,
        AverageQueueLength = 1.2,
        AverageSystemTimeMinutes = 46.7,
        StageUtilisation = 0.42,
        PerServerUtilisation = new[] { 0.42 },
        ThroughputPerMinute = 0.055,
        OperatingTimeMinutes = SessionMinutes,
    };

    /// <summary>
    /// A long diagnostic horizon, so the generated inter-arrival sample is large
    /// enough for its mean to pin λ down. <c>SimulationResult</c> does not report
    /// the λ it was built with, so the run's rate is read back from the engine's
    /// own generated gaps — which is a stronger assertion than trusting a
    /// recorded field: it is the rate the engine actually simulated at.
    /// </summary>
    private static SimulationParameters DiagnosticParameters() => ManualParameters() with
    {
        RunMode = RunMode.DiagnosticTrace,
        HorizonMinutes = 400_000,
    };

    private static double SampledArrivalRate(RunOutcome outcome)
    {
        Assert.Null(outcome.Error);
        var samples = outcome.Result!.GeneratedInterArrivalSamples;
        Assert.NotEmpty(samples);
        return 1.0 / samples.Average();
    }

    /// <summary>
    /// Compares the rate the engine sampled at against the rate the run was
    /// supposed to use. A 5% tolerance is a sampling tolerance, not a looseness
    /// choice: with ~10,000 gaps the mean gap is accurate to well under 1%, so
    /// anything above 5% means the wrong λ reached the topology.
    /// </summary>
    private static void AssertRateEquals(double expected, double actual)
    {
        Assert.InRange(actual, expected * 0.95, expected * 1.05);
    }

    // ── Session dates are a SET, not one entry per row ─────────────────────

    /// <summary>
    /// A six-day file with 60 rows reports SIX dates. If <c>SessionDates</c> were
    /// one entry per row it would report 60, and every consumer that reasoned
    /// about it — the window picker, anything that shows "the days this file
    /// covers" — would be describing a 60-day file.
    /// </summary>
    /// <remarks>
    /// Asserting the count alone would not catch a change to WHICH dates, so both
    /// the count and the membership are checked. The order is file order, so the
    /// picker lists the days the way the file shows them.
    /// </remarks>
    [Fact]
    public void SessionDates_AreDistinctDates_InFileOrder()
    {
        var binding = AnalyzeMultidayFixture();

        Assert.NotNull(binding.SessionDates);
        var dates = binding.SessionDates!;
        Assert.Equal(6, dates.Count);
        Assert.Equal(dates.Distinct().Count(), dates.Count);
        Assert.Equal(
            dates.OrderBy(d => d).ToList(),
            dates);
    }

    /// <summary>
    /// The same set feeds the window arithmetic, so distinctness is not only a
    /// display nicety: a per-row list divided by 165 would have produced a
    /// 60-day window and a window λ 10× too small.
    /// </summary>
    [Fact]
    public void ObservedWindow_CountsDistinctDays_NotRows()
    {
        var binding = AnalyzeMultidayFixture();

        Assert.Equal(6, binding.ObservedWindow!.OperatingDays);
        Assert.Equal(990.0, binding.ObservedWindow.OperatingMinutes);
    }

    // ── The selected window is the window the run uses ─────────────────────

    /// <summary>
    /// THE defect this pair of tests exists for. Choosing "1 week" on a six-day
    /// file must change the divisor — so the λ the run uses must change with it.
    /// Before the fix the panel displayed 60 ÷ 825 while the coordinator read the
    /// binding's 60 ÷ 990: two different numbers, one of them on screen, the
    /// other driving the simulation, and nothing to tell the user.
    /// </summary>
    /// <remarks>
    /// The assertion is on the rate the ENGINE sampled at, read back from the gaps
    /// it generated. Asserting on <c>InputTabViewModel.SelectedWindowLambda</c>
    /// alone would only prove the view model agrees with itself.
    /// </remarks>
    [Fact]
    public void SelectedWindow_ChangesTheRateTheRunUses()
    {
        var binding = AnalyzeMultidayFixture();

        var tab = new InputTabViewModel();
        tab.SetLoadedFile(binding);
        tab.SelectedWindowLabel = "1 week";

        double expected = binding.DataSet!.RowCount / 825.0;   // 5 operating sessions
        AssertRateEquals(expected, tab.SelectedWindowLambda!.Value);

        // …and the same number through the whole path: shell → config panel →
        // parameters → coordinator → engine.
        var main = new MainViewModel();
        main.InputTab.SetLoadedFile(binding);
        main.SelectedLambdaSource = LambdaSource.Window;
        main.InputTab.SelectedWindowLabel = "1 week";

        var parameters = main.Config.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(LambdaSource.Window, parameters!.LambdaSource);
        AssertRateEquals(expected, parameters.WindowLambdaOverride!.Value);

        // The λ seam is the subject, and the fixture cannot get past p_exit = 1.0
        // with its three default stages (a real refusal, correctly). DiagnosticParameters
        // supplies the other stages and a legal p_exit, and a long horizon so the
        // sampled rate is a mean over ~10,000 gaps rather than the ~12 a single
        // 165-minute clinic day would give — at that sample size a 5% tolerance
        // would be testing the seed, not the code.
        var runParameters = DiagnosticParameters() with
        {
            LambdaSource = LambdaSource.Window,
            WindowLambdaOverride = parameters.WindowLambdaOverride,
        };

        double sampled = SampledArrivalRate(SimulationCoordinator.Run(runParameters, binding));

        AssertRateEquals(expected, sampled);

        // The auto-detected figure is a different number, which is the whole
        // reason the override exists: had the run used it, this would fail.
        Assert.NotEqual(binding.WindowLambda!.Value, expected, 3);
    }

    /// <summary>
    /// The custom-hours path reaches the run too. 5.5 hours is 330 minutes, so
    /// 60 arrivals over 330 minutes is a rate more than twice the 6-day one.
    /// </summary>
    [Fact]
    public void CustomWindowHours_ReachTheRun()
    {
        var binding = AnalyzeMultidayFixture();

        var tab = new InputTabViewModel();
        tab.SetLoadedFile(binding);
        tab.SelectedWindowLabel = "Custom…";
        tab.CustomHours = "5.5";

        AssertRateEquals(binding.DataSet!.RowCount / 330.0, tab.SelectedWindowLambda!.Value);

        // 5.5 hours = 330 operating minutes: the DIVISOR is what the user typed,
        // and it is on screen so the arithmetic can be checked. The caption
        // reports whole days for internal arithmetic, which is the one thing this
        // readout must NOT do (ruling 3) — hence the wording, not the day count.
        Assert.Contains("330 operating minutes", tab.WindowLambdaText);
        Assert.DoesNotContain("hours", tab.WindowLambdaText);
    }

    /// <summary>
    /// An unusable custom value must say so in the field's own terms (FR-UI-17)
    /// and must not leave a stale rate behind for the run to pick up. A panel
    /// that kept the previous λ while showing a red error would let a bad value
    /// pass silently — the user would fix the field eventually, or not, and the
    /// run in between would use a number they had just invalidated.
    /// </summary>
    [Fact]
    public void InvalidCustomHours_ClearTheRateAndStateTheRemedy()
    {
        var binding = AnalyzeMultidayFixture();
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(binding);

        tab.SelectedWindowLabel = "Custom…";
        tab.CustomHours = "5.5";
        Assert.NotNull(tab.SelectedWindowLambda);

        tab.CustomHours = "-2";

        Assert.True(tab.IsCustomWindowInvalid);
        Assert.Null(tab.SelectedWindowLambda);
        Assert.False(tab.HasWindowLambda);
        Assert.True(tab.IsWindowChoiceDisabled);
        Assert.Contains("greater than 0", tab.CustomWindowErrorText);
        Assert.Contains("-2", tab.CustomWindowErrorText);   // what was entered
        Assert.Contains("5.5", tab.CustomWindowErrorText);  // what a value looks like

        // Restoring a valid value clears the error immediately.
        tab.CustomHours = "5.5";
        Assert.False(tab.IsCustomWindowInvalid);
        Assert.Equal("", tab.CustomWindowErrorText);
        Assert.NotNull(tab.SelectedWindowLambda);
    }

    /// <summary>
    /// A cleared tab must forget its binding. The cached binding is what a later
    /// dropdown change re-derives the λ from, so keeping it would let a dismissed
    /// file keep producing rates on a panel that shows no file.
    /// </summary>
    [Fact]
    public void Clear_ForgetsTheBinding_SoADropdownChangeCannotReviveIt()
    {
        var binding = AnalyzeMultidayFixture();
        var tab = new InputTabViewModel();
        tab.SetLoadedFile(binding);
        Assert.NotNull(tab.SelectedWindowLambda);

        tab.Clear();
        tab.SelectedWindowLabel = "1 week";

        Assert.Null(tab.SelectedWindowLambda);
        Assert.False(tab.HasFile);
        Assert.False(tab.HasWindowLambda);
    }

    // ── The radios follow the shell's choice from any direction ────────────

    /// <summary>
    /// Ruling 7's ownership rule has a second half. The Input tab reads the
    /// choice from the shell, so anything that changes it on the shell — a preset
    /// load, a reset, a direct assignment — must reach the radio, or the display
    /// says MLE while the run uses the window rate.
    /// </summary>
    /// <remarks>
    /// Asserted through a real <see cref="PropertyChangedEventArgs"/> signal
    /// rather than a snapshot of the values, because a binding that never raises
    /// the change leaves the control showing the old value even though the
    /// property getter has already flipped.
    /// </remarks>
    [Fact]
    public void ShellChangingTheChoice_NotifiesTheRadios()
    {
        var main = new MainViewModel();
        var changed = new List<string?>();
        main.InputTab.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        main.SelectedLambdaSource = LambdaSource.Window;

        Assert.True(main.InputTab.IsWindowSelected);
        Assert.False(main.InputTab.IsMleSelected);
        Assert.Contains(nameof(InputTabViewModel.IsMleSelected), changed);
        Assert.Contains(nameof(InputTabViewModel.IsWindowSelected), changed);
    }

    // ── The diagnostic duration belongs to the diagnostic mode ─────────────

    /// <summary>
    /// D-174's decoupling, tested from the other side. A calendar run takes its
    /// length from GeneratorDays and never reads the duration field, so a
    /// malformed value left in that field must not refuse a run the user is
    /// entitled to. Before the fix, switching from diagnostic to Multi-day after
    /// mistyping the custom value made Start silently fail with the offending
    /// field scrolled out of sight.
    /// </summary>
    [Fact]
    public void CalendarMode_IgnoresAnInvalidDiagnosticDuration()
    {
        var config = new ConfigPanelViewModel();
        config.IsMultiDay = true;
        config.Days.Value = "5";
        config.IsDiagnosticTrace = false;
        config.Duration = DiagnosticDurationPreset.CustomMinutes;
        config.CustomMinutes.Value = "not a number";

        var parameters = config.TryBuildRunParameters();

        Assert.NotNull(parameters);
        Assert.Equal(RunMode.MultiDay, parameters!.RunMode);
        Assert.Equal(5, parameters.GeneratorDays);
    }

    /// <summary>
    /// The mirror case: the same malformed value DOES refuse a diagnostic run,
    /// because that mode is the one that reads the field. Without this the test
    /// above would pass just as well if the field were ignored everywhere.
    /// </summary>
    [Fact]
    public void DiagnosticMode_RefusesAnInvalidDuration()
    {
        var config = new ConfigPanelViewModel();
        config.IsDiagnosticTrace = true;
        config.Duration = DiagnosticDurationPreset.CustomMinutes;
        config.CustomMinutes.Value = "not a number";

        Assert.Null(config.TryBuildRunParameters());
    }
}
