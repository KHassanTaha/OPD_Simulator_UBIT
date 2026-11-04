namespace OpdSimulator.Core.Tests;

using OpdSimulator.Core.Analytics;
using OpdSimulator.Core.Distributions;
using Xunit;

/// <summary>
/// Verifies the closed-form models against values derived by hand from the textbook
/// formulas (Erlang-C, Pollaczek–Khinchine, Kingman, Allen–Cunneen). The point of
/// these tests is the identity claims the report makes: M/G/c at c = 1 <b>is</b> P-K,
/// and G/G/c at c = 1 <b>is</b> Kingman. If those identities ever stop holding, the
/// report is quoting the wrong formula name (D-208).
/// </summary>
public class AnalyticalModelsTests
{
    [Fact]
    public void Mmc_SingleServer_MatchesErlangCClosedForm()
    {
        var inputs = new AnalyticalModelInputs(Lambda: 0.5, Mu: 0.8, Servers: 1, Ca2: 1.0, Cs2: 1.0);

        Assert.True(AnalyticalModels.TryCompute(AnalyticalModelFamily.Mmc, inputs, out var result, out var refusal), refusal);

        // ρ = 0.625, Lq = ρ²/(1−ρ) = 1.041667, Wq = Lq/λ = 2.083333.
        Assert.Equal(0.625, result!.Rho, 9);
        Assert.Equal(1.0416667, result.Lq, 6);
        Assert.Equal(2.0833333, result.Wq, 6);
        Assert.Equal(0.375, result.P0, 9);
    }

    [Fact]
    public void Mmc_TwoServers_MatchesErlangCByHand()
    {
        // λ = 1, μ = 0.6, c = 2 → ρ = 0.833333, a = 1.666667.
        // P0 = [1 + a + a²/(2(1−ρ))]⁻¹ = 1/11 = 0.090909.
        // Pw = a²/(2(1−ρ))·P0 = 0.757576, Lq = Pw·ρ/(1−ρ) = 3.787879.
        var inputs = new AnalyticalModelInputs(1.0, 0.6, 2, 1.0, 1.0);

        Assert.True(AnalyticalModels.TryCompute(AnalyticalModelFamily.Mmc, inputs, out var result, out var refusal), refusal);

        Assert.Equal(1.0 / 11.0, result!.P0, 9);
        Assert.Equal(0.7575758, result.PWait, 6);
        Assert.Equal(3.7878788, result.Lq, 5);
        Assert.Equal(3.7878788, result.Wq, 5);
    }

    [Fact]
    public void Mgc_SingleServer_IsPollaczekKhinchine()
    {
        // P-K: Wq = ρ(1 + Cs²) / (2μ(1 − ρ)) = 0.625·3 / (2·0.8·0.375) = 3.125.
        var inputs = new AnalyticalModelInputs(0.5, 0.8, 1, Ca2: 1.0, Cs2: 2.0);

        Assert.True(AnalyticalModels.TryCompute(AnalyticalModelFamily.Mgc, inputs, out var result, out var refusal), refusal);

        Assert.Equal(3.125, result!.Wq, 9);
        Assert.Contains("Pollaczek", result.FormulaName);
    }

    [Fact]
    public void Ggc_SingleServer_IsKingman()
    {
        // Kingman: Wq = (Ca² + Cs²)/2 · (ρ/(1−ρ)) / μ = 2 · 2.083333 = 4.166667.
        var inputs = new AnalyticalModelInputs(0.5, 0.8, 1, Ca2: 2.0, Cs2: 2.0);

        Assert.True(AnalyticalModels.TryCompute(AnalyticalModelFamily.Ggc, inputs, out var result, out var refusal), refusal);

        Assert.Equal(4.1666667, result!.Wq, 6);
        Assert.Contains("Kingman", result.FormulaName);
    }

    [Fact]
    public void AllFamilies_SatisfyLittleLaw()
    {
        var inputs = new AnalyticalModelInputs(1.0, 0.6, 2, Ca2: 1.5, Cs2: 0.7);

        foreach (AnalyticalModelFamily family in Enum.GetValues<AnalyticalModelFamily>())
        {
            Assert.True(AnalyticalModels.TryCompute(family, inputs, out var result, out var refusal), refusal);
            Assert.Equal(inputs.Lambda * result!.Wq, result.Lq, 9);
            Assert.Equal(inputs.Lambda * result.W, result.L, 9);
        }
    }

    [Fact]
    public void UnstableStage_IsRefusedByName()
    {
        var inputs = new AnalyticalModelInputs(1.0, 0.5, 1, 1.0, 1.0);

        Assert.False(AnalyticalModels.TryCompute(AnalyticalModelFamily.Mmc, inputs, out var result, out var refusal));
        Assert.Null(result);
        Assert.Contains("unstable", refusal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ρ", refusal);
    }

    [Theory]
    [InlineData(DistributionFamily.Exponential, 1.0)]
    [InlineData(DistributionFamily.Deterministic, 0.0)]
    [InlineData(DistributionFamily.Gamma, 0.5)]
    [InlineData(DistributionFamily.Normal, 0.25)]
    [InlineData(DistributionFamily.Uniform, 0.333333333)]
    public void SquaredCoefficientOfVariation_ComesFromTheConfiguredSpec(DistributionFamily family, double expected)
    {
        DistributionSpec spec = family switch
        {
            DistributionFamily.Exponential => new DistributionSpec(family, 2.0),
            DistributionFamily.Deterministic => new DistributionSpec(family, 2.0),
            DistributionFamily.Gamma => new DistributionSpec(family, 2.0, Shape: 2.0, Scale: 1.0),
            DistributionFamily.Normal => new DistributionSpec(family, 2.0, StdDev: 1.0),
            DistributionFamily.Uniform => new DistributionSpec(family, 1.0, Min: 0.0, Max: 2.0),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

        Assert.Equal(expected, AnalyticalModels.SquaredCoefficientOfVariation(spec), 6);
    }
}
