namespace OpdSimulator.Core.Distributions;

/// <summary>
/// A seeded <see cref="System.Random"/>-backed implementation of <see cref="IRandomSource"/>.
/// </summary>
/// <remarks>
/// Wraps <see cref="System.Random"/> so that the same seed always reproduces the
/// same sequence (PRD FR-VAL-3, default seed 42). .NET 8's <see cref="System.Random"/>
/// is deterministic per-process/per-runtime-version; pinning the runtime via
/// <c>global.json</c> keeps sequences stable across machines.
/// </remarks>
public sealed class SeededRandomSource : IRandomSource
{
    /// <summary>Default seed per PRD FR-VAL-3.</summary>
    public const int DefaultSeed = 42;

    private Random _random = new(DefaultSeed);

    /// <summary>
    /// Creates the source, immediately seeded.
    /// </summary>
    /// <param name="seed">Seed for the underlying generator (default 42).</param>
    public SeededRandomSource(int seed = DefaultSeed)
    {
        SetSeed(seed);
    }

    /// <inheritdoc />
    public void SetSeed(int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc />
    public double NextDouble() => _random.NextDouble();
}