using OpdSimulator.Core.Distributions;

namespace OpdSimulator.Core.Stages;

/// <summary>
/// Immutable description of one service stage in the serial network.
/// </summary>
/// <remarks>
/// <para>
/// Holds the per-stage configuration (name, number of parallel servers <c>c</c>,
/// service rate per server μ, and optionally the distribution to draw service times
/// from). The <see cref="Engine"/> materialises a fresh <see cref="Stage"/> runtime
/// object for every run so that no queue/server state leaks between runs that use
/// different seeds (NFR-4).
/// </para>
/// <para>
/// <paramref name="serviceDistribution"/> is optional so that every pre-8I caller keeps
/// compiling and keeps its exact behaviour: a stage constructed with only
/// (name, c, μ) runs M/M/c, because <see cref="EffectiveServiceDistribution"/> falls
/// back to Exponential with mean 1/μ.
/// </para>
/// </remarks>
public sealed record StageSpec
{
    /// <summary>
    /// Creates a stage description.
    /// </summary>
    /// <param name="name">Human-readable stage name (e.g. Reception, Screening, Doctor).</param>
    /// <param name="serverCount">Number of parallel servers c at the stage.</param>
    /// <param name="serviceRate">Service rate per server μ (patients per minute).</param>
    /// <param name="serviceDistribution">
    /// Distribution to sample service times from, or null to use Exponential with
    /// mean 1/<paramref name="serviceRate"/>. The explicit form is what makes a stage
    /// something other than M/M/c — M/D/c, M/M/c, M/E2/c and so on.
    /// </param>
    public StageSpec(
        string name,
        int serverCount,
        double serviceRate,
        DistributionSpec? serviceDistribution = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Stage name must not be empty.", nameof(name));
        if (serverCount < 1)
            throw new ArgumentOutOfRangeException(nameof(serverCount), serverCount, "At least one server is required.");
        if (serviceRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(serviceRate), serviceRate, "Service rate μ must be strictly positive.");

        Name = name;
        ServerCount = serverCount;
        ServiceRate = serviceRate;
        ServiceDistribution = serviceDistribution;
    }

    /// <summary>Human-readable stage name.</summary>
    public string Name { get; }

    /// <summary>Number of parallel servers c at the stage.</summary>
    public int ServerCount { get; }

    /// <summary>
    /// Service rate per server μ (patients per minute).
    /// </summary>
    /// <remarks>
    /// Retained for compatibility and for the analytical <c>ρ = λ/(c·μ)</c> stability
    /// check, which is defined in terms of the rate. It is also what
    /// <see cref="EffectiveServiceDistribution"/> converts into an exponential mean when
    /// no explicit distribution was given.
    /// </remarks>
    public double ServiceRate { get; }

    /// <summary>
    /// The explicitly requested service-time distribution, or null when the stage was
    /// described by rate alone.
    /// </summary>
    public DistributionSpec? ServiceDistribution { get; }

    /// <summary>
    /// The distribution to sample service times from. Falls back to Exponential with
    /// mean = 1/<see cref="ServiceRate"/> when no explicit spec is supplied — keeps every
    /// existing caller compiling unchanged.
    /// </summary>
    public DistributionSpec EffectiveServiceDistribution =>
        ServiceDistribution
        ?? new DistributionSpec(
            DistributionFamily.Exponential,
            Mean: 1.0 / ServiceRate);
}