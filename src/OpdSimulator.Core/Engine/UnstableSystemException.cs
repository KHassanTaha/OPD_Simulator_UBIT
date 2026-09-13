namespace OpdSimulator.Core.Engine;

/// <summary>
/// Thrown when a stage's traffic intensity ρ = λ/(c·μ) is ≥ 1, i.e. the queue
/// would grow without bound and analytical/simulated results are meaningless.
/// </summary>
/// <remarks>
/// Realisation of FR-VAL-1 (per-stage stability check). Carries the full
/// parameter set (λ, c, μ, ρ) so the caller can report exactly which stage is
/// unstable and why. In the serial network this list will grow to ALL unstable
/// stages; Milestone 1 simulates a single stage.
/// </remarks>
public sealed class UnstableSystemException : Exception
{
    /// <summary>
    /// Creates the exception from the offending configuration.
    /// </summary>
    /// <param name="config">The configuration whose stage is unstable.</param>
    public UnstableSystemException(EngineConfig config)
        : base(
            $"stage '{config.StageName}' is unstable — " +
            $"ρ = {config.Rho:F2} (≥ 1) with λ = {config.ArrivalRate:F3}, c = {config.ServerCount}, μ = {config.ServiceRate:F3}. " +
            "The queue would grow without bound; lower the arrival rate or add servers.")
    {
        StageName = config.StageName;
        ArrivalRate = config.ArrivalRate;
        ServerCount = config.ServerCount;
        ServiceRate = config.ServiceRate;
        Rho = config.Rho;
    }

    /// <summary>Name of the unstable stage.</summary>
    public string StageName { get; }

    /// <summary>Arrival rate λ that overloaded the stage.</summary>
    public double ArrivalRate { get; }

    /// <summary>Number of servers at the stage.</summary>
    public int ServerCount { get; }

    /// <summary>Service rate per server μ.</summary>
    public double ServiceRate { get; }

    /// <summary>The offending traffic intensity ρ = λ/(c·μ).</summary>
    public double Rho { get; }
}