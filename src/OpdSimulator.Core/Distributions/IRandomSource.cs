namespace OpdSimulator.Core.Distributions;

/// <summary>
/// Abstraction over a deterministic random number source.
/// </summary>
/// <remarks>
/// Seeding the source before each run guarantees a reproducible sequence of
/// draws (FR-VAL-3 / NFR-4). Hiding the concrete generator behind an interface
/// lets the engine work with any source without knowing its implementation.
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Resets the source so a run is fully deterministic for a given seed.
    /// </summary>
    /// <param name="seed">The integer seed to reset the sequence from.</param>
    void SetSeed(int seed);

    /// <summary>
    /// Draws the next uniform random number in [0, 1).
    /// </summary>
    /// <returns>A uniform deviate in [0, 1).</returns>
    double NextDouble();
}