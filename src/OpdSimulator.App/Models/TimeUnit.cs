namespace OpdSimulator.App.Models;

/// <summary>
/// The unit in which the user enters rates (λ, μ) or means
/// (1/λ, 1/μ). The engine internally works in minutes; values
/// are converted at parameter-build time.
/// </summary>
public enum TimeUnit
{
    Minutes,
    Seconds,
    Hours,
}