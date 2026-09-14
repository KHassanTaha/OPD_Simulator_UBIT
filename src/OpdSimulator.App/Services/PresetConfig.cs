namespace OpdSimulator.App.Services;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Typed <c>config</c> section of a <see cref="Preset"/>. Field values are the
/// config panel's raw text so a save/load round-trip is lossless (D-083 —
/// <c>arrivalParameter</c> replaces the schema sketch's <c>manualLambda</c>
/// because the field is mode-aware: a rate under RateWise, a mean under
/// MeanWise). Unknown fields are kept via <see cref="Extra"/> so newer schema
/// fields survive a round-trip.
/// </summary>
public sealed class PresetConfig
{
    /// <summary>"rate" or "mean" — the parameter interpretation.</summary>
    [JsonPropertyName("parameterMode")]
    public string? ParameterMode { get; set; }

    /// <summary>Fitting family for inter-arrival times, e.g. "Exponential".</summary>
    [JsonPropertyName("interArrivalDistribution")]
    public string? InterArrivalDistribution { get; set; }

    /// <summary>Fitting family for service times.</summary>
    [JsonPropertyName("serviceDistribution")]
    public string? ServiceDistribution { get; set; }

    /// <summary>Raw arrival field text (λ per minute, or mean minutes under MeanWise).</summary>
    [JsonPropertyName("arrivalParameter")]
    public string? ArrivalParameter { get; set; }

    /// <summary>Per-stage server counts in clinic-flow order.</summary>
    [JsonPropertyName("servers")]
    public PresetStageNumbers? Servers { get; set; }

    /// <summary>Per-stage raw service-rate field text.</summary>
    [JsonPropertyName("serviceRates")]
    public PresetStageRates? ServiceRates { get; set; }

    /// <summary>Horizon: mode ("minutes"|"days") and raw value text.</summary>
    [JsonPropertyName("horizon")]
    public PresetHorizon? Horizon { get; set; }

    /// <summary>Weekday name of day block 0, e.g. "Monday".</summary>
    [JsonPropertyName("startDay")]
    public string? StartDay { get; set; }

    /// <summary>Raw daily-cap text, or null for unlimited.</summary>
    [JsonPropertyName("dailyCap")]
    public string? DailyCap { get; set; }

    /// <summary>Raw random-seed text.</summary>
    [JsonPropertyName("randomSeed")]
    public string? RandomSeed { get; set; }

    /// <summary>Raw p_exit override text, or null to derive it (data or default).</summary>
    [JsonPropertyName("pExitOverride")]
    public string? PExitOverride { get; set; }

    /// <summary>Trace level name: "None", "Events", "State" or "Rng".</summary>
    [JsonPropertyName("traceLevel")]
    public string? TraceLevel { get; set; }

    /// <summary>Stores unknown config fields (forward compatibility).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Per-stage integer counts (reception/screening/doctor) in flow order.</summary>
public sealed class PresetStageNumbers
{
    [JsonPropertyName("reception")]
    public int? Reception { get; set; }

    [JsonPropertyName("screening")]
    public int? Screening { get; set; }

    [JsonPropertyName("doctor")]
    public int? Doctor { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Per-stage raw service-rate field text in flow order.</summary>
public sealed class PresetStageRates
{
    [JsonPropertyName("reception")]
    public string? Reception { get; set; }

    [JsonPropertyName("screening")]
    public string? Screening { get; set; }

    [JsonPropertyName("doctor")]
    public string? Doctor { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Horizon mode + value pair.</summary>
public sealed class PresetHorizon
{
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Pure-UI preferences stored alongside the config (AGENTS §17.2).</summary>
public sealed class PresetView
{
    /// <summary>Widget keys shown in the results panel.</summary>
    [JsonPropertyName("visibleWidgets")]
    public IReadOnlyList<string>? VisibleWidgets { get; set; }

    /// <summary>Collapsible-section session keys currently collapsed.</summary>
    [JsonPropertyName("collapsedSections")]
    public IReadOnlyList<string>? CollapsedSections { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}