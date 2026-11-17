namespace OpdSimulator.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;

/// <summary>
/// Phase 9Q (FR-UI-44): the single-server Theory tab — an answer key for textbook
/// M/M/1, M/G/1 and G/G/1 questions with the substitutions shown next to every
/// number. All arithmetic is delegated to
/// <see cref="TheoryWorkedSolutionBuilder"/>, which delegates in turn to Core.
/// </summary>
/// <remarks>
/// <para>
/// The four numeric fields follow FR-UI-17: they validate on blur (<c>FieldLostFocus</c>
/// in the view's code-behind), not on every keystroke. Dropdown and unit changes are
/// not numeric, so they recompute immediately. On a unit or entry-mode change the
/// displayed numbers are rewritten to preserve the same physical λ and μ — otherwise
/// switching "minutes" to "hours" would silently multiply the arrival rate by 60.
/// </para>
/// <para>
/// One time unit is shared by both rates and both spreads, matching FR-UI-41's
/// single-axis rule; conversion delegates to <see cref="FittingTimeUnits.MinutesPerUnit"/>.
/// </para>
/// </remarks>
public partial class TheoryTabViewModel : ObservableObject
{
    private bool _lambdaValid;
    private bool _muValid;
    private bool _arrivalVariabilityValid = true;
    private bool _serviceVariabilityValid = true;
    private double _lambdaPerMinute;
    private double _muPerMinute;
    private double _arrivalCv2 = 1.0;
    private double _serviceCv2 = 1.0;

    /// <summary>Creates the tab and computes a valid default answer.</summary>
    public TheoryTabViewModel()
    {
        ValidateAll();
    }

    /// <summary>The three single-server models offered by this tab.</summary>
    public IReadOnlyList<string> ModelOptions { get; } = new[] { "M/M/1", "M/G/1", "G/G/1" };

    /// <summary>The two parameter interpretations, sharing the app's <see cref="ParameterMode"/>.</summary>
    public IReadOnlyList<string> ParameterModeOptions { get; } = new[] { "Rate-wise", "Mean-wise" };

    /// <summary>The five equivalent ways to enter a process spread (D-247).</summary>
    public IReadOnlyList<string> VariabilityOptions { get; } = new[]
    {
        "CV", "Variance", "Second moment", "Standard deviation (σ)", "Squared CV (C²)",
    };

    /// <summary>The selected model label ("M/M/1", "M/G/1" or "G/G/1").</summary>
    [ObservableProperty]
    private string selectedModel = "M/M/1";

    /// <summary>The selected interpretation label ("Rate-wise" or "Mean-wise").</summary>
    [ObservableProperty]
    private string selectedParameterMode = "Rate-wise";

    /// <summary>The selected spread entry form (CV, variance, second moment, σ or squared CV).</summary>
    [ObservableProperty]
    private string selectedVariabilityMode = "CV";

    /// <summary>The one time unit governing every entered value.</summary>
    [ObservableProperty]
    private TimeUnit selectedUnit = TimeUnit.Minutes;

    /// <summary>The entered arrival rate λ or mean inter-arrival time 1/λ.</summary>
    [ObservableProperty]
    private string lambdaInput = "0.5";

    /// <summary>The entered service rate μ or mean service time 1/μ.</summary>
    [ObservableProperty]
    private string muInput = "0.8";

    /// <summary>The entered inter-arrival spread (G/G/1 only).</summary>
    [ObservableProperty]
    private string arrivalVariabilityInput = "1";

    /// <summary>The entered service spread (M/G/1 and G/G/1).</summary>
    [ObservableProperty]
    private string serviceVariabilityInput = "1";

    /// <summary>The worked answer, or null when the inputs are invalid or unstable.</summary>
    [ObservableProperty]
    private TheorySolution? solution;

    /// <summary>The cause of a refused answer, shown in an inline banner.</summary>
    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Whether the arrival-rate field is currently invalid.</summary>
    [ObservableProperty]
    private bool lambdaHasError;

    /// <summary>Whether the service-rate field is currently invalid.</summary>
    [ObservableProperty]
    private bool muHasError;

    /// <summary>Whether the arrival-spread field is currently invalid.</summary>
    [ObservableProperty]
    private bool arrivalVariabilityHasError;

    /// <summary>Whether the service-spread field is currently invalid.</summary>
    [ObservableProperty]
    private bool serviceVariabilityHasError;

    /// <summary>Actionable error text for the arrival-rate field.</summary>
    [ObservableProperty]
    private string? lambdaError;

    /// <summary>Actionable error text for the service-rate field.</summary>
    [ObservableProperty]
    private string? muError;

    /// <summary>Actionable error text for the arrival-spread field.</summary>
    [ObservableProperty]
    private string? arrivalVariabilityError;

    /// <summary>Actionable error text for the service-spread field.</summary>
    [ObservableProperty]
    private string? serviceVariabilityError;

    /// <summary>The model behind the current answer.</summary>
    public TheoryModel CurrentModel => SelectedModel switch
    {
        "M/G/1" => TheoryModel.Mg1,
        "G/G/1" => TheoryModel.Gg1,
        _ => TheoryModel.Mm1,
    };

    /// <summary>True when the fields hold rates rather than means.</summary>
    public bool IsRateWise => SelectedParameterMode == "Rate-wise";

    /// <summary>The currently selected spread entry form.</summary>
    public VariabilityMode CurrentVariabilityMode => SelectedVariabilityMode switch
    {
        "Variance" => VariabilityMode.Variance,
        "Second moment" => VariabilityMode.SecondMoment,
        "Standard deviation (σ)" => VariabilityMode.StandardDeviation,
        "Squared CV (C²)" => VariabilityMode.SquaredCv,
        _ => VariabilityMode.Cv,
    };

    /// <summary>True only for G/G/1, which is the only model with general arrivals.</summary>
    public bool IsArrivalVariabilityVisible => CurrentModel == TheoryModel.Gg1;

    /// <summary>False for M/M/1, whose exponential service fixes Cs² = 1.</summary>
    public bool IsServiceVariabilityVisible => CurrentModel != TheoryModel.Mm1;

    /// <summary>Explains which spreads the selected model actually uses.</summary>
    public string VariabilityCaption => CurrentModel switch
    {
        TheoryModel.Mm1 =>
            "M/M/1 assumes exponential arrivals and service: Ca² = Cs² = 1, so spread entry does not apply.",
        TheoryModel.Mg1 =>
            "M/G/1 assumes Poisson arrivals (Ca² = 1). Only the service spread is used.",
        _ =>
            "G/G/1 uses both entered spreads. Enter the same spread in any form (CV, variance, second moment, σ or squared CV) — the answer is identical.",
    };

    /// <summary>
    /// Explains why two models give the same answer when the spread is exponential
    /// (D-247). Correct maths otherwise reads as a bug: with Cs² = 1,
    /// Pollaczek–Khinchine reduces exactly to Erlang-C.
    /// </summary>
    public string SpreadHint => CurrentModel switch
    {
        TheoryModel.Mm1 => string.Empty,
        TheoryModel.Mg1 when Math.Abs(_serviceCv2 - 1.0) < 1e-9 =>
            "Cs² = 1 means exponential service: M/G/1 gives exactly the M/M/1 answer. "
            + "Change the service spread to see a difference.",
        TheoryModel.Gg1 when Math.Abs(_arrivalCv2 - 1.0) < 1e-9
                          && Math.Abs(_serviceCv2 - 1.0) < 1e-9 =>
            "Ca² = Cs² = 1 means Poisson arrivals and exponential service, so all three "
            + "models coincide. Change a spread to see a difference.",
        _ => string.Empty,
    };

    /// <summary>Whether the exponential-spread explanation should be shown.</summary>
    public bool HasSpreadHint => SpreadHint.Length > 0;

    /// <summary>The compact one-line verdict above the worked rows.</summary>
    public string Headline => Solution is null
        ? "No steady-state answer — see the message below."
        : $"{SelectedModel} · ρ = {N(Solution.Result.Rho)} < 1 · stable · "
          + $"Wq = {N(Solution.Result.Wq)} min · Lq = {N(Solution.Result.Lq)} customers";

    /// <summary>True when an answer key is available.</summary>
    public bool HasSolution => Solution is not null;

    /// <summary>The worked rows for the answer key (empty until a valid answer exists).</summary>
    public IReadOnlyList<TheoryWorkedRow> WorkedRows => Solution?.Rows ?? Array.Empty<TheoryWorkedRow>();

    /// <summary>The expandable derivation for the selected model.</summary>
    public string FormulaSheetText => Solution?.FormulaSheetText ?? string.Empty;

    /// <summary>The G/G/1 approximation notice; empty for the exact models.</summary>
    public string ApproximationNotice => Solution?.ApproximationNotice ?? string.Empty;

    /// <summary>Why P(wait) is reported for the Poisson models and omitted for G/G/1.</summary>
    public string PWaitNote => Solution?.PWaitNote ?? string.Empty;

    /// <summary>The expandable sheet's title, naming the model it describes.</summary>
    public string FormulaSheetTitle => $"Formula sheet — {SelectedModel}";

    /// <summary>Selects a model by enum value (used by tests and keyboard shortcuts).</summary>
    /// <param name="model">The single-server model to select.</param>
    public void SetModel(TheoryModel model) => SelectedModel = model switch
    {
        TheoryModel.Mm1 => "M/M/1",
        TheoryModel.Mg1 => "M/G/1",
        TheoryModel.Gg1 => "G/G/1",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
    };

    /// <summary>Selects the time unit by enum value.</summary>
    /// <param name="unit">The unit to select.</param>
    public void SetUnit(TimeUnit unit) => SelectedUnit = unit;

    /// <summary>Selects the parameter interpretation by enum value.</summary>
    /// <param name="mode">Rate-wise or mean-wise.</param>
    public void SetMode(ParameterMode mode) => SelectedParameterMode = mode == ParameterMode.RateWise
        ? "Rate-wise"
        : "Mean-wise";

    /// <summary>Selects the spread entry form by enum value.</summary>
    /// <param name="mode">CV, variance, second moment, standard deviation or squared CV.</param>
    public void SetVariabilityMode(VariabilityMode mode) => SelectedVariabilityMode = mode switch
    {
        VariabilityMode.Cv => "CV",
        VariabilityMode.Variance => "Variance",
        VariabilityMode.SecondMoment => "Second moment",
        VariabilityMode.StandardDeviation => "Standard deviation (σ)",
        VariabilityMode.SquaredCv => "Squared CV (C²)",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown variability mode."),
    };

    /// <summary>Validates one named field on blur, then refreshes the answer.</summary>
    /// <param name="key"><c>lambda</c>, <c>mu</c>, <c>arrival</c> or <c>service</c>.</param>
    public void ValidateField(string key)
    {
        switch (key)
        {
            case "lambda":
                _lambdaValid = TryParseRate(LambdaInput, "Arrival rate λ", out _lambdaPerMinute, out var lambdaError);
                LambdaHasError = !_lambdaValid && lambdaError is not null;
                LambdaError = lambdaError;
                break;

            case "mu":
                _muValid = TryParseRate(MuInput, "Service rate μ", out _muPerMinute, out var muError);
                MuHasError = !_muValid && muError is not null;
                MuError = muError;
                break;

            case "arrival":
                _arrivalVariabilityValid = TryParseVariability(
                    ArrivalVariabilityInput, "Inter-arrival spread", ArrivalMeanUnit(), out _arrivalCv2,
                    out var arrivalError);
                ArrivalVariabilityHasError = !_arrivalVariabilityValid && arrivalError is not null;
                ArrivalVariabilityError = arrivalError;
                break;

            case "service":
                _serviceVariabilityValid = TryParseVariability(
                    ServiceVariabilityInput, "Service spread", ServiceMeanUnit(), out _serviceCv2,
                    out var serviceError);
                ServiceVariabilityHasError = !_serviceVariabilityValid && serviceError is not null;
                ServiceVariabilityError = serviceError;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown theory field.");
        }

        Recompute();
    }

    private void ValidateAll()
    {
        ValidateField("lambda");
        ValidateField("mu");
        ValidateField("arrival");
        ValidateField("service");
    }

    partial void OnLambdaInputChanged(string value) => ValidateField("lambda");

    partial void OnMuInputChanged(string value) => ValidateField("mu");

    partial void OnArrivalVariabilityInputChanged(string value) => ValidateField("arrival");

    partial void OnServiceVariabilityInputChanged(string value) => ValidateField("service");

    partial void OnSelectedModelChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentModel));
        OnPropertyChanged(nameof(IsArrivalVariabilityVisible));
        OnPropertyChanged(nameof(IsServiceVariabilityVisible));
        OnPropertyChanged(nameof(VariabilityCaption));
        OnPropertyChanged(nameof(FormulaSheetTitle));
        Recompute();
    }

    partial void OnSelectedParameterModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsRateWise));
        RewriteInputs();
        Recompute();
    }

    partial void OnSelectedVariabilityModeChanged(string value)
    {
        OnPropertyChanged(nameof(CurrentVariabilityMode));
        RewriteInputs();
        Recompute();
    }

    partial void OnSelectedUnitChanged(TimeUnit value)
    {
        RewriteInputs();
        Recompute();
    }

    partial void OnSolutionChanged(TheorySolution? value)
    {
        OnPropertyChanged(nameof(HasSolution));
        OnPropertyChanged(nameof(Headline));
        OnPropertyChanged(nameof(WorkedRows));
        OnPropertyChanged(nameof(FormulaSheetText));
        OnPropertyChanged(nameof(ApproximationNotice));
        OnPropertyChanged(nameof(PWaitNote));
    }

    private void Recompute()
    {
        // D-247: the hint depends on the resolved spreads, which are updated by
        // ValidateField immediately before this runs.
        OnPropertyChanged(nameof(SpreadHint));
        OnPropertyChanged(nameof(HasSpreadHint));

        bool needArrival = IsArrivalVariabilityVisible;
        bool needService = IsServiceVariabilityVisible;

        if (!_lambdaValid || !_muValid || (needArrival && !_arrivalVariabilityValid)
            || (needService && !_serviceVariabilityValid))
        {
            Solution = null;
            ErrorMessage = FirstCause(needArrival, needService);
            return;
        }

        double ca2 = needArrival ? _arrivalCv2 : 1.0;
        double cs2 = needService ? _serviceCv2 : 1.0;

        if (TheoryWorkedSolutionBuilder.TryBuild(
                CurrentModel, _lambdaPerMinute, _muPerMinute, ca2, cs2,
                out var answer, out var refusal))
        {
            Solution = answer;
            ErrorMessage = null;
        }
        else
        {
            Solution = null;
            ErrorMessage = refusal;
        }
    }

    private string FirstCause(bool needArrival, bool needService)
    {
        if (!_lambdaValid && LambdaError is not null)
        {
            return LambdaError;
        }

        if (!_muValid && MuError is not null)
        {
            return MuError;
        }

        if (needArrival && !_arrivalVariabilityValid && ArrivalVariabilityError is not null)
        {
            return ArrivalVariabilityError;
        }

        if (needService && !_serviceVariabilityValid && ServiceVariabilityError is not null)
        {
            return ServiceVariabilityError;
        }

        return "Enter a positive arrival rate and service rate to see the answer.";
    }

    /// <summary>
    /// Rewrites the four displayed numbers for a new unit, entry mode or spread form
    /// while keeping the underlying λ, μ and CV² unchanged.
    /// </summary>
    private void RewriteInputs()
    {
        if (!(_lambdaValid && _muValid && _arrivalVariabilityValid && _serviceVariabilityValid))
        {
            return;
        }

        double minutes = MinutesPerUnit(SelectedUnit);
        double lambdaPerUnit = _lambdaPerMinute * minutes;
        double muPerUnit = _muPerMinute * minutes;

        LambdaInput = RoundTrip(IsRateWise ? lambdaPerUnit : 1.0 / lambdaPerUnit);
        MuInput = RoundTrip(IsRateWise ? muPerUnit : 1.0 / muPerUnit);

        if (IsArrivalVariabilityVisible)
        {
            ArrivalVariabilityInput = TheoryWorkedSolutionBuilder.VariabilityText(
                _arrivalCv2, 1.0 / lambdaPerUnit, CurrentVariabilityMode);
        }

        if (IsServiceVariabilityVisible)
        {
            ServiceVariabilityInput = TheoryWorkedSolutionBuilder.VariabilityText(
                _serviceCv2, 1.0 / muPerUnit, CurrentVariabilityMode);
        }
    }

    private double ArrivalMeanUnit()
        => _lambdaValid ? 1.0 / (_lambdaPerMinute * MinutesPerUnit(SelectedUnit)) : double.NaN;

    private double ServiceMeanUnit()
        => _muValid ? 1.0 / (_muPerMinute * MinutesPerUnit(SelectedUnit)) : double.NaN;

    private bool TryParseRate(string? raw, string label, out double perMinute, out string? error)
    {
        perMinute = double.NaN;
        error = null;

        if (!TryNumber(raw, label, out double value, out error))
        {
            return false;
        }

        double minutes = MinutesPerUnit(SelectedUnit);
        perMinute = IsRateWise ? value / minutes : 1.0 / (value * minutes);
        return true;
    }

    private bool TryParseVariability(
        string? raw, string label, double meanUnit, out double cv2, out string? error)
    {
        cv2 = double.NaN;

        if (double.IsNaN(meanUnit))
        {
            // The rate field owns this error; do not report the same cause twice.
            error = null;
            return false;
        }

        if (!TryNumber(raw, label, out double value, out error, allowZero: true))
        {
            return false;
        }

        double meanSquared = meanUnit * meanUnit;
        switch (CurrentVariabilityMode)
        {
            case VariabilityMode.Cv:
                cv2 = value * value;
                return true;

            case VariabilityMode.Variance:
                cv2 = value / meanSquared;
                return true;

            case VariabilityMode.SecondMoment:
                if (value < meanSquared)
                {
                    error = $"{label} cannot be below the mean squared ({N(meanSquared)}). "
                          + "A second moment must be at least E[X]²; enter a larger value.";
                    return false;
                }

                cv2 = value / meanSquared - 1.0;
                return true;

            case VariabilityMode.StandardDeviation:
                cv2 = (value / meanUnit) * (value / meanUnit);
                return true;

            case VariabilityMode.SquaredCv:
                cv2 = value;
                return true;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(CurrentVariabilityMode), CurrentVariabilityMode, "Unknown variability mode.");
        }
    }

    private static bool TryNumber(
        string? raw, string label, out double value, out string? error, bool allowZero = false)
    {
        value = double.NaN;
        error = null;

        if (string.IsNullOrWhiteSpace(raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || !double.IsFinite(value)
            || value < 0 || (!allowZero && value == 0))
        {
            value = double.NaN;
            string expected = allowZero ? "finite non-negative number" : "finite positive number";
            error = $"{label} must be a {expected}. You entered '{raw ?? ""}'.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Minutes in one selected unit, without depending on the configuration
    /// panel's unit helper (9Q, D-246 amendment). The Theory tab owns its own
    /// selector and must build independently of the uncommitted 9P refactor, so
    /// it converts by NAME — the enum has three members before 9P and five
    /// after — and delegates the actual mapping to <see cref="FittingTimeUnits"/>,
    /// which already owns every minute constant in the project.
    /// </summary>
    /// <param name="unit">The unit chosen on this tab.</param>
    /// <returns>Minutes represented by one <paramref name="unit"/>.</returns>
    private static double MinutesPerUnit(TimeUnit unit) =>
        FittingTimeUnits.MinutesPerUnit(
            Enum.Parse<FittingTimeUnit>(unit.ToString(), ignoreCase: true));

    /// <summary>Text written back into a field must keep enough digits to round-trip.</summary>
    private static string RoundTrip(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
