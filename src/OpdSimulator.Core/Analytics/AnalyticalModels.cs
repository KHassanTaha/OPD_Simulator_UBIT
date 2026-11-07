namespace OpdSimulator.Core.Analytics;

using OpdSimulator.Core.Distributions;

/// <summary>
/// The closed-form queueing model used as the analytical reference for one stage.
/// </summary>
public enum AnalyticalModelFamily
{
    /// <summary>M/M/c — exact Erlang-C (both arrivals and service exponential).</summary>
    Mmc = 0,

    /// <summary>M/G/c — Allen–Cunneen approximation; for c = 1 it is exactly Pollaczek–Khinchine.</summary>
    Mgc = 1,

    /// <summary>G/G/c — Allen–Cunneen approximation; for c = 1 it is exactly Kingman's formula.</summary>
    Ggc = 2
}

/// <summary>
/// Inputs shared by every closed-form model: the stage's rates, server count, and the
/// squared coefficients of variation of the arrival and service processes.
/// </summary>
/// <param name="Lambda">Arrival rate λ into the stage (patients per minute).</param>
/// <param name="Mu">Service rate μ per server (patients per minute).</param>
/// <param name="Servers">Number of parallel servers c.</param>
/// <param name="Ca2">Squared coefficient of variation of inter-arrival times (1 for Poisson).</param>
/// <param name="Cs2">Squared coefficient of variation of service times (1 for exponential).</param>
public readonly record struct AnalyticalModelInputs(
    double Lambda,
    double Mu,
    int Servers,
    double Ca2,
    double Cs2);

/// <summary>
/// The closed-form metrics for one stage under one model.
/// </summary>
/// <param name="Family">Which model produced these numbers.</param>
/// <param name="DisplayName">Model name as shown to the user, e.g. <c>M/G/c</c>.</param>
/// <param name="FormulaName">Named formula behind <paramref name="Wq"/>, for the viva.</param>
/// <param name="Rho">Server utilisation ρ = λ / (c·μ).</param>
/// <param name="Lq">Expected number waiting in the queue.</param>
/// <param name="Wq">Expected time waiting in the queue, minutes.</param>
/// <param name="L">Expected number in the system.</param>
/// <param name="W">Expected time in the system, minutes.</param>
/// <param name="P0">Probability the system is empty (M/M/c reference state probabilities).</param>
/// <param name="PWait">Probability an arrival waits (M/M/c reference state probabilities).</param>
public sealed record AnalyticalModelResult(
    AnalyticalModelFamily Family,
    string DisplayName,
    string FormulaName,
    double Rho,
    double Lq,
    double Wq,
    double L,
    double W,
    double P0,
    double PWait);

/// <summary>
/// Closed-form queueing models for a single stage (FR-STAT-5 extension, D-208).
/// </summary>
/// <remarks>
/// <para>
/// All three models are built on the exact M/M/c result and then scaled, which is the
/// standard Allen–Cunneen presentation and keeps one derivation to defend:
/// </para>
/// <list type="bullet">
/// <item><b>M/M/c</b> — exact Erlang-C.</item>
/// <item><b>M/G/c</b> — <c>Wq ≈ Wq(M/M/c) · (1 + Cs²) / 2</c>. With c = 1 this reduces
/// exactly to the Pollaczek–Khinchine result <c>Wq = ρ(1 + Cs²) / (2μ(1 − ρ))</c>.</item>
/// <item><b>G/G/c</b> — <c>Wq ≈ Wq(M/M/c) · (Ca² + Cs²) / 2</c>. With c = 1 this is
/// exactly Kingman's heavy-traffic formula.</item>
/// </list>
/// <para>
/// Every model is a steady-state result, so a stage with ρ ≥ 1 is refused rather than
/// reported — the same rule the engine applies before a run (FR-VAL-1).
/// </para>
/// </remarks>
public static class AnalyticalModels
{
    /// <summary>
    /// Computes the closed-form metrics for one stage, or explains why they do not exist.
    /// </summary>
    /// <param name="family">The model to evaluate.</param>
    /// <param name="inputs">The stage's rates, server count and variability terms.</param>
    /// <param name="result">The metrics when this method returns <c>true</c>; otherwise <c>null</c>.</param>
    /// <param name="refusal">The reason when this method returns <c>false</c>; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the model is defined for these inputs.</returns>
    public static bool TryCompute(
        AnalyticalModelFamily family,
        AnalyticalModelInputs inputs,
        out AnalyticalModelResult? result,
        out string? refusal)
    {
        result = null;
        refusal = null;

        if (inputs.Lambda <= 0)
        {
            refusal = $"λ must be positive (was {inputs.Lambda:0.####}).";
            return false;
        }

        if (inputs.Mu <= 0)
        {
            refusal = $"μ must be positive (was {inputs.Mu:0.####}).";
            return false;
        }

        if (inputs.Servers < 1)
        {
            refusal = $"c must be at least 1 (was {inputs.Servers}).";
            return false;
        }

        if (inputs.Ca2 < 0 || !double.IsFinite(inputs.Ca2))
        {
            refusal = $"Ca² must be a finite, non-negative number (was {inputs.Ca2:0.####}).";
            return false;
        }

        if (inputs.Cs2 < 0 || !double.IsFinite(inputs.Cs2))
        {
            refusal = $"Cs² must be a finite, non-negative number (was {inputs.Cs2:0.####}).";
            return false;
        }

        double rho = inputs.Lambda / (inputs.Servers * inputs.Mu);
        if (rho >= 1.0)
        {
            refusal = $"Stage is unstable: ρ = {rho:0.####} ≥ 1, so the steady-state formulas do not exist.";
            return false;
        }

        // Exact M/M/c: offered load a and the Erlang-C delay probability.
        double a = inputs.Lambda / inputs.Mu;
        double p0 = ErlangCProbabilityEmpty(a, rho, inputs.Servers);
        double pWait = ErlangCDelayProbability(a, rho, p0, inputs.Servers);

        double lq = pWait * rho / (1.0 - rho);
        double wq = lq / inputs.Lambda;

        (string displayName, string formulaName, double scale) = family switch
        {
            AnalyticalModelFamily.Mmc => ("M/M/c", "Erlang-C (exact)", 1.0),
            AnalyticalModelFamily.Mgc => (
                "M/G/c",
                inputs.Servers == 1
                    ? "Pollaczek–Khinchine (exact for c = 1)"
                    : "Allen–Cunneen M/G/c approximation",
                (1.0 + inputs.Cs2) / 2.0),
            AnalyticalModelFamily.Ggc => (
                "G/G/c",
                inputs.Servers == 1
                    ? "Kingman's formula (exact for c = 1)"
                    : "Allen–Cunneen G/G/c approximation",
                (inputs.Ca2 + inputs.Cs2) / 2.0),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown analytical model family.")
        };

        wq *= scale;
        lq = inputs.Lambda * wq;

        // Little's law closes the two system-level values.
        double w = wq + (1.0 / inputs.Mu);
        double l = inputs.Lambda * w;

        result = new AnalyticalModelResult(
            family,
            displayName,
            formulaName,
            rho,
            lq,
            wq,
            l,
            w,
            p0,
            pWait);

        return true;
    }

    /// <summary>
    /// Squared coefficient of variation of a configured distribution, computed from its
    /// spec — never from a refit of the output (D-157).
    /// </summary>
    /// <param name="spec">The configured distribution for the stage.</param>
    /// <returns>Var(X) / E[X]², which is 1 for Exponential and 0 for Deterministic.</returns>
    /// <exception cref="ArgumentException">If the spec's mean is not positive.</exception>
    public static double SquaredCoefficientOfVariation(DistributionSpec spec)
        => DistributionSpecOperations.SquaredCoefficientOfVariation(spec);

    /// <summary>
    /// Builds the inputs for one stage from its rates and the configured distributions.
    /// </summary>
    /// <param name="lambda">Arrival rate into the stage (patients per minute).</param>
    /// <param name="mu">Service rate per server (patients per minute).</param>
    /// <param name="servers">Number of parallel servers.</param>
    /// <param name="serviceSpec">The stage's configured service distribution; null falls back to exponential (Cs² = 1).</param>
    /// <param name="arrivalCa2">
    /// Ca² for the arrivals. Defaults to 1 (exponential) for callers that mean the
    /// Poisson process; a configured G arrival supplies its own value (D-211).
    /// </param>
    /// <returns>The model inputs for this stage.</returns>
    public static AnalyticalModelInputs InputsFor(
        double lambda,
        double mu,
        int servers,
        DistributionSpec? serviceSpec,
        double arrivalCa2 = 1.0)
    {
        double cs2 = serviceSpec is null ? 1.0 : SquaredCoefficientOfVariation(serviceSpec);
        return new AnalyticalModelInputs(lambda, mu, servers, arrivalCa2, cs2);
    }

    /// <summary>P₀ = [ Σ_{k=0}^{c−1} aᵏ/k! + aᶜ/(c!(1−ρ)) ]⁻¹.</summary>
    private static double ErlangCProbabilityEmpty(double a, double rho, int servers)
    {
        double sum = 0;
        double term = 1; // a⁰/0!
        for (int k = 0; k < servers; k++)
        {
            if (k > 0)
            {
                term *= a / k;
            }

            sum += term;
        }

        double lastTerm = term * a / servers; // aᶜ/c!
        return 1.0 / (sum + (lastTerm / (1.0 - rho)));
    }

    /// <summary>C(c, a) = P(wait &gt; 0) = aᶜ/(c!(1−ρ)) · P₀.</summary>
    private static double ErlangCDelayProbability(double a, double rho, double p0, int servers)
    {
        double factorial = 1;
        for (int k = 2; k <= servers; k++)
        {
            factorial *= k;
        }

        return Math.Pow(a, servers) / (factorial * (1.0 - rho)) * p0;
    }

}
