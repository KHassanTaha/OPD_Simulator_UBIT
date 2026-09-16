namespace OpdSimulator.App.Models;

/// <summary>
/// How the Horizon section interprets a run (D-105). The clinic day modes use
/// the calendar-aware engine overload; <see cref="DiagnosticTrace"/> is the
/// only mode that records an event trace — the frozen Core emits trace events
/// solely on the plain minutes-horizon run.
/// </summary>
public enum RunMode
{
    /// <summary>One operating session (default): a single clinic calendar day.</summary>
    ClinicDay = 0,

    /// <summary>N consecutive operating days (Mon–Thu + Sat, 08:15–11:00 arrivals).</summary>
    MultiDay = 1,

    /// <summary>
    /// A bounded minutes-horizon run with the full per-event trace — the
    /// diagnostic surface for demonstrating DES correctness (D-105).
    /// </summary>
    DiagnosticTrace = 2,
}