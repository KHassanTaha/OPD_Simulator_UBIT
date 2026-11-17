namespace OpdSimulator.App.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using OpdSimulator.Core.Analytics;

/// <summary>
/// The three single-server models the Theory tab answers (FR-UI-44, D-246).
/// </summary>
/// <remarks>
/// A separate App-level enum is deliberate: Core's <see cref="AnalyticalModelFamily"/>
/// is phrased for <c>c</c> servers and is used by the multi-stage validation path,
/// while the Theory tab only ever answers <c>c = 1</c>. Keeping the UI's model
/// list here makes it impossible to offer M/M/c from the textbook tab by accident.
/// </remarks>
public enum TheoryModel
{
    /// <summary>M/M/1 — exact Erlang-C at one server.</summary>
    Mm1 = 0,

    /// <summary>M/G/1 — Pollaczek–Khinchine, exact at one server.</summary>
    Mg1 = 1,

    /// <summary>G/G/1 — Allen–Cunneen / Kingman two-moment approximation.</summary>
    Gg1 = 2,
}

/// <summary>
/// How the user supplies a process's spread in the Theory tab.
/// </summary>
/// <remarks>
/// All five are the same quantity written differently: <c>Cv → Cv²</c>,
/// <c>Variance → Variance / Mean²</c>, <c>SecondMoment → E[X²]/Mean² − 1</c>,
/// <c>StandardDeviation → (σ / Mean)²</c>, <c>SquaredCv → C²</c>.
/// The view model performs that conversion; this enum only names the entry form.
/// </remarks>
public enum VariabilityMode
{
    /// <summary>Coefficient of variation, σ / E[X].</summary>
    Cv = 0,

    /// <summary>Variance, σ², in squared time units.</summary>
    Variance = 1,

    /// <summary>Second raw moment, E[X²], in squared time units.</summary>
    SecondMoment = 2,

    /// <summary>Standard deviation σ, in time units (D-247).</summary>
    StandardDeviation = 3,

    /// <summary>Squared coefficient of variation C² = (σ / E[X])² (D-247).</summary>
    SquaredCv = 4,
}

/// <summary>
/// One line of the answer key: the symbol, the closed form, the numbers put
/// into it, and the value Core returned.
/// </summary>
/// <param name="Symbol">Short metric name, e.g. <c>Wq</c>.</param>
/// <param name="Name">The metric's full name, shown beside the abbreviation (D-247).</param>
/// <param name="Formula">The closed form with symbols, e.g. <c>ρ / [μ(1 − ρ)]</c>.</param>
/// <param name="Substitution">The same form with the entered numbers, one step per line (D-247).</param>
/// <param name="Value">The value with its unit, taken from Core's result.</param>
/// <param name="SerialNumber">1-based display order, so a row can be cited in the viva.</param>
public sealed record TheoryWorkedRow(
    string Symbol,
    string Name,
    string Formula,
    string Substitution,
    string Value,
    int SerialNumber = 0);

/// <summary>
/// The complete answer key for one single-server question.
/// </summary>
/// <param name="Model">The model that was evaluated.</param>
/// <param name="Result">Core's closed-form result — the one arithmetic authority.</param>
/// <param name="Rows">Worked substitutions, one per reported metric.</param>
/// <param name="FormulaSheetText">Expandable per-model derivation and symbol list.</param>
/// <param name="ApproximationNotice">Empty for exact models; a prominent warning for G/G/1.</param>
/// <param name="PWaitNote">Why P(wait) is shown or deliberately omitted.</param>
public sealed record TheorySolution(
    TheoryModel Model,
    AnalyticalModelResult Result,
    IReadOnlyList<TheoryWorkedRow> Rows,
    string FormulaSheetText,
    string ApproximationNotice,
    string PWaitNote);

/// <summary>
/// Builds the Theory tab's worked answer from Core's closed-form result.
/// </summary>
/// <remarks>
/// <para>
/// This type is a pure function of its inputs (D-246). It adds <b>no</b> formula:
/// every number in <see cref="TheorySolution.Rows"/> is copied from
/// <see cref="AnalyticalModels.TryCompute"/>. The formula and substitution columns
/// only show <i>how that number was obtained</i>, which is what makes the tab an
/// answer key rather than a bare calculator.
/// </para>
/// <para>
/// Keeping the strings here rather than in the view model lets the worked steps
/// be asserted headlessly, including the three equivalent variability entry forms.
/// </para>
/// </remarks>
public static class TheoryWorkedSolutionBuilder
{
    /// <summary>
    /// Computes the answer key, or explains why the steady-state answer does not exist.
    /// </summary>
    /// <param name="model">The single-server model to evaluate.</param>
    /// <param name="lambda">Arrival rate λ, per minute.</param>
    /// <param name="mu">Service rate μ per server, per minute.</param>
    /// <param name="ca2">Squared coefficient of variation of inter-arrival times.</param>
    /// <param name="cs2">Squared coefficient of variation of service times.</param>
    /// <param name="solution">The answer key on success; otherwise <c>null</c>.</param>
    /// <param name="refusal">The named cause on failure; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the model is defined for these inputs.</returns>
    public static bool TryBuild(
        TheoryModel model,
        double lambda,
        double mu,
        double ca2,
        double cs2,
        out TheorySolution? solution,
        out string? refusal)
    {
        solution = null;

        // The three textbook assumptions fix one or both variability terms. They
        // are applied here, not in the view model, so the answer key and the Core
        // call can never disagree about which model was asked for.
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
        // D-247: each substitution step is its own line, and each metric carries
        // its full name beside the abbreviation so a viva answer is unambiguous.
        var rows = new List<TheoryWorkedRow>
        {
            Worked("ρ", "Traffic intensity (utilisation)", "λ / μ",
                $"= {N(lambda)} / {N(mu)}\n= {N(answer.Rho)}", N(answer.Rho)),
            Worked("P₀", "Server idle probability", "1 − ρ",
                $"= 1 − {N(answer.Rho)}\n= {N(answer.P0)}", N(answer.P0)),
            Worked("Wq", "Mean queue waiting time", WaitFormula(model),
                WaitSubstitution(model, answer.Rho, mu, arrivalCv2, serviceCv2, answer.Wq),
                $"{N(answer.Wq)} min"),
            Worked("Lq", "Mean queue length", "λ · Wq",
                $"= {N(lambda)} × {N(answer.Wq)}\n= {N(answer.Lq)}", N(answer.Lq)),
            Worked("W", "Mean time in system", "Wq + 1 / μ",
                $"= {N(answer.Wq)} + 1 / {N(mu)}\n= {N(answer.W)}", $"{N(answer.W)} min"),
            Worked("L", "Mean number in system", "λ · W",
                $"= {N(lambda)} × {N(answer.W)}\n= {N(answer.L)}", N(answer.L)),
            Worked("Busy", "Server busy fraction", "ρ (one server)",
                $"= {N(answer.Rho)}\n= {Pct(answer.Rho)}", Pct(answer.Rho)),
            Worked("Idle", "Server idle fraction", "1 − ρ",
                $"= {N(answer.P0)}\n= {Pct(answer.P0)}", Pct(answer.P0)),
            Worked("Wait/h", "Aggregate customer wait per hour", "60 · Lq",
                $"= 60 × {N(answer.Lq)}\n= {N(60 * answer.Lq)} min/h\n(total across all customers)",
                $"{N(60 * answer.Lq)} min/h"),
        };

        // PASTA holds for Poisson arrivals, so M/M/1 and M/G/1 may report the
        // arrival-epoch wait probability. A general arrival process has no such
        // two-moment result; showing Core's M/M/c reference ρ there would be a lie.
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

        // D-247: the P(wait) row already prints the formula and value for the
        // Poisson models. Only G/G/1 needs a caption, and there it explains an
        // ABSENT row rather than repeating a present one.
        string pWaitNote = model == TheoryModel.Gg1
            ? "P(wait) is intentionally omitted. With general arrivals the probability at arrival "
              + "epochs is not determined by Ca² and Cs² alone."
            : string.Empty;

        // FR-UI-36: a header row makes this a listing table, so number it from 1
        // in display order rather than making the reader count to cite a line.
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

    /// <summary>Converts a squared CV into the form the user chose.</summary>
    /// <param name="cv2">Squared coefficient of variation.</param>
    /// <param name="mean">The process mean in the chosen time unit.</param>
    /// <param name="mode">The entry form to produce.</param>
    /// <returns>A culture-invariant number string in the requested form.</returns>
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

    /// <summary>
    /// The numeric substitution for Wq, with one arithmetic step per line (D-247).
    /// The final value is Core's <paramref name="wq"/>; the intermediate lines only
    /// display the inputs, so no second arithmetic authority is introduced.
    /// </summary>
    private static string WaitSubstitution(
        TheoryModel model, double rho, double mu, double ca2, double cs2, double wq)
    {
        string numerator = model switch
        {
            TheoryModel.Mm1 => $"= {N(rho)}",
            TheoryModel.Mg1 => $"= {N(rho)} × (1 + {N(cs2)}) / 2",
            TheoryModel.Gg1 => $"= {N(rho)} × ({N(ca2)} + {N(cs2)}) / 2",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Unknown theory model."),
        };

        return $"{numerator}\n/ [{N(mu)} × (1 − {N(rho)})]\n= {N(wq)} min";
    }

    private static string FormulaSheet(TheoryModel model, double ca2, double cs2)
    {
        // D-247: one closed form per line. The old sheet joined two or three
        // formulas with ";" or "and", which made a substitution hard to follow.
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

    /// <summary>
    /// Culture-invariant high-precision text for values that are written back into
    /// an input field. Four decimals would turn 0.390625 into 0.3906 and change
    /// the answer on the next recompute (the variance/second-moment round trip).
    /// </summary>
    private static string RoundTrip(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Culture-invariant fixed point — the answer key must not change with locale.</summary>
    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Culture-invariant percentage.</summary>
    private static string Pct(double value) =>
        (value * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";
}
