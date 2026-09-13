using OpdSimulator.Core.Distributions;

namespace OpdSimulator.Core.Tests;

/// <summary>
/// Tests for <see cref="SeededRandomSource"/>: same seed ⇒ same sequence (NFR-4);
/// re-seeding restarts the sequence; values stay in [0, 1).
/// </summary>
public class SeededRandomSourceTests
{
    [Fact]
    public void TwoSources_SameSeed_ProduceIdenticalSequences()
    {
        var a = new SeededRandomSource(42);
        var b = new SeededRandomSource(42);

        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void TwoSources_DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new SeededRandomSource(42);
        var b = new SeededRandomSource(43);

        bool differs = false;
        for (int i = 0; i < 1000 && !differs; i++)
            differs |= a.NextDouble() != b.NextDouble();

        Assert.True(differs, "Two different seeds must eventually diverge.");
    }

    [Fact]
    public void SetSeed_RestartsTheSequence()
    {
        var source = new SeededRandomSource(7);
        source.NextDouble();

        source.SetSeed(7);
        var fresh = new SeededRandomSource(7);

        for (int i = 0; i < 100; i++)
            Assert.Equal(source.NextDouble(), fresh.NextDouble());
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var source = new SeededRandomSource(42);
        for (int i = 0; i < 10000; i++)
        {
            double value = source.NextDouble();
            Assert.InRange(value, 0.0, 1.0);
        }
    }
}