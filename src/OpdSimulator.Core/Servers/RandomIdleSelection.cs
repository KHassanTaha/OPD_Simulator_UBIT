namespace OpdSimulator.Core.Servers;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Picks an idle server uniformly at random (D-017 production implementation).
/// </summary>
/// <remarks>
/// <para>
/// When exactly one server is idle the deterministic pick avoids a wasted RNG
/// draw, which keeps the single-server M/M/1 byte-for-byte regression stable
/// (seed 42 produces served = 29892, wait = 0.724). When ≥2 are idle the next
/// <see cref="IRandomSource.NextDouble"/> is consumed to compute a uniform
/// index in [0, idleCount).
/// </para>
/// </remarks>
public sealed class RandomIdleSelection : IServerSelectionPolicy
{
    /// <inheritdoc />
    public Server SelectIdleServer(IReadOnlyList<Server> servers, IRandomSource random)
    {
        // Count idle servers without allocating.
        int idleCount = 0;
        foreach (var s in servers)
            if (!s.IsBusy) idleCount++;

        if (idleCount == 0)
            throw new InvalidOperationException("No idle server available to select.");

        // Deterministic pick for the common single-idle case — no RNG draw,
        // so the single-server M1 regression stays byte-for-byte identical.
        if (idleCount == 1)
            return servers.First(s => !s.IsBusy);

        // Uniform index in [0, idleCount). NextDouble ∈ [0, 1), so
        // product ∈ [0, idleCount), and floor ∈ [0, idleCount-1].
        int pick = (int)(random.NextDouble() * idleCount);
        foreach (var s in servers)
        {
            if (s.IsBusy) continue;
            if (pick == 0) return s;
            pick--;
        }

        // Should be unreachable: the arithmetic guarantees a hit.
        throw new InvalidOperationException("Uniform pick fell outside the idle set.");
    }
}