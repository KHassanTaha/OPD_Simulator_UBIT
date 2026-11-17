namespace OpdSimulator.App.ViewModels;

using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Data.Fitting;
using OpdSimulator.Data.Parameters;

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

    public TheoryTabViewModel()
    {
        ValidateAll();
    }

    public IReadOnlyList<string> ModelOptions { get; } = new[] { "M/M/1", "M/G/1", "G/G/1" };

    public IReadOnlyList<string> ParameterModeOptions { get; } = new[] { "Rate-wise", "Mean-wise" };

    public IReadOnlyList<string> VariabilityOptions { get; } = new[]
    {
        "CV", "Variance", "Second moment", "Standard deviation (σ)", "Squared CV (C²)",
    };

    [ObservableProperty]
    private string selectedModel = "M/M/1";

    [ObservableProperty]
    private string selectedParameterMode = "Rate-wise";

    [ObservableProperty]
    private string selectedVariabilityMode = "CV";

    [ObservableProperty]
    private TimeUnit selectedUnit = TimeUnit.Minutes;

    [ObservableProperty]
    private string lambdaInput = "0.5";

    [ObservableProperty]
    private string muInput = "0.8";

    [ObservableProperty]
    private string arrivalVariabilityInput = "1";

    [ObservableProperty]
    private string serviceVariabilityInput = "1";

    [ObservableProperty]
    private TheorySolution? solution;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool lambdaHasError;

    [ObservableProperty]
    private bool muHasError;

    [ObservableProperty]
    private bool arrivalVariabilityHasError;

    [ObservableProperty]
    private bool serviceVariabilityHasError;

    [ObservableProperty]
    private string? lambdaError;

    [ObservableProperty]
    private string? muError;

    [ObservableProperty]
    private string? arrivalVariabilityError;

    [ObservableProperty]
    private string? serviceVariabilityError;

    public TheoryModel CurrentModel => SelectedModel switch
    {
        "M/G/1" => TheoryModel.Mg1,
        "G/G/1" => TheoryModel.Gg1,
        _ => TheoryModel.Mm1,
    };

    public bool IsRateWise => SelectedParameterMode == "Rate-wise";

    public VariabilityMode CurrentVariabilityMode => SelectedVariabilityMode switch
    {
        "Variance" => VariabilityMode.Variance,
        "Second moment" => VariabilityMode.SecondMoment,
        "Standard deviation (σ)" => VariabilityMode.StandardDeviation,
        "Squared CV (C²)" => VariabilityMode.SquaredCv,
        _ => VariabilityMode.Cv,
    };

    public bool IsArrivalVariabilityVisible => CurrentModel == TheoryModel.Gg1;

    public bool IsServiceVariabilityVisible => CurrentModel != TheoryModel.Mm1;

    public string VariabilityCaption => CurrentModel switch
    {
        TheoryModel.Mm1 =>
            "M/M/1 assumes exponential arrivals and service: Ca² = Cs² = 1, so spread entry does not apply.",
        TheoryModel.Mg1 =>
            "M/G/1 assumes Poisson arrivals (Ca² = 1). Only the service spread is used.",
        _ =>
            "G/G/1 uses both entered spreads. Enter the same spread in any form (CV, variance, second moment, σ or squared CV) — the answer is identical.",
    };

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

    public bool HasSpreadHint => SpreadHint.Length > 0;

    public string Headline => Solution is null
        ? "No steady-state answer — see the message below."
        : $"{SelectedModel} · ρ = {N(Solution.Result.Rho)} < 1 · stable · "
          + $"Wq = {N(Solution.Result.Wq)} min · Lq = {N(Solution.Result.Lq)} customers";

    public bool HasSolution => Solution is not null;

    public IReadOnlyList<TheoryWorkedRow> WorkedRows => Solution?.Rows ?? Array.Empty<TheoryWorkedRow>();

    public string FormulaSheetText => Solution?.FormulaSheetText ?? string.Empty;

    public string ApproximationNotice => Solution?.ApproximationNotice ?? string.Empty;

    public string PWaitNote => Solution?.PWaitNote ?? string.Empty;

    public string FormulaSheetTitle => $"Formula sheet — {SelectedModel}";

    public void SetModel(TheoryModel model) => SelectedModel = model switch
    {
        TheoryModel.Mm1 => "M/M/1",
        TheoryModel.Mg1 => "M/G/1",
        TheoryModel.Gg1 => "G/G/1",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
    };

    public void SetUnit(TimeUnit unit) => SelectedUnit = unit;

    public void SetMode(ParameterMode mode) => SelectedParameterMode = mode == ParameterMode.RateWise
        ? "Rate-wise"
        : "Mean-wise";

    public void SetVariabilityMode(VariabilityMode mode) => SelectedVariabilityMode = mode switch
    {
        VariabilityMode.Cv => "CV",
        VariabilityMode.Variance => "Variance",
        VariabilityMode.SecondMoment => "Second moment",
        VariabilityMode.StandardDeviation => "Standard deviation (σ)",
        VariabilityMode.SquaredCv => "Squared CV (C²)",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown variability mode."),
    };

    public void ValidateField(string key)
    {
        ReparseField(key, out var error);

        switch (key)
        {
            case "lambda":
                LambdaHasError = !_lambdaValid && error is not null;
                LambdaError = error;
                break;

            case "mu":
                MuHasError = !_muValid && error is not null;
                MuError = error;
                break;

            case "arrival":
                ArrivalVariabilityHasError = !_arrivalVariabilityValid && error is not null;
                ArrivalVariabilityError = error;
                break;

            case "service":
                ServiceVariabilityHasError = !_serviceVariabilityValid && error is not null;
                ServiceVariabilityError = error;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown theory field.");
        }

        Recompute();
    }

    private void ClearErrorIfNowValid(string key)
    {
        switch (key)
        {
            case "lambda" when _lambdaValid:
                LambdaHasError = false;
                LambdaError = null;
                break;

            case "mu" when _muValid:
                MuHasError = false;
                MuError = null;
                break;

            case "arrival" when _arrivalVariabilityValid:
                ArrivalVariabilityHasError = false;
                ArrivalVariabilityError = null;
                break;

            case "service" when _serviceVariabilityValid:
                ServiceVariabilityHasError = false;
                ServiceVariabilityError = null;
                break;
        }
    }

    private void ReparseField(string key, out string? error)
    {
        error = null;
        switch (key)
        {
            case "lambda":
                _lambdaValid = TryParseRate(LambdaInput, "Arrival rate λ", out _lambdaPerMinute, out error);
                break;

            case "mu":
                _muValid = TryParseRate(MuInput, "Service rate μ", out _muPerMinute, out error);
                break;

            case "arrival":
                _arrivalVariabilityValid = TryParseVariability(
                    ArrivalVariabilityInput, "Inter-arrival spread", ArrivalMeanUnit(), out _arrivalCv2,
                    out error);
                break;

            case "service":
                _serviceVariabilityValid = TryParseVariability(
                    ServiceVariabilityInput, "Service spread", ServiceMeanUnit(), out _serviceCv2,
                    out error);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown theory field.");
        }
    }

    private void ValidateAll()
    {
        ValidateField("lambda");
        ValidateField("mu");
        ValidateField("arrival");
        ValidateField("service");
    }

    partial void OnLambdaInputChanged(string value)
    {
        ReparseField("lambda", out _);
        ClearErrorIfNowValid("lambda");
        Recompute();
    }

    partial void OnMuInputChanged(string value)
    {
        ReparseField("mu", out _);
        ClearErrorIfNowValid("mu");
        Recompute();
    }

    partial void OnArrivalVariabilityInputChanged(string value)
    {
        ReparseField("arrival", out _);
        ClearErrorIfNowValid("arrival");
        Recompute();
    }

    partial void OnServiceVariabilityInputChanged(string value)
    {
        ReparseField("service", out _);
        ClearErrorIfNowValid("service");
        Recompute();
    }

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

    private static double MinutesPerUnit(TimeUnit unit) =>
        FittingTimeUnits.MinutesPerUnit(
            Enum.Parse<FittingTimeUnit>(unit.ToString(), ignoreCase: true));

    private static string RoundTrip(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
