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
/// Mirrors the CLI's <c>simulate-network</c> run stage (D-104).
/// </summary>
/// <remarks>
/// <para>
/// Gotchas G3/G4 (owner spec): the <see cref="NetworkTopology"/> is built
/// <strong>inside</strong> the try block so both refusal sources land in the
/// <see cref="RunOutcome.Error"/> banner — an <see cref="UnstableSystemException"/>
/// keeps its <em>exact</em> Core message (a ρ ≥ 1 refusal lists every unstable
/// stage with λᵢ, cᵢ, μᵢ, ρᵢ), and a topology-constructor
/// <see cref="ArgumentOutOfRangeException"/>(exitProbability) — the fitted
/// p_exit = 1.0 case — surfaces its Core wording. The clearer
/// "every row in the loaded data exits after Screening…" banner is emitted
/// before the topology is built, because the fitted value is known.
/// </para>
/// <para>
/// Run dispatch (5c.3): every mode records a trace through a
/// <see cref="CollectionTraceSink"/> — the calendar day modes forward the
/// sink via the calendar <c>Engine.Run</c> overload (D-110). DiagnosticTrace
/// remains the recommended hand-walk mode (D-105), but ClinicDay/MultiDay now
/// populate the event-trace widget too.
/// </para>
/// </remarks>
public static class SimulationCoordinator
{
    /// <summary>Exit probability used when neither a manual override nor data provides one.</summary>
    public const double DefaultExitProbability = 0.4;

    /// <summary>Banner shown when no arrival rate is available (no manual λ, no fitted λ).</summary>
    public const string MissingArrivalRateMessage =
        "Nothing to run yet: enter an arrival rate λ in Parameters, or load a valid data file.";

    /// <summary>Banner shown when a stage has neither a manual nor a fitted service rate (5d.1, D-112): names the stage.</summary>
    public const string MissingServiceRateMessage =
        "Stage '{0}' has no service rate. Upload data covering this stage, or enable Parameters to enter μ manually.";

    /// <summary>Banner shown when the data's fitted p_exit is 1.0 (no downstream route exists).</summary>
    public const string FittedPExitEqualsOneMessage =
        "p_exit = 1.0: every row in the loaded data exits after Screening, so no patient reaches a downstream stage. Load different data, or override p_exit with a value below 1 in Parameters.";

    /// <summary>
    /// Runs a configuration synchronously (call on a worker thread).
    /// </summary>
    /// <param name="parameters">Validated inputs from the config panel.</param>
    /// <param name="binding">Loaded data binding, or null for a pure-manual run.</param>
    /// <param name="status">Optional progress text callback (e.g. "Running simulation…").</param>
    /// <param name="significanceLevel">α used for every chi-square verdict (5d.2, D-113).</param>
    public static RunOutcome Run(SimulationParameters parameters, DataBindingResult? binding, Action<string>? status = null, double significanceLevel = FitsService.DefaultAlpha)
    {
        status?.Invoke("Fitting distributions…");
        var fits = BuildFits(parameters, binding, significanceLevel);

        status?.Invoke("Running simulation…");

        // ── Resolve routing (D-008/D-015/D-104) ─────────────────────────────
        double exitProbability = ResolveExitProbability(parameters, binding);
        if (exitProbability >= 1.0)
        {
            // Only a fitted value can reach 1.0 here: manual overrides are
            // config-validated to [0, 1). Surface the specific "no downstream
            // route" meaning instead of the raw Core message (5-F).
            return Refused(fits, exitProbability, FittedPExitEqualsOneMessage);
        }

        // ── Resolve the per-stage topology from manual-or-fitted values ─────
        double? arrivalRate = parameters.ManualArrivalRate ?? binding?.FittedArrivalRate;
        if (!(arrivalRate is > 0))
        {
            return Refused(fits, exitProbability, MissingArrivalRateMessage);
        }

        var stageResult = BuildStageSpecs(parameters, binding);
        if (stageResult.Specs is null)
        {
            return Refused(fits, exitProbability,
                string.Format(MissingServiceRateMessage, stageResult.MissingStageName ?? "?"));
        }

        int exitStageIndex = exitProbability > 0 ? parameters.StageNames.Count - 2 : -1;

        // G4: build INSIDE the try so a topology-level refusal (the fitted
        // p_exit = 1.0 Constructor OutOfRange) becomes a banner, never an
        // unhandled exception.
        try
        {
            var topology = new NetworkTopology(arrivalRate.Value, stageResult.Specs!, exitStageIndex, exitProbability);

            var traceLevel = TraceLevelFromName(parameters.TraceLevelName);
            var sink = new CollectionTraceSink(traceLevel);
            var engine = new Engine(new SeededRandomSource(), Log.Logger);

            SimulationResult result = parameters.RunMode == RunMode.DiagnosticTrace
                ? engine.Run(topology, parameters.Seed, parameters.HorizonMinutes, sink)
                : engine.Run(topology, new ClinicCalendar(startDayOfWeek: parameters.StartDay),
                    parameters.GeneratorDays, parameters.Seed, parameters.DailyCap, sink);

            status?.Invoke(string.Empty);
            return new RunOutcome(result, fits, sink.Lines, exitProbability, null);
        }
        catch (UnstableSystemException ex)
        {
            Log.Warning(ex, "Run refused: unstable configuration");
            return Refused(fits, exitProbability, ex.Message);
        }
        catch (ArgumentOutOfRangeException ex) when (string.Equals(ex.ParamName, "exitProbability", StringComparison.Ordinal))
        {
            Log.Warning(ex, "Run refused: exit probability out of the [0, 1) contract");
            return Refused(fits, exitProbability, ex.Message);
        }
    }

    private static IReadOnlyList<FitReport> BuildFits(SimulationParameters parameters, DataBindingResult? binding, double significanceLevel)
    {
        if (binding is null || !binding.IsUsable || binding.FittedArrivalRate is null)
        {
            return Array.Empty<FitReport>();
        }

        var reports = new List<FitReport>
        {
            FitsService.Fit("Inter-arrival", binding.InterArrivalMinutes, parameters.InterArrivalDistribution, significanceLevel),
        };

        foreach (var stage in binding.StageNames)
        {
            if (binding.ServiceMinutesByStage.TryGetValue(stage, out var times))
            {
                reports.Add(FitsService.Fit($"{stage} service", times, parameters.ServiceDistribution, significanceLevel));
            }
        }

        return reports;
    }

    /// <summary>
    /// Builds the ordered stage specifications, replacing a stage whose manual
    /// μ is blank with the data-fitted rate for that stage name (D-100). Null
    /// when any stage has neither manual nor fitted μ — the run cannot be
    /// configured (5d.1: the banner names the first offending stage).
    /// </summary>
    private static (List<StageSpec>? Specs, string? MissingStageName) BuildStageSpecs(SimulationParameters parameters, DataBindingResult? binding)
    {
        string? missingFor = null;
        var specs = new List<StageSpec>(parameters.StageNames.Count);
        for (int i = 0; i < parameters.StageNames.Count; i++)
        {
            string name = parameters.StageNames[i];
            double mu = parameters.ManualServiceRates[i] ?? FittedRateFor(name, binding) ?? double.NaN;
            if (!(mu > 0))
            {
                missingFor ??= name;
                continue;
            }

            specs.Add(new StageSpec(name, parameters.ServerCounts[i], mu));
        }

        if (specs.Count != parameters.StageNames.Count)
        {
            Log.Warning("Run refused: stage '{}' has no service rate (manual or fitted)", missingFor);
            return (null, missingFor);
        }

        return (specs, null);
    }

    private static double? FittedRateFor(string stageName, DataBindingResult? binding)
    {
        if (binding is null)
        {
            return null;
        }

        int index = Array.IndexOf(binding.StageNames.ToArray(), stageName);
        if (index < 0 || index >= binding.FittedServiceRates.Count)
        {
            return null;
        }

        double rate = binding.FittedServiceRates[index];
        return double.IsFinite(rate) && rate > 0 ? rate : null;
    }

    private static RunOutcome Refused(IReadOnlyList<FitReport> fits, double exitProbability, string message)
        => new(null, fits, Array.Empty<string>(), exitProbability, message);

    /// <summary>
    /// The exit probability the run uses: manual override, else the data-fitted
    /// value, else the default (D-104).
    /// </summary>
    internal static double ResolveExitProbability(SimulationParameters parameters, DataBindingResult? binding)
    {
        if (parameters.PExitOverride is { } manual)
        {
            return manual;
        }

        if (binding?.FittedExitProbability is { } fitted)
        {
            return fitted;
        }

        return DefaultExitProbability;
    }

    internal static TraceLevel TraceLevelFromName(string name)
        => name.Trim().ToUpperInvariant() switch
        {
            "MINIMAL" => TraceLevel.Minimal,
            "STANDARD" => TraceLevel.Standard,
            "DETAILED" => TraceLevel.Detailed,
            "DEBUG" => TraceLevel.Debug,
            _ => TraceLevel.Minimal,
        };
}