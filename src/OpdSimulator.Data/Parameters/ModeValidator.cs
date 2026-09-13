namespace OpdSimulator.Data.Parameters;

/// <summary>
/// Validates a user-supplied parameter value against the chosen <see cref="ParameterMode"/>.
/// </summary>
/// <remarks>
/// <para>
/// Rate-wise input must be strictly positive (λ, μ = 0 or negative makes the M/M/c
/// model unstable/meaningless). Mean-wise input must satisfy the stability
/// condition <c>mean_service / mean_inter_arrival &lt; c</c> (ρ &lt; 1) or the
/// queue never empties and the run is not valid for comparison with theory.
/// </para>
/// <para>
/// <b>Failure is a soft warning, never a hard blockage</b> (kickoff F): the run
/// goes ahead on purpose — an unstable queue produces perfectly valid DES output
/// (an ever-growing queue) that the user may legitimately want to inspect. The
/// warning is printed so the interpretation is never silent.
/// </remarks>
public static class ModeValidator
{
    /// <summary>
    /// Reports whether the given parameters are usable in the chosen mode.
    /// </summary>
    /// <param name="mode">The interpretation the user selected.</param>
    /// <param name="arrivalParameter">λ (rate mode) or mean inter-arrival time (mean mode).</param>
    /// <param name="serviceParameter">μ (rate mode) or mean service time (mean mode).</param>
    /// <param name="serverCount">Number of servers c.</param>
    /// <param name="warning">The human-readable warning, or empty when everything is fine.</param>
    /// <returns><see langword="true"/> when no warning is warranted.</returns>
    public static bool IsValid(ParameterMode mode, double arrivalParameter, double serviceParameter, int serverCount, out string warning)
    {
        warning = string.Empty;

        if (mode == ParameterMode.RateWise)
        {
            if (arrivalParameter <= 0 || serviceParameter <= 0)
            {
                warning = "Rate-wise input must be strictly positive (λ > 0, μ > 0).";
                return false;
            }

            double rho = arrivalParameter / (serviceParameter * serverCount);
            if (rho >= 1.0)
            {
                warning = $"ρ = {rho:0.###} ≥ 1 — the queue will never reach steady state; results are informative, not comparable to M/M/c theory. (Run continues.)";
                return false;
            }

            return true;
        }

        // Mean-wise: check ρ = meanS / (meanA * c) < 1.
        if (arrivalParameter <= 0 || serviceParameter <= 0)
        {
            warning = "Mean-wise input must be strictly positive (mean inter-arrival > 0, mean service > 0).";
            return false;
        }

        double rhoMean = serviceParameter / (arrivalParameter * serverCount);
        if (rhoMean >= 1.0)
        {
            warning = $"ρ = {rhoMean:0.###} ≥ 1 — the queue will never reach steady state; results are informative, not comparable to M/M/c theory. (Run continues.)";
            return false;
        }

        return true;
    }
}