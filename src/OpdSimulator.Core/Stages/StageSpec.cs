namespace OpdSimulator.Core.Stages;

/// <summary>
/// Immutable description of one service stage in the serial network.
/// </summary>
/// <remarks>
/// Holds the per-stage configuration (name, number of parallel servers <c>c</c>,
/// and service rate per server μ). The <see cref="Engine"/> materialises a fresh
/// <see cref="Stage"/> runtime object for every run so that no queue/server state
/// leaks between runs that use different seeds (NFR-4).
/// </remarks>
public sealed record StageSpec
{
    /// <summary>
    /// Creates a stage description.
    /// </summary>
    /// <param name="name">Human-readable stage name (e.g. Reception, Screening, Doctor).</param>
    /// <param name="serverCount">Number of parallel servers c at the stage.</param>
    /// <param name="serviceRate">Service rate per server μ (patients per minute).</param>
    public StageSpec(string name, int serverCount, double serviceRate)
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
    }

    /// <summary>Human-readable stage name.</summary>
    public string Name { get; }

    /// <summary>Number of parallel servers c at the stage.</summary>
    public int ServerCount { get; }

    /// <summary>Service rate per server μ (patients per minute).</summary>
    public double ServiceRate { get; }
}