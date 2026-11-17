namespace OpdSimulator.App.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.ViewModels;
using OpdSimulator.App.Views;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;
using Xunit;

/// <summary>
/// Phase 9P, unit axis (D-240): one <see cref="TimeUnit"/> state with five
/// members, reused by λ, the per-stage μ and the service spread, and shown by
/// exactly one selector.
/// </summary>
/// <remarks>
/// The old converters threw on a member they did not list, so "five members" is
/// not a cosmetic widening — an unlisted unit was a crash waiting to happen at
/// the moment the user picked it. Every assertion here is written so that a
/// missing case fails on the member that was forgotten, not on a copied literal.
/// </remarks>
public class Phase9PUnitAxisTests
{
    [Fact]
    public void AllFiveUnits_ConvertAndRoundTrip()
    {
        // Why it matters: λ converts through ToPerMinute and the spread converts
        // through ToMinutes. If the two mappings disagree, a μ typed in days beside
        // a σ typed in days describes a distribution with a different mean than the
        // user asked for — no exception, just a wrong simulation.
        foreach (var unit in Enum.GetValues<TimeUnit>())
        {
            double minutesPerUnit = TimeUnits.MinutesPerUnit(unit);
            Assert.True(minutesPerUnit > 0 && double.IsFinite(minutesPerUnit), $"{unit} has no minute mapping");

            // The app's mapping is the Data layer's, so the fit captions and the run
            // cannot drift apart.
            Assert.Equal(
                FittingTimeUnits.MinutesPerUnit(TimeUnits.ToFitting(unit)),
                minutesPerUnit);

            // Forward: typing one unit's worth of minutes must land on exactly one
            // event per minute.
            var lambdaPanel = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true };
            lambdaPanel.TimeUnit = unit;
            lambdaPanel.ManualLambda.Value = minutesPerUnit.ToString("R");
            double? perMinute = lambdaPanel.TryBuildRunParameters()?.ManualArrivalRate;
            Assert.Equal(1.0, perMinute!.Value, 10);

            // Backward: a spread of one unit must become that many minutes in the
            // spec. The value typed is 1, not minutesPerUnit: the field is read in
            // `unit`, so typing the minute count there would mean "60 hours", and
            // the expectation would have to square the conversion to pass — which is
            // a fixture bug that looks like a converter bug.
            var row = new StageRow
            {
                ServiceFamily = DistributionFamily.Normal,
                ServiceStdDev = "1",
            };
            var spec = ConfigPanelViewModel.BuildSpec(row, 1.0, unit);
            Assert.NotNull(spec);
            Assert.Equal(minutesPerUnit, spec!.StdDev!.Value, 10);
        }
    }

    [Fact]
    public void LambdaInDays_AndSessions_ReachesParametersPerMinute()
    {
        // Why it matters: Days and Sessions were added so a clinic can be described
        // in its own terms. If either were accepted by the enum but not the
        // converter, the selector would offer a unit the run never honours.
        var days = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true, TimeUnit = TimeUnit.Days };
        days.ManualLambda.Value = "1440"; // one patient per day

        var sessions = new ConfigPanelViewModel { ParametersIsOptionalEnabled = true, TimeUnit = TimeUnit.Sessions };
        sessions.ManualLambda.Value = "165"; // one patient per 165-minute session

        Assert.Equal(1.0, days.TryBuildRunParameters()!.ManualArrivalRate!.Value, 10);
        Assert.Equal(1.0, sessions.TryBuildRunParameters()!.ManualArrivalRate!.Value, 10);
    }

    [Fact]
    public void MuInDays_AndSessions_ReachesParametersPerMinute()
    {
        // Why it matters: R-UNIT-1 says one unit governs λ AND μ. The per-stage μ
        // used to be minutes-only regardless of the selector, so a per-hour μ sat
        // beside a per-hour λ and the run read it 60× too fast.
        foreach (var (unit, typed, expected) in new[]
        {
            (TimeUnit.Days, "2880", 2.0),
            (TimeUnit.Sessions, "330", 2.0),
        })
        {
            var vm = new ConfigPanelViewModel { TimeUnit = unit, SourceMode = DataSourceMode.EnterManually };
            vm.ManualLambda.Value = "0.5";
            foreach (var row in vm.StageRows)
            {
                row.MuValue = typed;
            }

            var parameters = vm.TryBuildRunParameters();
            Assert.NotNull(parameters);
            Assert.All(
                parameters!.ManualServiceRates,
                rate => Assert.Equal(expected, rate!.Value, 10));
        }
    }

    [AvaloniaFact]
    public void ExactlyOneUnitSelector_ExistsInTheVisualTree()
    {
        // Why it matters: the defect was two selectors that could disagree. Hiding
        // the second one would leave the disagreement reachable from a preset; the
        // assertion therefore looks for its absence, not for a visibility flag.
        var window = new MainWindow();
        window.Show();
        try
        {
            var unitSelectors = window.GetVisualDescendants().OfType<TimeUnitSelector>().ToList();
            Assert.Single(unitSelectors);

            var fittingUnitFields = window.GetVisualDescendants().OfType<DropdownField>()
                .Where(f => f.Label == "Distribution fitting unit")
                .ToList();
            Assert.Empty(fittingUnitFields);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void FittingUnitLabel_MatchesTimeUnit_ForAllFive()
    {
        // Why it matters: the Input tab reports the unit, and it must report the one
        // the run uses. A second stored copy would let the caption say "hours" while
        // the parameters were per minute.
        var main = new MainViewModel();

        foreach (var unit in Enum.GetValues<TimeUnit>())
        {
            main.Config.TimeUnit = unit;

            Assert.Equal(TimeUnits.ToFitting(unit), main.InputTab.SelectedFittingUnit);
            Assert.Equal(TimeUnits.Label(unit), main.InputTab.SelectedFittingUnitLabel);
        }
    }

    [Fact]
    public void NoFittingUnitState_RemainsInConfigPanelViewModel()
    {
        // Why it matters: deleting the selector's XAML while leaving its backing
        // property would leave two homes for the unit — the exact seam 9P closed.
        // Reflection is deliberate: a future re-add fails here even if nothing binds
        // it yet.
        var members = typeof(ConfigPanelViewModel)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(m => m.Name)
            .Where(n => n.Contains("FittingUnit", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            members.Count == 0,
            "ConfigPanelViewModel still exposes: " + string.Join(", ", members));
    }

    [AvaloniaFact]
    public void UnitSelector_IsReachableByKeyboard_AndEscapeClosesIt()
    {
        // Why it matters: AGENTS §16.7/§16.8. The phase moved the ONE unit selector
        // and deleted the other, so the tab order of the Simulation panel changed.
        // A control that is present but only reachable with a mouse is not shipped:
        // the assertion therefore Tabs in from the window rather than calling
        // Focus(), which is the path :focus-visible actually follows.
        //
        // The selector lives inside "3 · Parameters", which starts disabled because
        // the section is optional. So the walkthrough first enables it through the
        // section's own switch — the same key sequence a user performs — and only
        // then asks whether the selector is in the chain.
        var window = new MainWindow();
        window.Width = 1200;
        window.Height = 900;
        window.Show();
        try
        {
            window.UpdateLayout();
            var main = Assert.IsType<MainViewModel>(window.DataContext);

            var section = window.GetVisualDescendants().OfType<CollapsibleSection>()
                .Single(s => s.Title?.Contains("Parameters", StringComparison.Ordinal) == true);
            var enableSwitch = section.GetVisualDescendants().OfType<ToggleSwitch>().First();

            bool onSwitch = false;
            for (int i = 0; i < 40 && !onSwitch; i++)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "Tab");
                window.UpdateLayout();
                onSwitch = enableSwitch.IsKeyboardFocusWithin;
            }

            Assert.True(onSwitch, "the Parameters enable switch must be reachable by Tab");

            // Enter, not Space: on this Avalonia build the Fluent ToggleSwitch
            // activates on Enter and ignores Space. That is pre-existing behaviour of
            // the 4c.2 control, not something Phase 9P changed, so the walkthrough
            // uses the key that works and the gap is recorded in PROGRESS.md rather
            // than papered over by a programmatic write.
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
            window.UpdateLayout();
            Assert.True(main.Config.ParametersIsOptionalEnabled, "Enter must enable the optional section");

            var selector = section.GetVisualDescendants().OfType<TimeUnitSelector>().Single();
            var combo = selector.FindDescendantOfType<DropdownField>()!
                .FindDescendantOfType<ComboBox>()!;

            bool reached = false;
            for (int i = 0; i < 40 && !reached; i++)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, "Tab");
                window.UpdateLayout();
                reached = combo.IsKeyboardFocusWithin;
            }

            Assert.True(reached, "the unit selector must be reachable by Tab once the section is on");

            // Escape closes the popup, and it must not take the focus away with it
            // (D-169: assert where the user is left, not just that a flag flipped).
            combo.IsDropDownOpen = true;
            window.UpdateLayout();
            Assert.True(combo.IsDropDownOpen);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "Escape");
            window.UpdateLayout();

            Assert.False(combo.IsDropDownOpen, "Escape must close the unit dropdown");
            Assert.True(combo.IsKeyboardFocusWithin, "focus must stay on the selector after Escape");
        }
        finally
        {
            window.Close();
        }
    }
}
