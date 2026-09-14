namespace OpdSimulator.App.Services;

using OpdSimulator.App.Models;
using OpdSimulator.Core.Calendar;
using OpdSimulator.Core.Distributions;
using OpdSimulator.Core.Engine;
using OpdSimulator.Core.Stages;
using OpdSimulator.Core.Trace;
using Serilog;

/// <summary>
/// The completed result of one GUI run: the engine report, the goodness-of-fit
/// reports over the loaded data series, the rendered trace lines and any
/// refusal/error text. <see cref="Error"/> is null only on a successful run.
/// </summary>
/// <param name="Result">Engine report, or null when the run refused.</param>
/// <param name="Fits">Fit reports (empty when no data file was used).</param>
/// <param name="TraceLines">Rendered trace lines at the requested level.</param>
/// <param name="EffectiveExitProbability">Exit probability the run actually used.</param>
/// <param name="Error">Clean refusal/error message, or null on success.</param>
public sealed record RunOutcome(
    SimulationResult? Result,
    IReadOnlyList<FitReport> Fits,
    IReadOnlyList<string> TraceLines,
    double EffectiveExitProbability,
    string? Error);

/// <summary>
/// Runs one simulation configuration with the fitted/manual parameters and
/// collects the artifacts the results panel needs (metrics, fits, trace).
/// Mirrors the CLI's simulate-data run stage; the engine refuses unstable
/// topologies, so any <see cref="UnstableSystemException"/> becomes the
/// outcome's <see cref="RunOutcome.Error"/> for the GUI dialog.
/// </summary>
public static class SimulationCoordinator
{
    /// <summary>Exit probability used when neither a manual override nor data provides one.</summary>
    public const double DefaultExitProbability = 0.4;

    /// <summary>Number of stages the GUI always simulates (clinic flow, CONTEXT §1.1).</summary>
    public static readonly IReadOnlyList<string> FlowStageNames =
        new[] { "Reception", "Screening", "Doctor" };

    /// <summary>
    /// Runs a configuration synchronously (call on a worker thread).
    /// </summary>
    /// <param name="parameters">Validated inputs from the config panel.</param>
    /// <param name="binding">Loaded data binding, or null for a pure-manual run.</param>
    /// <param name="status">Optional progress text callback (e.g. "Running simulation…").</param>
    public static RunOutcome Run(SimulationParameters parameters, DataBindingResult? binding, Action<string>? status = null)
    {
        status?.Invoke("Fitting distributions…");
        var fits = BuildFits(parameters, binding);

        status?.Invoke("Running simulation…");
        double exitProbability = ResolveExitProbability(parameters, binding);
        var topology = new NetworkTopology(
            parameters.ArrivalRate,
            Enumerable.Range(0, FlowStageNames.Count)
                .Select(i => new StageSpec(parameters.StageNames[i], parameters.ServerCounts[i], parameters.ServiceRates[i]))
                .ToArray(),
            exitStageIndex: 1,
            exitProbability);

        var traceLevel = TraceLevelFromName(parameters.TraceLevelName);
        var sink = new CollectionTraceSink(traceLevel);

        try
        {
            var engine = new Core.Engine.Engine(new SeededRandomSource(), Log.Logger);
            SimulationResult result = parameters.HorizonMode == HorizonMode.Minutes
                ? engine.Run(topology, parameters.Seed, parameters.HorizonMinutes, sink)
                : engine.Run(topology, new ClinicCalendar(startDayOfWeek: parameters.StartDay),
                    parameters.GeneratorDays, parameters.Seed, parameters.DailyCap);

            status?.Invoke(string.Empty);
            return new RunOutcome(result, fits, sink.Lines, exitProbability, null);
        }
        catch (UnstableSystemException ex)
        {
            Log.Warning(ex, "Run refused: unstable configuration");
            return new RunOutcome(null, fits, sink.Lines, exitProbability,
                $"Run refused: {ex.Message}");
        }
    }

    private static IReadOnlyList<FitReport> BuildFits(SimulationParameters parameters, DataBindingResult? binding)
    {
        if (binding is null || !binding.IsUsable || binding.FittedArrivalRate is null)
        {
            return Array.Empty<FitReport>();
        }

        var reports = new List<FitReport>
        {
            FitsService.Fit("Inter-arrival", binding.InterArrivalMinutes, parameters.InterArrivalDistribution),
        };

        foreach (var stage in binding.StageNames)
        {
            if (binding.ServiceMinutesByStage.TryGetValue(stage, out var times))
            {
                reports.Add(FitsService.Fit($"{stage} service", times, parameters.ServiceDistribution));
            }
        }

        return reports;
    }

    internal static double ResolveExitProbability(SimulationParameters parameters, DataBindingResult? binding)
    {
        if (parameters.PExitOverride is { } manual)
            return manual;
        if (binding?.FittedExitProbability is { } fitted)
            return fitted;
        return DefaultExitProbability;
    }

    internal static TraceLevel TraceLevelFromName(string name)
        => name.Trim().ToUpperInvariant() switch
        {
            "EVENTS" => TraceLevel.Events,
            "STATE" => TraceLevel.State,
            "RNG" => TraceLevel.Rng,
            _ => TraceLevel.None,
        };
}