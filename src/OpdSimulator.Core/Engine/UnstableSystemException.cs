namespace OpdSimulator.Core.Engine;

/// <summary>
/// A single (stage, λᵢ, cᵢ, μᵢ, ρᵢ) entry describing one unstable stage.
/// </summary>
public sealed record UnstableStage(string StageName, double ArrivalRate, int ServerCount, double ServiceRate, double Rho);

/// <summary>
/// Thrown when a stage's traffic intensity ρ = λ/(c·μ) is ≥ 1, i.e. the queue
/// would grow without bound and analytical/simulated results are meaningless.
/// </summary>
/// <remarks>
/// Realisation of FR-VAL-1 (per-stage stability check). Carries the full
/// parameter set (λ, c, μ, ρ) of the first offending stage — and in the serial
/// network an aggregated message over ALL unstable stages — so the caller can
/// report exactly which stage is unstable and why.
/// </remarks>
public sealed class UnstableSystemException : Exception
{
    /// <summary>
    /// Creates the exception from the offending single-stage configuration.
    /// </summary>
    /// <param name="config">The configuration whose stage is unstable.</param>
    public UnstableSystemException(EngineConfig config)
        : this(new[] { new UnstableStage(config.StageName, config.ArrivalRate, config.ServerCount, config.ServiceRate, config.Rho) })
    {
    }

    /// <summary>
    /// Creates the exception from one or more unstable stages.
    /// </summary>
    /// <param name="stages">Every unstable stage, each with λᵢ, cᵢ, μᵢ and ρᵢ.</param>
    /// <exception cref="ArgumentException">If <paramref name="stages"/> is empty.</exception>
    public UnstableSystemException(IReadOnlyList<UnstableStage> stages)
        : base(BuildMessage(stages))
    {
        var first = stages[0];
        StageName = first.StageName;
        ArrivalRate = first.ArrivalRate;
        ServerCount = first.ServerCount;
        ServiceRate = first.ServiceRate;
        Rho = first.Rho;
    }

    /// <summary>Name of the first unstable stage.</summary>
    public string StageName { get; }

    /// <summary>Arrival rate λ that overloaded the first unstable stage.</summary>
    public double ArrivalRate { get; }

    /// <summary>Number of servers at the first unstable stage.</summary>
    public int ServerCount { get; }

    /// <summary>Service rate per server μ of the first unstable stage.</summary>
    public double ServiceRate { get; }

    /// <summary>The offending traffic intensity ρ = λ/(c·μ) of the first unstable stage.</summary>
    public double Rho { get; }

    private static string BuildMessage(IReadOnlyList<UnstableStage> stages)
    {
        if (stages.Count == 0)
            throw new ArgumentException("At least one unstable stage is required.", nameof(stages));

        string detail = string.Join("; ", stages.Select(s =>
            $"stage '{s.StageName}' is unstable — ρ = {s.Rho:F2} (≥ 1) with λ = {s.ArrivalRate:F3}, c = {s.ServerCount}, μ = {s.ServiceRate:F3}"));

        return stages.Count == 1
            ? $"{detail}. The queue would grow without bound; lower the arrival rate or add servers."
            : $"{detail}. The queues would grow without bound at these stages; lower the arrival rate or add servers.";
    }
}