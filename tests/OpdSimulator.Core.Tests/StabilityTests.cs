using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using Serilog;

namespace OpdSimulator.Core.Tests;

using Engine = OpdSimulator.Core.Engine.Engine;

/// <summary>
/// Stability tests (FR-VAL-1): ρ ≥ 1 must refuse to run with an
/// explanation carrying λ, c, μ and ρ.
/// </summary>
public class StabilityTests
{
    private static readonly ILogger Log = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .CreateLogger();

    [Fact]
    public void Run_RhoOverOne_RhoGreaterThanOrEqualToOne_Throws()
    {
        // λ = 5, μ = 4, c = 1 → ρ = 1.25 ≥ 1 → must refuse.
        var config = new EngineConfig(5.0, 4.0, serverCount: 1, horizonMinutes: 1000, seed: 42);
        var engine = new Engine(config, new SeededRandomSource(), Log);

        var ex = Assert.Throws<UnstableSystemException>(() => engine.Run());

        Assert.Equal(1.25, ex.Rho, 3);
        Assert.Equal(config.StageName, ex.StageName);
        Assert.Contains("1.25", ex.Message);
        Assert.Contains("5", ex.Message);   // λ
        Assert.Contains("4", ex.Message);   // μ and c
    }

    [Fact]
    public void Run_RhoExactlyOne_Throws()
    {
        // λ = c·μ exactly → ρ = 1 → must refuse (FR-VAL-1 refuses ρ ≥ 1).
        var config = new EngineConfig(4.0, 4.0, serverCount: 1, horizonMinutes: 1000, seed: 42);
        var engine = new Engine(config, new SeededRandomSource(), Log);

        Assert.Throws<UnstableSystemException>(() => engine.Run());
    }

    [Fact]
    public void Run_RhoBelowOne_DoesNotThrow()
    {
        var config = new EngineConfig(3.0, 4.0, serverCount: 1, horizonMinutes: 1000, seed: 42);
        var engine = new Engine(config, new SeededRandomSource(), Log);

        var result = engine.Run();
        Assert.NotNull(result);
    }

    [Fact]
    public void Rho_ComputedAsLambdaOverCtimesMu()
    {
        var config = new EngineConfig(6.0, 2.0, serverCount: 3, horizonMinutes: 10, seed: 42);
        Assert.Equal(1.0, config.Rho, 3); // 6 / (3·2) = 1.0
    }
}