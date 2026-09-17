namespace OpdSimulator.App.Models;

/// <summary>
/// How the simulation's arrival and service parameters are supplied (Phase 7C).
/// Exactly one path drives a run: fit the distributions from an uploaded data
/// file, or enter the arrival rate, per-stage service rates and p_exit by hand.
/// </summary>
public enum DataSourceMode
{
    /// <summary>Derive λ / μ / p_exit from an uploaded patient data file (default).</summary>
    FitFromData,

    /// <summary>Enter λ, per-stage μ and p_exit by hand; no data file is required.</summary>
    EnterManually,
}
