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
/// before the topology is built, because the fitted value is known. That
/// refusal is conditional on the network having a downstream stage: a
/// single-stage network has none, so a fitted p_exit = 1.0 is correct there
/// and the run proceeds with p_exit normalised to 0 (Phase 8E, D-139).
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

        // Exit routing only means something when there IS a downstream stage to
        // reach. In a single-stage network every patient leaves after the only
        // stage, so a fitted p_exit = 1.0 is the correct (and only possible)
        // behaviour and must not be refused (Phase 8E, D-139).
        bool hasDownstreamStage = parameters.StageNames.Count >= 2;

        if (exitProbability >= 1.0 && hasDownstreamStage)
        {
            // Only a fitted value can reach 1.0 here: manual overrides are
            // config-validated to [0, 1). Surface the specific "no downstream
            // route" meaning instead of the raw Core message (5-F).
            return Refused(fits, exitProbability, FittedPExitEqualsOneMessage);
        }

        // For a single-stage network the exit probability is a routing no-op.
        // NetworkTopology requires exitProbability ∈ [0, 1) and only applies the
        // exit route when it is > 0, so normalising to 0 leaves every patient
        // exiting after the sole stage — identical routing, no refusal.
        double routingExitProbability = hasDownstreamStage ? exitProbability : 0.0;

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

        int exitStageIndex = routingExitProbability > 0 ? parameters.StageNames.Count - 2 : -1;

        // G4: build INSIDE the try so a topology-level refusal (the fitted
        // p_exit = 1.0 Constructor OutOfRange) becomes a banner, never an
        // unhandled exception.
        try
        {
            var topology = new NetworkTopology(arrivalRate.Value, stageResult.Specs!, exitStageIndex, routingExitProbability);

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
                // Phase 8J: test the samples against the family that stage was actually
                // configured with, instead of one family for the whole run. Looked up by
                // name because the binding's stage list is the data's, which need not be
                // the same length or order as the configured one; an unmatched stage
                // falls back to the first configured family, which is what every stage
                // was tested against before 8J.
                reports.Add(FitsService.Fit(
                    $"{stage} service",
                    times,
                    FamilyNameFor(parameters, stage),
                    significanceLevel));
            }
        }

        return reports;
    }

    /// <summary>
    /// The configured service family name for a stage, matched by stage name.
    /// </summary>
    private static string FamilyNameFor(SimulationParameters parameters, string stageName)
    {
        for (int i = 0; i < parameters.StageNames.Count && i < parameters.ServiceFamilies.Count; i++)
        {
            if (string.Equals(parameters.StageNames[i], stageName, StringComparison.OrdinalIgnoreCase))
                return parameters.ServiceFamilies[i].Family.ToString();
        }

        return parameters.ServiceDistribution;
    }

    /// <summary>
    /// Builds the ordered stage specifications, replacing a stage whose manual
    /// μ is blank with the data-fitted rate for that stage name (D-100). Null
    /// when any stage has neither manual nor fitted μ — the run cannot be
    /// configured (5d.1: the banner names the first offending stage).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// If the per-stage service lists do not line up with <c>StageNames</c> (D-147).
    /// This is a misconfiguration rather than a user error, so it is surfaced loudly
    /// instead of being papered over by padding or truncating a list.
    /// </exception>
    private static (List<StageSpec>? Specs, string? MissingStageName) BuildStageSpecs(SimulationParameters parameters, DataBindingResult? binding)
    {
        int stageCount = parameters.StageNames.Count;
        RequirePerStageList("ServiceFamilies", stageCount, parameters.ServiceFamilies.Count, parameters);
        RequirePerStageList("ServiceRates", stageCount, parameters.ServiceRates.Count, parameters);

        string? missingFor = null;
        var specs = new List<StageSpec>(stageCount);
        for (int i = 0; i < stageCount; i++)
        {
            string name = parameters.StageNames[i];

            // ServiceRates is the as-entered μ (Phase 8J); ManualServiceRates is the
            // mode-precedence input and FittedRateFor the data fallback. All three
            // normally agree on the first non-null, so the resolved value is the same
            // one the pre-8J code computed.
            double mu = parameters.ServiceRates[i]
                ?? parameters.ManualServiceRates[i]
                ?? FittedRateFor(name, binding)
                ?? double.NaN;
            if (!(mu > 0))
            {
                missingFor ??= name;
                continue;
            }

            // The family comes from the per-stage spec; the mean is re-derived from the
            // μ that actually won, so ServiceRate and ServiceDistribution.Mean are
            // consistent by construction. Inverting Mean to recover μ is deliberately
            // NOT done — that round trip is not bit-reversible (D-146 caveat 1).
            var family = parameters.ServiceFamilies[i].Family;
            specs.Add(new StageSpec(
                name,
                parameters.ServerCounts[i],
                mu,
                new DistributionSpec(family, Mean: 1.0 / mu)));
        }

        if (specs.Count != stageCount)
        {
            Log.Warning("Run refused: stage '{}' has no service rate (manual or fitted)", missingFor);
            return (null, missingFor);
        }

        return (specs, null);
    }

    /// <summary>
    /// Fails fast when a per-stage list does not have one entry per stage.
    /// </summary>
    /// <remarks>
    /// Without this, a short list would be indexed out of range mid-run and a long one
    /// would silently drop stages — a misconfiguration that produced a plausible but
    /// wrong network is worse than a refusal (AGENTS §12.4, fail loud).
    /// </remarks>
    private static void RequirePerStageList(string listName, int stageCount, int actualCount, SimulationParameters parameters)
    {
        if (actualCount == stageCount)
            return;

        Log.Error(
            "SimulationParameters is misconfigured: {StageCount} stage name(s) but {ListName} has {ActualCount} entr(ies)",
            stageCount, listName, actualCount);

        throw new ArgumentException(
            $"SimulationParameters is misconfigured: {stageCount} stage name(s) " +
            $"('{string.Join(", ", parameters.StageNames)}') but {listName} has " +
            $"{actualCount} entr(ies). The per-stage service lists must have exactly one " +
            "entry per stage.",
            nameof(parameters));
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