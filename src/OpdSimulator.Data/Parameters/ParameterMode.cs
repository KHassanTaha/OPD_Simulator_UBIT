namespace OpdSimulator.Data.Parameters;

/// <summary>
/// How the user intends to supply the distribution parameter(s) (FR-STAT-2).
/// </summary>
/// <remarks>
/// The clinic's Excel data records <em>times</em>, so the natural inputs in the
/// GUI are mean inter-arrival time and mean service time. But the simulation
/// engine and fitted results are expressed in rates (λ, μ). This enum tells the
/// UI which convention a given parameter box uses; the cost function logs the
/// interpretation cleanly instead of trusting a silently-inverted number.
/// </remarks>
public enum ParameterMode
{
    /// <summary>Values are rates: inter-arrival rate λ and service rate μ.</summary>
    RateWise,

    /// <summary>Values are means (in minutes): mean inter-arrival time and mean service time.</summary>
    MeanWise,
}