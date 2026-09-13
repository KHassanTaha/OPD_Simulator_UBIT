namespace OpdSimulator.Data.Tests;

using OpdSimulator.Data.Parameters;
using Xunit;

public class ModeValidatorTests
{
    [Fact]
    public void RateWise_PositiveAndStable_IsValid()
    {
        // λ=0.5/min, μ=0.75/min, c=1 → ρ=0.667
        Assert.True(ModeValidator.IsValid(ParameterMode.RateWise, 0.5, 0.75, 1, out string warning));
        Assert.Equal(string.Empty, warning);
    }

    [Fact]
    public void RateWise_NonPositiveRates_AreRejected()
    {
        Assert.False(ModeValidator.IsValid(ParameterMode.RateWise, 0, 0.75, 1, out string warning));
        Assert.Contains("positive", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RateWise_UnstableQueue_WarnsButStillRuns()
    {
        // λ=1, μ=0.5, c=1 → ρ=2: no steady state exists.
        Assert.False(ModeValidator.IsValid(ParameterMode.RateWise, 1.0, 0.5, 1, out string warning));
        Assert.Contains("ρ", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeanWise_Stable_IsValid()
    {
        // mean inter-arrival 3, mean service 1.5, c=1 → ρ=0.5
        Assert.True(ModeValidator.IsValid(ParameterMode.MeanWise, 3.0, 1.5, 1, out string warning));
        Assert.Equal(string.Empty, warning);
    }

    [Fact]
    public void MeanWise_Unstable_WarnsRatherThanBlocks()
    {
        // mean inter-arrival 1, mean service 2.5, c=1 → ρ=2.5
        Assert.False(ModeValidator.IsValid(ParameterMode.MeanWise, 1.0, 2.5, 1, out string warning));
        Assert.Contains("ρ", warning, StringComparison.OrdinalIgnoreCase);
    }
}