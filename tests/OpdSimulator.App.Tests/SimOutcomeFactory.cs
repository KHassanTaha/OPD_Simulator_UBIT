using OpdSimulator.App.Models;
using OpdSimulator.App.Services;
using OpdSimulator.Data.Parameters;

namespace OpdSimulator.App.Tests;

/// <summary>
/// Builds the deterministic specimen run used by the results/chart tests so
/// they share one source of truth instead of each constructing engine inputs.
/// </summary>
internal static class SimOutcomeFactory
{
    /// <summary>Completes a small, stable manual run (no data file) with a fixed seed.</summary>
    public static RunOutcome ManualRun(int seed = 7)
        => SimulationCoordinator.Run(Parameters(seed), null, null);

    /// <summary>A stable manual configuration: λ=0.3/min outside, μ=0.5/min per server, one server per stage.</summary>
    public static SimulationParameters Parameters(int seed = 7)
        => new(
            ParameterMode.RateWise,
            "Exponential",
            "Exponential",
            ArrivalRate: 0.3,
            new[] { "Reception", "Screening", "Doctor" },
            new[] { 1, 1, 1 },
            new[] { 0.5, 0.5, 0.5 },
            HorizonMode.Days,
            HorizonMinutes: 0,
            GeneratorDays: 1,
            StartDay: DayOfWeek.Monday,
            DailyCap: null,
            Seed: seed,
            PExitOverride: null,
            TraceLevelName: "None");
}