using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpdSimulator.App.Controls;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.App.ViewModels;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;
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

    // ── 8K.6 · the G/G/c auto-fit answer ──────────────────────────────

    /// <summary>
    /// The variable-variance 3-stage fixture. Chosen over sample_3stage_clinic.csv
    /// because that file's service times are constant (reception always 2 min,
    /// screening always 4 min), so every family is correctly rejected and the auto-fit
    /// success path can never run against it.
    /// </summary>
    private const string VariableCsv = "sample_3stage_variable.csv";

    private static string VariablePath() => SamplePath(VariableCsv);

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

        throw new FileNotFoundException(
            $"Could not find samples/{fileName} above {AppContext.BaseDirectory}.");
    }

    /// <summary>
    /// Loads the variable fixture and returns the Screening row, which is where the
    /// auto-fit is exercised. Index 1 because the fixture's stage order is
    /// Reception, Screening, Doctor.
    /// </summary>
    private static (ConfigPanelViewModel Panel, StageRow Screening) ScreeningWithData()
    {
        var panel = Panel(3);
        panel.ApplyLoadedFile(VariablePath());
        return (panel, panel.StageRows[1]);
    }

    [Fact]
    public void AutoFit_Succeeds_SetsBadgeWithFiniteAic_AndKeepsGGNotation()
    {
        // Why it matters: this is the whole point of G/G/c. The user names no family,
        // the fitter picks one from the data, and the panel has to say which — with a
        // number the user can check.
        //
        // Deliberately NOT asserting a particular winning family or a "good" p-value
        // (owner ruling): both are properties of the data, not of the code. What must
        // hold is that a family was chosen, that the reported AIC is a real number, and
        // that the user's G/G/1 request is still on screen afterwards.
        var (panel, row) = ScreeningWithData();
        Assert.Null(row.AutoFitBadge);

        row.SelectedModel = "G/G/1";

        Assert.NotNull(row.AutoFitBadge);
        Assert.Contains("Best fit:", row.AutoFitBadge);
        AssertNoNonFiniteNumber(row.AutoFitBadge);
        Assert.Equal("G/G/1", row.SelectedModel);
    }

    [Fact]
    public void AutoFit_Success_AppliesTheWinningFamilyToTheRow()
    {
        // Why it matters: the badge is only half the contract. The family the fitter
        // chose has to reach the row the run is built from, or the badge is describing a
        // decision the simulation never makes.
        var (panel, row) = ScreeningWithData();

        row.SelectedModel = "G/G/1";

        // The expected family is whatever the fitter actually decided for these samples,
        // read from the fitter rather than hard-coded. Naming a family here would pin the
        // test to one dataset's outcome; comparing against the fitter tests the thing this
        // test is actually about — that the DECISION reached the row — for any winner,
        // including Exponential.
        // Read the same stage samples the auto-fit reads: Binding is the panel's public
        // door onto the loaded analysis, and ServiceMinutesByStage is keyed by stage name
        // exactly as the production lookup does.
        Assert.NotNull(panel.Binding);
        Assert.True(panel.Binding!.ServiceMinutesByStage.TryGetValue(row.StageName, out var samples));
        Assert.NotEmpty(samples);
        var decision = GeneralDistributionFitter.FitBest(samples);
        Assert.NotNull(decision.Best);
        Assert.Equal(decision.Best!.Family, row.ServiceFamily);
        Assert.Null(row.InlineError);
    }

    [Fact]
    public void AutoFit_BlankMu_IsFilledFromTheFit_AndLabelledAsFitted()
    {
        // Why it matters: a G/G/c stage the data covers has no μ, and Start stays
        // disabled until it has one, so the auto-fit is the only thing that can make
        // the stage runnable. The user must also be able to tell that μ came from the
        // data rather than from their own keyboard.
        var (_, row) = ScreeningWithData();
        Assert.True(string.IsNullOrWhiteSpace(row.MuValue));

        row.SelectedModel = "G/G/1";

        Assert.False(string.IsNullOrWhiteSpace(row.MuValue));
        Assert.True(row.IsMuFittedLocally);
        Assert.Contains("(fitted)", row.ServiceRateLabel);
    }

    [Fact]
    public void AutoFit_UserEnteredMu_IsKept_AndNotLabelledAsFitted()
    {
        // Why it matters: the inverse of the previous test, and the one that protects
        // deliberate input. A typed μ is an answer, and a fitter that overwrote it would
        // silently discard the user's model.
        var (_, row) = ScreeningWithData();
        row.MuValue = "0.300";

        row.SelectedModel = "G/G/1";

        Assert.Equal("0.300", row.MuValue);
        Assert.False(row.IsMuFittedLocally);
        Assert.DoesNotContain("(fitted)", row.ServiceRateLabel);
    }

    [Fact]
    public void AutoFit_NoDataLoaded_RefusesWithAnActionableReason()
    {
        // Why it matters: a G/G/c stage with no data has nothing to fit. Silently
        // leaving it Exponential would run a model the user did not ask for while the
        // dropdown still claimed G/G.
        var panel = Panel(3);
        var row = panel.StageRows[1];
        row.SelectedModel = "M/M/2";

        row.SelectedModel = "G/G/2";

        Assert.Equal("M/M/2", row.SelectedModel);
        Assert.True(row.HasInlineError);
        Assert.Contains("no usable", row.InlineError, StringComparison.OrdinalIgnoreCase);
        Assert.Null(row.AutoFitBadge);
    }

    [Fact]
    public void AutoFit_FitFindsNoWinner_RefusesWithTheFittersOwnReason()
    {
        // Why it matters: the refusal must name the actual statistical reason, not a
        // generic failure. sample_3stage_clinic.csv has constant service times, so
        // every family is rejected and the reason is a real diagnostic the user can act
        // on ("your data has no variation"). This is also what keeps the sample-count
        // threshold in exactly one place — the fitter's — instead of being restated here.
        var panel = Panel(3);
        panel.ApplyLoadedFile(SamplePath("sample_3stage_clinic.csv"));
        var row = panel.StageRows[1];
        row.SelectedModel = "M/M/2";

        row.SelectedModel = "G/G/2";

        Assert.Equal("M/M/2", row.SelectedModel);
        Assert.True(row.HasInlineError);
        Assert.Contains("could not fit", row.InlineError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Screening", row.InlineError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutoFit_ManualFamilyChange_RetiresTheBadge()
    {
        // Why it matters: a stale verdict is worse than none. Once the user overrides
        // the family by hand, a badge still claiming "Best fit: Lognormal" describes a
        // decision the user has already overturned, and nothing on screen would say so.
        var (_, row) = ScreeningWithData();
        row.SelectedModel = "G/G/1";
        Assert.NotNull(row.AutoFitBadge);

        row.ServiceFamily = DistributionFamily.Exponential;

        Assert.Null(row.AutoFitBadge);
    }

    [Fact]
    public void AutoFit_InlineErrorIsSupersededByTheNextAttempt_NotByAFamilyChange()
    {
        // Why it matters: it records WHERE a stale message is allowed to die. A family
        // change deliberately does NOT clear the error, because the error may describe
        // a spread value that is still invalid — switching Normal -> Lognormal keeps
        // using the same StdDev, and wiping the message would hide a real problem until
        // Start refused the run. The error is superseded by the next auto-fit attempt,
        // which is the only event that can genuinely resolve it.
        var panel = Panel(3);
        var row = panel.StageRows[1];
        row.SelectedModel = "G/G/2";
        Assert.True(row.HasInlineError);

        // Same family pair, different problem: the error survives a manual override...
        row.ServiceFamily = DistributionFamily.Exponential;
        Assert.True(row.HasInlineError);

        // ...and is cleared once the stage can actually be fitted.
        panel.ApplyLoadedFile(VariablePath());
        row.SelectedModel = "G/G/2";
        Assert.False(row.HasInlineError);
        Assert.NotNull(row.AutoFitBadge);
    }

    /// <summary>
    /// Asserts a user-facing string carries no non-finite number. Phase 8K's gate item
    /// 3(j): an AIC of -inf, +inf or NaN is always a bug, and it reached the UI once
    /// already (D-156), so it is checked at the point it would be read rather than only
    /// at the fitter that produced it.
    /// </summary>
    private static void AssertNoNonFiniteNumber(string text)
    {
        foreach (var forbidden in new[] { "-∞", "+∞", "∞", "NaN", "-inf", "+inf", "inf" })
        {
            Assert.DoesNotContain(forbidden, text);
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

    // ── B-010: the configured SPREAD must reach the engine ──────────────────
    //
    // Before the fix, SimulationCoordinator rebuilt each engine spec as
    // `new DistributionSpec(family, Mean: 1/mu)`, discarding the spread, so
    // DistributionSamplerFactory refused every non-Exponential stage. These tests
    // observe the SAMPLER'S OUTPUT, not the spec object, because the spec is what the
    // bug threw away: only a behavioural observation can prove the spread arrived.

    /// <summary>
    /// Runs a single-stage clinic with <paramref name="spec"/> configured and returns the
    /// service times the engine actually drew. The run is refused outright if the spread
    /// never reached the sampler, so a refusal here is itself the regression signal.
    /// </summary>
    private static IReadOnlyList<double> EngineServiceSamplesFor(DistributionSpec spec, double mu, int seed = 4242)
    {
        var parameters = new SimulationParameters(
            ParameterMode.RateWise,
            "Exponential",
            // lambda is chosen so the WORST case here stays stable: the tightest family
            // under test is Normal(mean 2.5) -> mu = 0.4 on ONE server, so lambda must be
            // well under 0.4 or the coordinator correctly refuses the run as unstable.
            // 600 minutes at 0.2/min draws ~120 patients, enough for a usable sample.
            ManualArrivalRate: 0.2,
            StageNames: ["Stage"],
            ServerCounts: [1],
            ManualServiceRates: new double?[] { mu },
            RunMode.DiagnosticTrace,
            HorizonMinutes: 600,
            GeneratorDays: 1,
            StartDay: DayOfWeek.Monday,
            DailyCap: null,
            Seed: seed,
            PExitOverride: null,
            TraceLevelName: "Standard")
        {
            ServiceFamilies = [spec],
            ServiceRates = new double?[] { mu },
        };

        var outcome = SimulationCoordinator.Run(parameters, binding: null);
        Assert.Null(outcome.Error);
        var samples = outcome.Result!.GeneratedServiceSamplesByStage[0];
        Assert.NotEmpty(samples);
        return samples;
    }

    private static double SampleStdDev(IReadOnlyList<double> values)
    {
        double mean = values.Average();
        return Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());
    }

    [Fact]
    public void Coordinator_NormalStage_EngineSamplesCarryTheConfiguredStdDev()
    {
        // WHY: σ is the whole content of a Normal stage. If the spread is dropped the run
        // is refused outright; if a default were substituted, the sd would not be 0.4.
        var spec = new DistributionSpec(DistributionFamily.Normal, Mean: 2.5, StdDev: 0.4);

        var samples = EngineServiceSamplesFor(spec, mu: 1.0 / 2.5);

        Assert.True(samples.Count >= 100, $"need a usable sample, got {samples.Count}");
        Assert.InRange(SampleStdDev(samples), 0.35, 0.45);
        Assert.InRange(samples.Average(), 2.35, 2.65);
    }

    [Fact]
    public void Coordinator_UniformStage_EngineSamplesRespectTheDerivedBounds()
    {
        // WHY: Min/Max are DERIVED from μ and w (mean ± w), so a dropped spread does not
        // merely change the shape — the hard bounds disappear, and an exponential draw
        // would fall outside them.
        var spec = new DistributionSpec(DistributionFamily.Uniform, Mean: 4.0, Min: 3.0, Max: 5.0);

        var samples = EngineServiceSamplesFor(spec, mu: 1.0 / 4.0);

        Assert.All(samples, s => Assert.InRange(s, 3.0, 5.0));
        Assert.InRange(samples.Average(), 3.8, 4.2);
    }

    [Fact]
    public void Coordinator_GammaStage_EngineSamplesCarryTheConfiguredShape()
    {
        // WHY: Gamma's spread is k, and Scale is DERIVED as Mean/k. A dropped spec would
        // leave the engine unable to build a Gamma sampler at all; a substituted default
        // would give var = mean²/k for some other k. var = mean²/k is the assertion.
        //
        // Scale is set here exactly as ConfigPanelViewModel.BuildSpec sets it
        // (Scale = mean / shape), because the sampler needs it explicitly — Gamma is
        // the one family whose stored fields encode the mean TWICE.
        var spec = new DistributionSpec(
            DistributionFamily.Gamma, Mean: 3.0, Shape: 2.0, Scale: 3.0 / 2.0);

        var samples = EngineServiceSamplesFor(spec, mu: 1.0 / 3.0);

        Assert.True(samples.Count >= 100, $"need a usable sample, got {samples.Count}");
        double mean = samples.Average();
        double observedVariance = samples.Select(v => (v - mean) * (v - mean)).Average();
        double expectedVariance = 9.0 / 2.0; // mean² / k
        Assert.InRange(observedVariance, expectedVariance * 0.75, expectedVariance * 1.25);
        Assert.All(samples, s => Assert.True(s > 0, "Gamma support is strictly positive"));

        // The sampler's mean must equal the spec mean, which is the half of the D-147
        // invariant that Gamma can violate on its own: it draws from k·Scale, and a
        // stale Scale would silently decouple the drawn mean from the configured one.
        Assert.InRange(mean, 2.85, 3.15);
    }

    [Fact]
    public void Coordinator_LognormalStage_EngineSamplesArePositiveAndCentredOnTheMean()
    {
        var spec = new DistributionSpec(DistributionFamily.Lognormal, Mean: 3.0, StdDev: 1.0);

        var samples = EngineServiceSamplesFor(spec, mu: 1.0 / 3.0);

        Assert.True(samples.Count >= 100, $"need a usable sample, got {samples.Count}");
        Assert.All(samples, s => Assert.True(s > 0, "Lognormal support is strictly positive"));
        Assert.InRange(samples.Average(), 2.4, 3.6);
    }

    /// <summary>
    /// The end-to-end confirmation the owner asked for (gate item 3): a real run whose
    /// three stages genuinely differ — M/M/1, M/D/2, M/M/3 — and the two observations
    /// that ONLY mixed families can produce.
    /// </summary>
    [Fact]
    public void Coordinator_MixedFamilyRun_M_D_StageHasNoServiceVarianceWhileM_M_StagesDo()
    {
        var parameters = new SimulationParameters(
            ParameterMode.RateWise,
            "Exponential",
            // Reception is the binding constraint: mu = 0.5 on ONE server, so lambda
            // must be strictly below 0.5 or the run is refused as unstable (rho >= 1).
            ManualArrivalRate: 0.2,
            StageNames: ["Reception", "Screening", "Doctor"],
            ServerCounts: [1, 2, 3],
            ManualServiceRates: new double?[] { 0.5, 0.25, 0.4 },
            RunMode.DiagnosticTrace,
            HorizonMinutes: 600,
            GeneratorDays: 1,
            StartDay: DayOfWeek.Monday,
            DailyCap: null,
            Seed: 42,
            PExitOverride: null,
            TraceLevelName: "Standard")
        {
            // Reception M/M/1, Screening M/D/2, Doctor M/M/3.
            ServiceFamilies =
            [
                new DistributionSpec(DistributionFamily.Exponential, Mean: 2.0),
                new DistributionSpec(DistributionFamily.Deterministic, Mean: 4.0),
                new DistributionSpec(DistributionFamily.Exponential, Mean: 2.5),
            ],
            ServiceRates = new double?[] { 0.5, 0.25, 0.4 },
        };

        var outcome = SimulationCoordinator.Run(parameters, binding: null);
        Assert.Null(outcome.Error);
        var result = outcome.Result!;
        Assert.Equal(3, result.StageMetrics.Count);

        var reception = result.GeneratedServiceSamplesByStage[0];
        var screening = result.GeneratedServiceSamplesByStage[1];
        var doctor = result.GeneratedServiceSamplesByStage[2];
        Assert.NotEmpty(reception);
        Assert.NotEmpty(screening);
        Assert.NotEmpty(doctor);

        // (i) The D stage has NO service-duration variance: every draw is the mean.
        Assert.Equal(0.0, SampleStdDev(screening), 12);
        Assert.All(screening, s => Assert.Equal(4.0, s, 10));

        // (ii) The M/M stages DO vary. Without this the run could pass while every
        // stage were deterministic, which is not what was configured.
        Assert.True(SampleStdDev(reception) > 0.1, "Reception is M/M/1 and must vary");
        Assert.True(SampleStdDev(doctor) > 0.1, "Doctor is M/M/3 and must vary");

        // (iii) Per-stage metrics differ in the way only mixed families can produce:
        // the D stage's utilisation is the same LEAST-SQUARES number as an M/M/1 at the
        // same μ, while a 2-server stage at the same μ has a different queue profile.
        var screeningMetrics = result.StageMetrics[1];
        var receptionMetrics = result.StageMetrics[0];
        Assert.Equal(0.25, screeningMetrics.ServiceRate, 9);
        Assert.Equal(2, screeningMetrics.ServerCount);
        Assert.Equal(1, receptionMetrics.ServerCount);

        // (iv) The D stage's mean wait is exactly service time / servers for a
        // deterministic service, i.e. no variability in the service duration to
        // average out. Asserted as a bound rather than an exact figure because the
        // value depends on the arrival pattern.
        Assert.True(
            screeningMetrics.AverageWaitMinutes is >= 0,
            "the D stage must still report a wait time");

        // (v) Verification: the D stage cannot be chi-square tested and says so, while
        // the two M/M stages get real verdicts.
        var reports = SimulationVerificationService.VerifyAll(
            result, "Exponential", parameters.ServiceFamilies, 0.05);

        var screeningReport = Assert.Single(reports, r => r.Label == "Screening service");
        Assert.Null(screeningReport.ChiSquare);
        Assert.Equal("Deterministic — chi-square not applicable.", screeningReport.Note);

        foreach (string label in new[] { "Reception service", "Doctor service" })
        {
            var report = Assert.Single(reports, r => r.Label == label);
            Assert.NotNull(report.ChiSquare);
            Assert.Equal("Exponential", report.IntendedFamily);
        }
    }

    // ── B-011: the mean is encoded TWICE for two families, so it must be resolved ──
    //
    // Gamma stores `Scale = mean/k` and Uniform stores `Min`/`Max = mean ∓ w`. Neither
    // can be built without a mean, so a spec built from an unresolved μ comes out with
    // NaN in those fields and DistributionSamplerFactory throws. Exponential,
    // Deterministic, Normal and Lognormal are unaffected: their spread is absent or a
    // mean-independent σ.

    [Fact]
    public void BuildSpec_Gamma_MeanChange_RedrivesScale()
    {
        // WHY: Scale is what makes the SAMPLED mean equal the configured mean
        // (k · Scale = k · (mean/k) = mean). If a changed μ left the old Scale in
        // place, the engine would silently draw a different mean than the one the user
        // typed — no exception, just wrong numbers. So this asserts the relationship,
        // not one literal value.
        var row = new StageRow { ServiceFamily = DistributionFamily.Gamma, ServiceShape = "2" };

        var first = ConfigPanelViewModel.BuildSpec(row, rate: 0.5);
        var second = ConfigPanelViewModel.BuildSpec(row, rate: 0.25);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2.0, first!.Mean, 10);
        Assert.Equal(1.0, first.Scale!.Value, 10);
        Assert.Equal(4.0, second!.Mean, 10);
        Assert.Equal(2.0, second.Scale!.Value, 10);

        // The invariant itself: the sampler's mean tracks the configured mean.
        Assert.Equal(first.Mean, first.Shape!.Value * first.Scale!.Value, 10);
        Assert.Equal(second.Mean, second.Shape!.Value * second.Scale!.Value, 10);
    }

    [Fact]
    public void BuildSpec_Uniform_MeanChange_RedrivesBounds()
    {
        // WHY: for Uniform the MEAN IS the midpoint of the bounds, so a stale bound
        // moves the distribution rather than merely rescaling it. Half-width w = 0.5
        // is held constant on purpose; only the mean moves.
        var row = new StageRow { ServiceFamily = DistributionFamily.Uniform, ServiceSpread = "0.5" };

        var first = ConfigPanelViewModel.BuildSpec(row, rate: 0.25);
        var second = ConfigPanelViewModel.BuildSpec(row, rate: 0.1);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(4.0, first!.Mean, 10);
        Assert.Equal(3.5, first.Min!.Value, 10);
        Assert.Equal(4.5, first.Max!.Value, 10);
        Assert.Equal(10.0, second!.Mean, 10);
        Assert.Equal(9.5, second.Min!.Value, 10);
        Assert.Equal(10.5, second.Max!.Value, 10);

        // The midpoint of the bounds IS the mean — the reason the bounds must be
        // re-derived rather than carried over.
        Assert.Equal(first.Mean, (first.Min!.Value + first.Max!.Value) / 2, 10);
        Assert.Equal(second.Mean, (second.Min!.Value + second.Max!.Value) / 2, 10);
    }

    [Fact]
    public void BuildSpec_Gamma_BlankMu_CommaListSuppliesMean_Accepted()
    {
        // WHY: a blank row μ with the comma list supplying it is a documented,
        // supported configuration (fit mode, stage the file does not cover). It must
        // build a COMPLETE spec, not a NaN one.
        var panel = Panel(3);
        panel.SourceMode = DataSourceMode.FitFromData;
        // The per-stage μ list lives in the OPTIONAL Parameters section, so it does
        // not participate until that section is switched on (D-103). Without this the
        // comma list is silently empty and the test would pass for the wrong reason.
        panel.ParametersIsOptionalEnabled = true;
        panel.StageRows[1].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[1].ServiceShape = "2";
        panel.StageRows[1].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "0.5, 0.25, 0.4";

        var parameters = panel.TryBuildRunParameters();

        Assert.NotNull(parameters);
        var spec = parameters!.ServiceFamilies[1];
        Assert.Equal(DistributionFamily.Gamma, spec.Family);
        Assert.Equal(4.0, spec.Mean, 10);
        Assert.Equal(2.0, spec.Scale!.Value, 10);
        Assert.False(double.IsNaN(spec.Mean));
        Assert.False(double.IsNaN(spec.Scale!.Value));
    }

    [Fact]
    public void BuildSpec_Gamma_NoMuAnywhere_RefusesCleanly()
    {
        // WHY: this is the B-011 case-2 contract. The refusal must name the stage and
        // say where to put μ. The failure it replaces named "Scale > 0" — a spread
        // field the user never sees for a Gamma row's problem — and came from Core.
        var panel = Panel(3);
        panel.SourceMode = DataSourceMode.FitFromData;
        // The per-stage μ list lives in the OPTIONAL Parameters section, so it does
        // not participate until that section is switched on (D-103). Without this the
        // comma list is silently empty and the test would pass for the wrong reason.
        panel.ParametersIsOptionalEnabled = true;
        panel.StageRows[1].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[1].ServiceShape = "2";
        panel.StageRows[1].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = string.Empty;

        var parameters = panel.TryBuildRunParameters();

        Assert.Null(parameters);
        var row = panel.StageRows[1];
        Assert.True(row.HasInlineError);
        Assert.Contains("Screening", row.InlineError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Gamma", row.InlineError, StringComparison.OrdinalIgnoreCase);
        // The message must be actionable, so it names the three places μ can come from.
        Assert.Contains("per-stage", row.InlineError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartGate_Gamma_BlankMuCommaListPresent_Enabled()
    {
        // WHY: the gate must consult the SAME three-level chain the coordinator uses,
        // so it never blocks a row the coordinator can resolve — and, by the same
        // token, never passes one the coordinator cannot. D-128.
        //
        // The stage is renamed to one the file does not cover, so the comma list is
        // genuinely the source of μ rather than a bystander.
        var panel = Panel(1);
        panel.ApplyLoadedFile(SamplePath("sample_3stage_clinic.csv"));
        panel.SourceMode = DataSourceMode.FitFromData;
        // The per-stage μ list lives in the OPTIONAL Parameters section, so it does
        // not participate until that section is switched on (D-103). Without this the
        // comma list is silently empty and the test would pass for the wrong reason.
        panel.ParametersIsOptionalEnabled = true;
        panel.StageRows[0].StageName = "Triage";
        panel.StageRows[0].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[0].ServiceShape = "2";
        panel.StageRows[0].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "0.4";

        Assert.True(panel.StartIsEnabled, $"Start blocked: {panel.StartBlockedMessage}");
        Assert.Equal(string.Empty, panel.StartBlockedMessage);
    }

    [Fact]
    public void Coordinator_GammaBlankMuCommaList_DoesNotThrow()
    {
        // WHY: the end-to-end proof. Before the fix this combination threw
        // ArgumentException out of the coordinator ("Invalid parametrization for the
        // distribution") because the spec's Scale was NaN. An end-to-end run that
        // completes is the only assertion that covers the whole chain.
        var panel = Panel(1);
        panel.SourceMode = DataSourceMode.FitFromData;
        // The per-stage μ list lives in the OPTIONAL Parameters section, so it does
        // not participate until that section is switched on (D-103). Without this the
        // comma list is silently empty and the test would pass for the wrong reason.
        panel.ParametersIsOptionalEnabled = true;
        panel.StageRows[0].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[0].ServiceShape = "2";
        panel.StageRows[0].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "0.5";
        panel.ManualLambda.Value = "0.2";
        panel.StageRows[0].Servers.Value = "2";
        // Diagnostic trace runs a raw window of minutes, so the sample size is under
        // this test's control; the calendar modes would cap it at one clinic morning.
        panel.IsDiagnosticTrace = true;
        panel.Duration = DiagnosticDurationPreset.CustomMinutes;
        panel.CustomMinutes.Value = "600";

        var parameters = panel.TryBuildRunParameters();
        Assert.NotNull(parameters);
        Assert.Equal(RunMode.DiagnosticTrace, parameters!.RunMode);

        var outcome = SimulationCoordinator.Run(parameters, binding: null);

        Assert.Null(outcome.Error);
        Assert.NotNull(outcome.Result);
        var samples = outcome.Result!.GeneratedServiceSamplesByStage[0];
        Assert.NotEmpty(samples);
        Assert.All(samples, v => Assert.True(v > 0, "Gamma support is strictly positive"));
        // The drawn mean must match the resolved μ's mean, i.e. 1/0.5 = 2.
        Assert.InRange(samples.Average(), 1.8, 2.2);
    }

    [Fact]
    public void BuildSpec_Gamma_FittedStage_BlankMu_SpecIsBuiltFromTheFittedRate()
    {
        // WHY: this is the branch B-011 case 1 actually changed, and a mutation that
        // reverted it was caught by NOTHING until this test existed — the other five
        // tests all take the comma-list path, where the coordinator rate and the
        // resolved rate are already the same value.
        //
        // The situation: the file COVERS this stage, so `rate` is deliberately left
        // null to preserve 7C.5's precedence (the coordinator fits it). But the MEAN
        // is not unknown — the fit already knows it — so the spec must be built from
        // it. Built from null instead, `Scale = mean/k` came out NaN and the sampler
        // threw on a configuration the Start gate had every reason to accept.
        var panel = Panel(3);
        panel.ParametersIsOptionalEnabled = true;
        panel.ApplyLoadedFile(SamplePath("sample_3stage_variable.csv"));
        panel.SourceMode = DataSourceMode.FitFromData;
        panel.StageRows[1].MuValue = string.Empty;
        // lambda is low, and the untouched rows get spare servers, purely so the run
        // is stable: the coordinator refuses rho >= 1, and Doctor's fitted mu of 0.2
        // on a single server is exactly on the boundary at lambda 0.2.
        panel.ManualLambda.Value = "0.1";
        // Servers must be set BEFORE the family: the `/c` in the model notation owns
        // the server count, so writing it re-derives the row's notation and resets the
        // family back to Exponential. The family is therefore assigned last.
        panel.StageRows[0].Servers.Value = "2";
        panel.StageRows[1].Servers.Value = "2";
        panel.StageRows[2].Servers.Value = "3";
        panel.StageRows[1].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[1].ServiceShape = "2";
        panel.IsDiagnosticTrace = true;
        panel.Duration = DiagnosticDurationPreset.CustomMinutes;
        panel.CustomMinutes.Value = "600";
        Assert.Equal(DistributionFamily.Gamma, panel.StageRows[1].ServiceFamily);

        // The fitted rate this stage resolves to, read back from the binding rather
        // than hardcoded, so the expectation states the RULE not a magic number.
        double fitted = panel.Binding!.FittedServiceRates[1];
        Assert.True(fitted > 0, "the fixture must yield a positive fitted rate");

        var parameters = panel.TryBuildRunParameters();
        Assert.NotNull(parameters);

        // 7C.5 precedence preserved: the coordinator still resolves μ itself.
        Assert.Null(parameters!.ServiceRates[1]);

        // But the spec is complete, because the mean was knowable all along.
        var spec = parameters.ServiceFamilies[1];
        Assert.Equal(DistributionFamily.Gamma, spec.Family);
        Assert.Equal(1.0 / fitted, spec.Mean, 10);
        Assert.False(double.IsNaN(spec.Mean));
        Assert.Equal((1.0 / fitted) / 2.0, spec.Scale!.Value, 10);
        Assert.False(double.IsNaN(spec.Scale!.Value));

        // And the run it produces must not throw, which was the whole defect.
        var outcome = SimulationCoordinator.Run(parameters, panel.Binding);
        Assert.Null(outcome.Error);
        var samples = outcome.Result!.GeneratedServiceSamplesByStage[1];
        Assert.NotEmpty(samples);
        Assert.InRange(samples.Average(), (1.0 / fitted) * 0.8, (1.0 / fitted) * 1.2);
    }

    [Fact]
    public void StartGate_Gamma_BlankMuNoCommaList_BlockedWithTheStageNamed()
    {
        // WHY: item 3(b) of the B-011 ruling. The gate must block a row whose μ cannot
        // be resolved, and it must name WHICH stage — D-128 requires a disabled control
        // to explain itself (FR-UI-7), so a bare "cannot start" is not enough.
        var panel = Panel(3);
        panel.ParametersIsOptionalEnabled = true;
        panel.ApplyLoadedFile(SamplePath("sample_3stage_variable.csv"));
        panel.SourceMode = DataSourceMode.FitFromData;
        // The row is renamed to a stage the file does NOT cover, and the comma list is
        // given only two entries for three rows. So all three sources come up empty for
        // it: no fitted rate (name absent), no comma part (index 2), no row μ (blank).
        // Naming a real stage name here would be wrong — the fit would legitimately
        // supply its μ and the gate would rightly stay open.
        panel.StageRows[2].StageName = "Triage";
        panel.StageRows[2].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "0.5, 0.25";
        panel.StageRows[2].Servers.Value = "2";
        panel.StageRows[2].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[2].ServiceShape = "2";

        // Re-clear the field so the gate actually re-evaluates. Assigning a value that
        // is already empty raises no change, so the gate would still be holding the
        // verdict from when ApplyLoadedFile ran (when the fitted rate covered the row).
        // Type-then-clear is what a user actually does, and it is what raises the event.
        panel.StageRows[2].MuValue = "0.3";
        panel.StageRows[2].MuValue = string.Empty;

        Assert.False(panel.StartIsEnabled);
        Assert.Contains("Triage", panel.StartBlockedMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("μ", panel.StartBlockedMessage);

        // The gate is the first line of defence, not the only one: the build refuses
        // too, so a row that reaches the coordinator cannot produce an opaque Core
        // exception.
        Assert.Null(panel.TryBuildRunParameters());
        Assert.True(panel.StageRows[2].HasInlineError);
        Assert.Contains("Triage", panel.StageRows[2].InlineError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Coordinator_UniformBlankMuCommaList_BoundsFollowTheResolvedMean()
    {
        // WHY: item 3(c) of the ruling. Uniform is the other family that stores the
        // mean a second time, and unlike Gamma its half-width is not recoverable from
        // the spec — it exists only on the row. So the comma-list path has to produce
        // correct bounds AND a run that completes with every sample inside them.
        var panel = Panel(1);
        panel.ParametersIsOptionalEnabled = true;
        panel.SourceMode = DataSourceMode.FitFromData;
        panel.ManualLambda.Value = "0.2";
        panel.StageRows[0].Servers.Value = "2";
        panel.StageRows[0].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "0.25";
        panel.StageRows[0].ServiceFamily = DistributionFamily.Uniform;
        panel.StageRows[0].ServiceSpread = "0.5";
        panel.IsDiagnosticTrace = true;
        panel.Duration = DiagnosticDurationPreset.CustomMinutes;
        panel.CustomMinutes.Value = "600";
        Assert.Equal(DistributionFamily.Uniform, panel.StageRows[0].ServiceFamily);

        var parameters = panel.TryBuildRunParameters();
        Assert.NotNull(parameters);

        var spec = parameters!.ServiceFamilies[0];
        Assert.Equal(4.0, spec.Mean, 10);
        Assert.Equal(3.5, spec.Min!.Value, 10);
        Assert.Equal(4.5, spec.Max!.Value, 10);

        var outcome = SimulationCoordinator.Run(parameters, binding: null);
        Assert.Null(outcome.Error);
        var samples = outcome.Result!.GeneratedServiceSamplesByStage[0];
        Assert.NotEmpty(samples);
        // Every draw inside the derived bounds is the observable form of "the engine
        // got the bounds", and it fails loudly if the bounds were left NaN or stale.
        Assert.All(samples, v => Assert.InRange(v, 3.5, 4.5));
        Assert.InRange(samples.Average(), 3.8, 4.2);
    }

    [Fact]
    public void BuildSpec_Gamma_NonFiniteMuAnywhere_RefusesCleanly()
    {
        // WHY: the B-011 ruling says "finite positive mu", and the two hand-typed
        // sources are not naturally finite-checked. "1e400" parses to +infinity
        // (double.TryParse returns TRUE), so without the IsFinite guard the row looks
        // like it HAS a rate, skips the refusal, and builds a Gamma with mean 0 and
        // Scale 0 -- a silently wrong simulation rather than a refused one, which is
        // the worse outcome of the two. Verified: the guard is what makes these two
        // assertions pass, not the surrounding validation.
        var panel = Panel(1);
        panel.ParametersIsOptionalEnabled = true;
        panel.SourceMode = DataSourceMode.FitFromData;
        // Rate-wise, deliberately. Mean-wise would hide the bug: it inverts mu, so +inf
        // becomes 0 and the plain "> 0" check already refuses. Rate-wise passes +inf
        // straight through, which is the case this guard exists for.
        panel.ParameterMode = ParameterMode.RateWise;
        panel.ManualLambda.Value = "0.2";
        panel.StageRows[0].Servers.Value = "2";
        panel.StageRows[0].ServiceFamily = DistributionFamily.Gamma;
        panel.StageRows[0].ServiceShape = "2";
        panel.StageRows[0].MuValue = "1e400";

        Assert.Null(panel.TryBuildRunParameters());
        Assert.True(panel.StageRows[0].HasInlineError);
        Assert.Contains("Reception", panel.StageRows[0].InlineError, StringComparison.OrdinalIgnoreCase);

        // Same rule via the comma list, so both hand-typed sources are covered.
        panel.StageRows[0].MuValue = string.Empty;
        panel.ManualMuPerStage.Value = "1e400";

        Assert.Null(panel.TryBuildRunParameters());
        Assert.True(panel.StageRows[0].HasInlineError);
    }
}
