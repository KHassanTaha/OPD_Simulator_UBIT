using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Stages;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Phase 8K (D-150): per-stage service families, the six-family spread model, and
/// the "defaults for new stages" contract.
/// </summary>
/// <remarks>
/// <para>
/// The invariant most of these tests protect is
/// <c>ServiceRate == 1 / Mean == μ</c> for all six families. It is not a style
/// preference: <c>StageSpec</c> (D-147) throws in Debug when a stage's rate and
/// its spec's mean disagree, and the Engine computes ρ = λ/(c·μ) from
/// <c>ServiceRate</c>. A mismatch would validate one model while sampling another,
/// producing plausible, silently wrong metrics.
/// </para>
/// <para>
/// The architecture that makes the invariant holdable is that μ is the ONLY location
/// parameter and the family's own parameters carry spread alone: Gamma's scale is
/// derived as Mean/k and Uniform's bounds as Mean±w, so no screen can describe two
/// different distributions at once.
/// </para>
/// <para>
/// Every test here is a single case, looping internally where a family set has to be
/// covered. A <c>[Theory]</c> would report one case per family and inflate the suite
/// count, which would misrepresent how many behaviours are actually specified.
/// </para>
/// </remarks>
public class Phase8KTests
{
    private const double Mu = 0.5; // per minute, rate-wise → Mean = 2.0

    /// <summary>Decimal places for derived means and rates, matching D-147's 1e-9 relative check.</summary>
    private const int Precision = 9;

    private static readonly DistributionFamily[] SpreadFamilies =
    {
        DistributionFamily.Normal,
        DistributionFamily.Lognormal,
        DistributionFamily.Gamma,
        DistributionFamily.Uniform,
    };

    private static readonly DistributionFamily[] NoSpreadFamilies =
    {
        DistributionFamily.Exponential,
        DistributionFamily.Deterministic,
    };

    /// <summary>A panel with N stages already created, so row seeding has run.</summary>
    private static ConfigPanelViewModel Panel(int stages)
    {
        var vm = new ConfigPanelViewModel();
        vm.StageCount.Value = stages.ToString();
        return vm;
    }

    private static StageRow Row(
        DistributionFamily service,
        string? stdDev = null,
        string? shape = null,
        string? spread = null) =>
        new()
        {
            ServiceFamily = service,
            ServiceStdDev = stdDev,
            ServiceShape = shape,
            ServiceSpread = spread,
        };

    private static DistributionSpec Spec(
        DistributionFamily family,
        string? stdDev = null,
        string? shape = null,
        string? spread = null) =>
        ConfigPanelViewModel.BuildSpec(Row(family, stdDev, shape, spread), Mu)
        ?? throw new InvalidOperationException($"BuildSpec refused {family}.");

    // ── 8K.1 · typed families, the notation, and the spread contract ────

    [Fact]
    public void StageRow_ServiceFamily_IsDistributionFamilyEnum()
    {
        // Why it matters: a string family must be translated at every boundary and a
        // typo only fails at runtime. Typing the row's family is what let BuildSpec drop
        // its name lookup entirely, and it is why an unrecognised family is now a
        // compile error rather than a ArgumentException from deep in a run.
        var row = Row(DistributionFamily.Normal);

        Assert.IsType<DistributionFamily>(row.ServiceFamily);

        row.ServiceFamily = DistributionFamily.Gamma;
        Assert.Equal(DistributionFamily.Gamma, row.ServiceFamily);
    }

    [Fact]
    public void StageRow_Defaults_ToExponentialServiceFamily()
    {
        var row = new StageRow();

        Assert.Equal(DistributionFamily.Exponential, row.ServiceFamily);
        Assert.Equal(DistributionFamily.Exponential, row.ArrivalFamily);
        Assert.Equal("M/M/1", row.SelectedModel);
    }

    [Fact]
    public void StageRow_AdvancedMode_FamilyChange_SetsCustomNotation()
    {
        // Why it matters: leaving the Kendall notation untouched would make the panel
        // claim M/M/1 while the stage runs Normal, so the notation has to admit it no
        // longer describes the row. Advanced is the only mode where the user owns the
        // family, so it is the only mode where the notation is disowned — outside it the
        // shorthand is authoritative and a family write must not detach the two.
        var advanced = new StageRow { UseAdvancedSetup = true };
        advanced.ServiceFamily = DistributionFamily.Lognormal;
        Assert.Equal(StageRow.CustomModel, advanced.SelectedModel);

        var simple = new StageRow();
        simple.ServiceFamily = DistributionFamily.Gamma;
        Assert.Equal("M/M/1", simple.SelectedModel);
    }

    [Fact]
    public void StageRow_SelectedModel_GGc_SetsCustomNotation()
    {
        // Why it matters: G/G/c names no family, so the row must not invent one and
        // must not let the notation's server count rewrite the row either. It asks the
        // parent to auto-fit instead — the event is the whole seam, and the families
        // must survive untouched until something answers it.
        var row = new StageRow();
        ModelAutoFitRequestedEventArgs? request = null;
        row.AutoFitRequested += (_, e) => request = e;
        row.SelectedModel = "G/G/2";

        Assert.NotNull(request);
        Assert.Equal("G/G/2", request!.Notation);
        Assert.Equal(DistributionFamily.Exponential, row.ServiceFamily);
        Assert.Equal("1", row.Servers.Value);
    }

    [Fact]
    public void NeedsShapeParameters_TracksTheFamiliesThatTakeASpread()
    {
        // Why it matters: the four spread families are under-described by a mean alone,
        // so each must offer exactly the one field it uses. Exponential and
        // Deterministic are fully described by their mean, so showing a spread input
        // there would invite a value the build path silently ignores — which is worse
        // than not showing it, because the user believes they configured something.
        Assert.All(
            SpreadFamilies,
            f => Assert.True(
                Row(f).NeedsShapeParameters,
                $"{f} needs a spread input but NeedsShapeParameters was false."));

        Assert.All(
            NoSpreadFamilies,
            f => Assert.False(
                Row(f).NeedsShapeParameters,
                $"{f} takes no spread, so a spread input would be ignored silently."));
    }

    [Fact]
    public void StageRow_SixFamilyOptions_AreAllEngineFamilies()
    {
        // Why it matters: the dropdown must not offer a family the sampler factory
        // cannot build, or the run fails at its last step with a sampler error. Asserting
        // the count AND the membership means a future family added to Core without a
        // matching option is caught here.
        var row = new StageRow();

        Assert.Equal(6, row.DistributionFamilies.Count);
        Assert.Equal(
            Enum.GetValues<DistributionFamily>().OrderBy(f => (int)f),
            row.DistributionFamilies.OrderBy(f => (int)f));
    }

    [Fact]
    public void ModelNotationParser_StandardModels_Are15InFixedOrder()
    {
        // Why it matters: the count and the order are a stated contract, not an
        // implementation detail — reordering would change which model a user picks by
        // position, and dropping G/G/c for c>1 would block a multi-stage network from
        // auto-fitting per stage. The detection check lives here because IsGeneralModel
        // is what routes a notation to auto-fit, so a false positive would auto-fit a
        // row the user set deliberately.
        var models = ModelNotationParser.StandardModels;

        Assert.Equal(15, models.Count);
        Assert.Equal(
            new[]
            {
                "M/M/1", "M/M/2", "M/M/3", "M/M/4", "M/M/5",
                "M/D/1", "M/D/2", "M/D/3",
                "D/M/1", "D/M/2",
                "G/G/1", "G/G/2", "G/G/3", "G/G/4", "G/G/5",
            },
            models);

        foreach (var c in new[] { 1, 2, 3, 4, 5 })
        {
            Assert.True(ModelNotationParser.IsGeneralModel($"G/G/{c}"));
        }

        Assert.False(ModelNotationParser.IsGeneralModel("M/M/1"));
        Assert.False(ModelNotationParser.IsGeneralModel("M/D/2"));
        Assert.False(ModelNotationParser.IsGeneralModel(null));
    }

    // ── 8K.2 · the six-family derivation ─────────────────────────────────

    [Fact]
    public void BuildSpec_Normal_UsesUserStdDev()
    {
        // Why it matters: μ owns the location, σ owns the spread, and nothing else is
        // read — so a Normal can only ever disagree with μ through its mean, which is
        // derived from μ and cannot. Lognormal is covered here too because it takes the
        // same StdDev slot and shares the mean derivation.
        foreach (var family in new[] { DistributionFamily.Normal, DistributionFamily.Lognormal })
        {
            var spec = Spec(family, stdDev: "0.4");

            Assert.Equal(family, spec.Family);
            Assert.Equal(1.0 / Mu, spec.Mean, precision: Precision);
            Assert.Equal(0.4, spec.StdDev!.Value, precision: Precision);
            Assert.Null(spec.Shape);
            Assert.Null(spec.Scale);
            Assert.Null(spec.Min);
            Assert.Null(spec.Max);
        }
    }

    [Fact]
    public void BuildSpec_Gamma_DerivesScaleFromMeanAndShape()
    {
        // k = 2, Mean = 2 → Scale = 1, so the sampler draws a Gamma whose mean is
        // exactly 2·1 = 2 = 1/μ. Entering θ as well would let the samples disagree with
        // the μ that ρ was computed from — the exact defect the derivation removes.
        var spec = Spec(DistributionFamily.Gamma, shape: "2");

        Assert.Equal(2.0, spec.Shape!.Value);
        Assert.Equal(1.0, spec.Scale!.Value, precision: Precision);
        Assert.Equal(2.0, spec.Shape!.Value * spec.Scale!.Value, precision: Precision);
    }

    [Fact]
    public void BuildSpec_Uniform_DerivesBoundsFromMeanAndHalfWidth()
    {
        // Mean = 2, w = 0.5 → Min = 1.5, Max = 2.5. Deriving the bounds makes a
        // symmetric spread about the user's μ structural: typing Min and Max separately
        // would allow Min > Max, or a midpoint that is not μ.
        var spec = Spec(DistributionFamily.Uniform, spread: "0.5");

        Assert.Equal(1.5, spec.Min!.Value, precision: Precision);
        Assert.Equal(2.5, spec.Max!.Value, precision: Precision);
        Assert.Equal(2.0, ((spec.Min!.Value + spec.Max!.Value) / 2.0), precision: Precision);
        Assert.True(spec.Min < spec.Max);
    }

    [Fact]
    public void BuildSpec_AllFamilies_SatisfyServiceRateEqualsOneOverMean()
    {
        // The D-147 gate. StageSpec's Debug check throws when a stage's rate and its
        // spec's mean disagree, so constructing a StageSpec here IS the assertion — a
        // relaxed version would let a run validate one model and sample another. Covers
        // all six, with each family's own spread parameter supplied.
        var cases = new (DistributionFamily Family, string? StdDev, string? Shape, string? Spread)[]
        {
            (DistributionFamily.Exponential, null, null, null),
            (DistributionFamily.Deterministic, null, null, null),
            (DistributionFamily.Normal, "0.4", null, null),
            (DistributionFamily.Lognormal, "0.4", null, null),
            (DistributionFamily.Gamma, null, "2", null),
            (DistributionFamily.Uniform, null, null, "0.5"),
        };

        foreach (var (family, stdDev, shape, spread) in cases)
        {
            var spec = Spec(family, stdDev, shape, spread);

            Assert.Equal(1.0 / Mu, spec.Mean, precision: Precision);
            var thrown = Record.Exception(() => new StageSpec("Probe", 1, Mu, spec));
            Assert.Null(thrown);
        }
    }

    [Fact]
    public void BuildSpec_BlankMean_DoesNotThrow()
    {
        // Why it matters: a blank μ must stay "fit me" and reach the coordinator, not
        // become a zero-mean spec that throws somewhere downstream with no clue which
        // stage or field caused it. A family with no spread of its own is the common
        // case, so that is the one asserted here.
        var spec = ConfigPanelViewModel.BuildSpec(Row(DistributionFamily.Exponential), null);

        Assert.NotNull(spec);
        Assert.True(double.IsNaN(spec!.Mean));
    }

    [Fact]
    public void BuildSpec_BadSpread_RefusesAndExplainsOnTheRow()
    {
        // Why it matters: the alternative is the sampler factory throwing
        // "requires StdDev > 0" from inside Core, naming neither the stage nor the field
        // the user must fix. The blur path shares the same rule, so both are checked:
        // FR-UI-17 wants the error at the field, not only when a run is refused.
        foreach (var family in SpreadFamilies)
        {
            var row = Row(family);

            var spec = ConfigPanelViewModel.BuildSpec(row, Mu);

            Assert.Null(spec);
            Assert.NotEqual(string.Empty, row.InlineError);
            Assert.Contains("greater than 0", row.InlineError, StringComparison.Ordinal);
            Assert.Contains("blank", row.InlineError, StringComparison.Ordinal);

            row.ValidateSpread();
            Assert.True(row.HasInlineError);
        }

        // A usable value clears it, which is what makes the field feel fixed rather
        // than permanently broken.
        var gamma = Row(DistributionFamily.Gamma, shape: "2");
        gamma.ValidateSpread();
        Assert.False(gamma.HasInlineError);

        gamma.ServiceShape = "0";
        gamma.ValidateSpread();
        Assert.True(gamma.HasInlineError);

        // A value that parses and is positive can still be unusable, and the refusal has
        // to name the specific problem. w = Mean gives Min = 0 and a larger w gives a
        // negative bound; DistributionSpec only checks Min < Max, so both would pass and
        // the run would quietly sample negative service times. μ = 0.5 → Mean = 2.
        foreach (var (halfWidth, accepted) in new[] { ("1.999", true), ("2", false), ("2.5", false) })
        {
            var row = Row(DistributionFamily.Uniform, spread: halfWidth);

            var spec = ConfigPanelViewModel.BuildSpec(row, Mu);

            if (accepted)
            {
                Assert.NotNull(spec);
                Assert.True(spec!.Min > 0);
            }
            else
            {
                Assert.Null(spec);
                Assert.Contains("less than the mean", row.InlineError, StringComparison.Ordinal);
                Assert.Contains(halfWidth, row.InlineError, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void StageRow_ChangingFamily_ClearsSpreadThatNoLongerApplies()
    {
        // Why it matters: a σ left behind by Normal would be read as Gamma's k after a
        // family switch, and k = 0.4 is a legal-looking but wrong model. The user never
        // sees the stale field — it is hidden — so nothing else would clean it up.
        var row = Row(DistributionFamily.Normal, stdDev: "0.4");

        row.ServiceFamily = DistributionFamily.Gamma;
        Assert.Null(row.ServiceStdDev);
        Assert.True(row.NeedsShape);

        row.ServiceFamily = DistributionFamily.Uniform;
        Assert.Null(row.ServiceShape);

        // And the derived visibility flags must follow the family, or the row keeps
        // rendering the previous family's input over the current family's parameter.
        Assert.False(row.NeedsShape);
        Assert.True(row.NeedsSpread);
    }

    // ── 8K.4 · defaults for new stages ───────────────────────────────────

    [Fact]
    public void NewStageRow_UsesCurrentDefaultServiceFamily()
    {
        // The panel owns rows from construction, so the contract is tested the only way
        // it occurs in practice: change the default, then ADD a stage. The new row must
        // pick the default up and the existing one must keep what it had.
        var vm = Panel(1);

        vm.DefaultStageServiceFamily = DistributionFamily.Gamma;
        vm.DefaultStageArrivalFamily = DistributionFamily.Deterministic;
        vm.StageCount.Value = "2";

        Assert.Equal(2, vm.StageRows.Count);
        Assert.Equal(DistributionFamily.Gamma, vm.StageRows[1].ServiceFamily);
        Assert.Equal(DistributionFamily.Deterministic, vm.StageRows[1].ArrivalFamily);
        Assert.Equal(DistributionFamily.Exponential, vm.StageRows[0].ServiceFamily);
    }

    [Fact]
    public void DefaultChange_DoesNotAlterExistingRows()
    {
        // Why it matters: a user who tuned one stage to Normal did not ask for the other
        // two to be rewritten when they later edited a default. Propagation is the
        // explicit Apply button and nothing else.
        var vm = Panel(2);
        var before = vm.StageRows.Select(r => r.ServiceFamily).ToArray();

        vm.DefaultStageServiceFamily = DistributionFamily.Uniform;

        Assert.Equal(before, vm.StageRows.Select(r => r.ServiceFamily));
    }

    [Fact]
    public void ApplyDefaultsToAllStages_SetsEveryRowAndMarksCustom()
    {
        // Why it matters: after this the shorthand no longer describes any row, so
        // leaving a stale M/M/1 beside a Gamma family would misreport what the run does.
        // It also covers the log noise regression: ApplyFamilies writes
        // SelectedModel = "Custom", which is not a notation, so without the suppression
        // guard the parse throws, is caught, and logs a Warning per row on every click.
        var vm = Panel(3);
        vm.StageRows[0].ServiceFamily = DistributionFamily.Normal;
        vm.DefaultStageServiceFamily = DistributionFamily.Gamma;
        vm.DefaultStageArrivalFamily = DistributionFamily.Deterministic;

        vm.ApplyDefaultsToAllStagesCommand.Execute(null);

        Assert.All(vm.StageRows, r =>
        {
            Assert.Equal(DistributionFamily.Gamma, r.ServiceFamily);
            Assert.Equal(DistributionFamily.Deterministic, r.ArrivalFamily);
            Assert.Equal(StageRow.CustomModel, r.SelectedModel);
        });
    }

    // ── 8K.3 · the row as a reusable control ────────────────────────────

    [AvaloniaFact]
    public void UI_AdvancedToggleRevealsFamilies_AndSpreadInputMatchesTheFamily()
    {
        // Why it matters: one spread input cannot describe four families. Showing σ for
        // a Gamma stage would invite a value the build path ignores, and hiding the k
        // input would leave the family unconfigurable. Each family must reveal exactly
        // the one field it uses, and μ must stay available for all six — μ is the only
        // location parameter, so no family may suppress it.
        var row = Row(DistributionFamily.Normal, stdDev: "0.4");

        // The panel normally sets this from the source mode; a bare row defaults to
        // hidden, which would mask what this test is about.
        row.IsMuVisible = true;
        var control = new StageRowControl { DataContext = row };
        var window = new Window { Content = control, Width = 600, Height = 900 };
        window.Show();

        try
        {
            var toggle = control.GetVisualDescendants()
                .OfType<ToggleSwitch>()
                .FirstOrDefault(t => t.Content?.ToString() == "Advanced");
            Assert.NotNull(toggle);

            // The toggle is always on screen; the family dropdowns it reveals start hidden.
            Assert.False(toggle!.IsChecked);
            Assert.True(toggle.IsVisible);
            Assert.False(FindLabel(control, "Arrival distribution")!.IsVisible);
            Assert.False(FindLabel(control, "Service distribution")!.IsVisible);

            toggle.IsChecked = true;
            Assert.True(toggle.IsVisible);
            Assert.True(FindLabel(control, "Arrival distribution")!.IsVisible);
            Assert.True(FindLabel(control, "Service distribution")!.IsVisible);

            var sigma = FindLabel(control, "Standard deviation σ")!;
            var shape = FindLabel(control, "Shape k")!;
            var width = FindLabel(control, "Half-width w")!;
            var mu = FindLabel(control, "Service rate μ")!;

            Assert.True(sigma.IsVisible);
            Assert.False(shape.IsVisible);
            Assert.False(width.IsVisible);
            Assert.True(mu.IsVisible);

            foreach (var (family, showSigma, showShape, showWidth) in new[]
                     {
                         (DistributionFamily.Gamma, false, true, false),
                         (DistributionFamily.Uniform, false, false, true),
                         (DistributionFamily.Exponential, false, false, false),
                     })
            {
                row.ServiceFamily = family;

                Assert.Equal(showSigma, sigma.IsVisible);
                Assert.Equal(showShape, shape.IsVisible);
                Assert.Equal(showWidth, width.IsVisible);
                Assert.True(mu.IsVisible);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Finds whichever labelled control carries the given Label text, whether it is a
    /// <see cref="ValidatedField"/> or a <see cref="SearchableDropdown"/>. Matching on
    /// the label rather than a name keeps the test tied to what the user reads, which is
    /// the thing that must not change.
    /// </summary>
    private static Control? FindLabel(Control root, string label) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c switch
            {
                ValidatedField f => f.Label == label,
                SearchableDropdown d => d.Label == label,
                _ => false,
            });
}
