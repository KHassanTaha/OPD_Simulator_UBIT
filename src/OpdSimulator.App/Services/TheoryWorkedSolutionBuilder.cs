namespace OpdSimulator.App.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using OpdSimulator.Core.Analytics;
using OpdSimulator.Data.Parameters;

public enum TheoryModel
{
    Mm1 = 0,

    Mg1 = 1,

    Gg1 = 2,
}

public enum VariabilityMode
{
    Cv = 0,

    Variance = 1,

    SecondMoment = 2,

    StandardDeviation = 3,

    SquaredCv = 4,
}

public sealed record TheoryWorkedRow(
    string Symbol,
    string Name,
    string Formula,
    string Substitution,
    string Value,
    int SerialNumber = 0);

public sealed record TheorySolution(
    TheoryModel Model,
    AnalyticalModelResult Result,
    IReadOnlyList<TheoryWorkedRow> Rows,
    string FormulaSheetText,
    string ApproximationNotice,
    string PWaitNote);

public static class TheoryWorkedSolutionBuilder
{
    public static bool TryBuild(
        TheoryModel model,
        double lambda,
        double mu,
        double ca2,
        double cs2,
        out TheorySolution? solution,
        out string? refusal,
        ParameterMode parameterMode = ParameterMode.RateWise,
        double arrivalMean = 0,
        double serviceMean = 0)
    {
        solution = null;

        double arrivalCv2 = model == TheoryModel.Gg1 ? ca2 : 1.0;
        double serviceCv2 = model == TheoryModel.Mm1 ? 1.0 : cs2;

        AnalyticalModelFamily family = model switch
        {
            TheoryModel.Mm1 => AnalyticalModelFamily.Mmc,
            TheoryModel.Mg1 => AnalyticalModelFamily.Mgc,
            TheoryModel.Gg1 => AnalyticalModelFamily.Ggc,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
        };

        var inputs = new AnalyticalModelInputs(lambda, mu, 1, arrivalCv2, serviceCv2);
        if (!AnalyticalModels.TryCompute(family, inputs, out var result, out refusal))
        {
            return false;
        }

        AnalyticalModelResult answer = result!;
        bool inverted = parameterMode == ParameterMode.MeanWise && arrivalMean > 0 && serviceMean > 0;
        string lambdaTerm = inverted ? $"(1 / {N(arrivalMean)})" : N(lambda);
        string muTerm = inverted ? $"(1 / {N(serviceMean)})" : N(mu);

        var rows = new List<TheoryWorkedRow>
        {
            Worked("ρ", "Traffic intensity (utilisation)", "λ / μ",
                $"= {lambdaTerm} / {muTerm}\n"
                + (inverted ? $"= {N(lambda)} / {N(mu)}\n" : string.Empty)
                + $"= {N(answer.Rho)}", N(answer.Rho)),
            Worked("P₀", "Server idle probability", "1 − ρ",
                $"= 1 − {N(answer.Rho)}\n= {N(answer.P0)}", N(answer.P0)),
            Worked("Wq", "Mean queue waiting time", WaitFormula(model),
                WaitSubstitution(model, answer.Rho, mu, arrivalCv2, serviceCv2, answer.Wq, muTerm, inverted),
                $"{N(answer.Wq)} min"),
            Worked("Lq", "Mean queue length", "λ · Wq",
                $"= {lambdaTerm} × {N(answer.Wq)}\n"
                + (inverted ? $"= {N(lambda)} × {N(answer.Wq)}\n" : string.Empty)
                + $"= {N(answer.Lq)}", N(answer.Lq)),
            Worked("W", "Mean time in system", "Wq + 1 / μ",
                $"= {N(answer.Wq)} + 1 / {muTerm}\n"
                + (inverted ? $"= {N(answer.Wq)} + 1 / {N(mu)}\n" : string.Empty)
                + $"= {N(answer.W)}", $"{N(answer.W)} min"),
            Worked("L", "Mean number in system", "λ · W",
                $"= {lambdaTerm} × {N(answer.W)}\n"
                + (inverted ? $"= {N(lambda)} × {N(answer.W)}\n" : string.Empty)
                + $"= {N(answer.L)}", N(answer.L)),
            Worked("Busy", "Server busy fraction", "ρ (one server)",
                $"= {N(answer.Rho)}\n= {Pct(answer.Rho)}", Pct(answer.Rho)),
            Worked("Idle", "Server idle fraction", "1 − ρ",
                $"= {N(answer.P0)}\n= {Pct(answer.P0)}", Pct(answer.P0)),
            Worked("Wait/h", "Aggregate customer wait per hour", "60 · Lq",
                $"= 60 × {N(answer.Lq)}\n= {N(60 * answer.Lq)} min/h\n(total across all customers)",
                $"{N(60 * answer.Lq)} min/h"),
        };

        if (model != TheoryModel.Gg1)
        {
            rows.Add(Worked("P(wait)", "Probability of waiting on arrival", "ρ (PASTA)",
                $"= {N(answer.PWait)}\n= {Pct(answer.PWait)}", Pct(answer.PWait)));
        }

        string notice = model == TheoryModel.Gg1
            ? "Approximation — G/G/1 has no exact two-moment answer. This is the Allen–Cunneen / "
              + "Kingman two-moment approximation: it is exact when Ca² = 1 (Poisson arrivals, "
              + "where it reduces to Pollaczek–Khinchine) but is not exact in general. Treat the "
              + "numbers as an estimate, especially away from heavy traffic."
            : string.Empty;

        string pWaitNote = model == TheoryModel.Gg1
            ? "P(wait) is intentionally omitted. With general arrivals the probability at arrival "
              + "epochs is not determined by Ca² and Cs² alone."
            : string.Empty;

        var numbered = new List<TheoryWorkedRow>(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            numbered.Add(rows[i] with { SerialNumber = i + 1 });
        }

        solution = new TheorySolution(
            model,
            answer,
            numbered,
            FormulaSheet(model, arrivalCv2, serviceCv2),
            notice,
            pWaitNote);
        refusal = null;
        return true;
    }

    public static string VariabilityText(double cv2, double mean, VariabilityMode mode) => mode switch
    {
        VariabilityMode.Cv => RoundTrip(Math.Sqrt(cv2)),
        VariabilityMode.Variance => RoundTrip(cv2 * mean * mean),
        VariabilityMode.SecondMoment => RoundTrip((1.0 + cv2) * mean * mean),
        VariabilityMode.StandardDeviation => RoundTrip(Math.Sqrt(cv2) * mean),
        VariabilityMode.SquaredCv => RoundTrip(cv2),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown variability entry mode."),
    };

    private static TheoryWorkedRow Worked(
        string symbol, string name, string formula, string substitution, string value)
        => new(symbol, name, formula, substitution, value);

    private static string WaitFormula(TheoryModel model) => model switch
    {
        TheoryModel.Mm1 => "ρ / [μ(1 − ρ)]",
        TheoryModel.Mg1 => "ρ(1 + Cs²) / [2μ(1 − ρ)]",
        TheoryModel.Gg1 => "ρ(Ca² + Cs²) / [2μ(1 − ρ)]  ≈",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
    };

    private static string WaitSubstitution(
        TheoryModel model, double rho, double mu, double ca2, double cs2, double wq,
        string muTerm, bool inverted)
    {
        string numerator = model switch
        {
            TheoryModel.Mm1 => N(rho),
            TheoryModel.Mg1 => $"{N(rho)} × (1 + {N(cs2)}) / 2",
            TheoryModel.Gg1 => $"{N(rho)} × ({N(ca2)} + {N(cs2)}) / 2",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
        };

        return $"= {numerator}\n/ [{muTerm} × (1 − {N(rho)})]\n"
            + (inverted ? $"= {numerator} / [{N(mu)} × (1 − {N(rho)})]\n" : string.Empty)
            + $"= {N(wq)} min";
    }

    private static string FormulaSheet(TheoryModel model, double ca2, double cs2)
    {
        const string stability =
            "Stability: ρ = λ / μ must be < 1.\n"
            + "At ρ ≥ 1 the queue grows without bound, so no steady-state answer exists.\n"
            + "Symbols: λ arrival rate; μ service rate; ρ = λ/μ; Ca², Cs² squared coefficients of variation.\n"
            + "Lq, Wq queue length and wait; L, W system length and time; P₀ idle fraction.";

        const string minutes = "Aggregate waiting minutes per hour = 60·Lq (all customers together).";

        return model switch
        {
            TheoryModel.Mm1 =>
                "M/M/1 — exact Erlang-C at one server\n"
                + stability + "\n\n"
                + "1. Ca² = 1 and Cs² = 1 by the exponential assumption; spread entry does not apply.\n"
                + "2. P₀ = 1 − ρ\n"
                + "3. P(wait) = ρ (PASTA)\n"
                + "4. Lq = ρ² / (1 − ρ)\n"
                + "5. Wq = Lq / λ\n"
                + "6. Wq = ρ / [μ(1 − ρ)]\n"
                + "7. W = Wq + 1/μ\n"
                + "8. L = λW (Little's law)\n"
                + "9. " + minutes,

            TheoryModel.Mg1 =>
                "M/G/1 — Pollaczek–Khinchine, exact at one server\n"
                + stability + "\n\n"
                + "1. Ca² = 1 (Poisson arrivals).\n"
                + $"2. Cs² = {N(cs2)} from the entered spread.\n"
                + "3. Cs² = 0 is deterministic, 1 is exponential, above 1 is high variability.\n"
                + "4. Wq = ρ(1 + Cs²) / [2μ(1 − ρ)]\n"
                + "5. Lq = λWq\n"
                + "6. W = Wq + 1/μ\n"
                + "7. L = λW (Little's law)\n"
                + "8. P₀ = 1 − ρ\n"
                + "9. P(wait) = ρ (PASTA)\n"
                + "10. " + minutes
                + "\n11. Cs² = 1 gives exactly the M/M/1 answer — change the service spread to see a difference.",

            TheoryModel.Gg1 =>
                "G/G/1 — Allen–Cunneen / Kingman two-moment APPROXIMATION\n"
                + stability + "\n\n"
                + $"1. Ca² = {N(ca2)} and Cs² = {N(cs2)} from the entered spreads.\n"
                + "2. Wq ≈ [ρ / (μ(1 − ρ))] · (Ca² + Cs²) / 2\n"
                + "3. Lq ≈ λWq\n"
                + "4. W ≈ Wq + 1/μ\n"
                + "5. L ≈ λW (Little's law)\n"
                + "6. P₀ = 1 − ρ (exact for any work-conserving single server)\n"
                + "7. " + minutes
                + "\n8. P(wait) is not reported: arrival-epoch probabilities need more than two moments."
                + "\n9. Exact when Ca² = 1 (Poisson), where this reduces to M/G/1."
                + "\n10. Otherwise a heavy-traffic approximation, not an exact G/G/1 result.",

            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
        };
    }

    private static string RoundTrip(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Pct(double value) =>
        (value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";
}
