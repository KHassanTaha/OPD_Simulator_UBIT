namespace OpdSimulator.Core.Engine;

/// <summary>
/// A single (stage, λᵢ, cᵢ, μᵢ, ρᵢ) entry describing one unstable stage.
/// </summary>
/// <param name="StageName">Name of the stage.</param>
/// <param name="ArrivalRate">The λᵢ actually used, i.e. after any admitted-load cap.</param>
/// <param name="ServerCount">Number of parallel servers c at the stage.</param>
/// <param name="ServiceRate">Service rate per server μ at the stage.</param>
/// <param name="Rho">Traffic intensity ρᵢ = λᵢ/(cᵢ·μᵢ).</param>
/// <param name="OfferedArrivalRate">The λᵢ demand would place on the stage before the cap,
/// when an admitted-load cap applies (D-190). null when no cap is configured.</param>
/// <param name="CapRate">The cap-derived rate in patients per minute, when a cap applies (D-190).</param>
public sealed record UnstableStage(
    string StageName,
    double ArrivalRate,
    int ServerCount,
    double ServiceRate,
    double Rho,
    double? OfferedArrivalRate = null,
    double? CapRate = null)
{
    /// <summary>
    /// Whether the cap is what is holding this stage at the rate it was refused
    /// with — i.e. the cap is binding but not enough on its own (D-190).
    /// </summary>
    public bool IsCapLimited => CapRate is not null && OfferedArrivalRate is not null;
}

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
        {
            string base_ = $"stage '{s.StageName}' is unstable — ρ = {s.Rho:F2} (≥ 1) with λ = {s.ArrivalRate:F3}, c = {s.ServerCount}, μ = {s.ServiceRate:F3}";
            // When a cap is configured, say what demand asked for and what the cap
            // allowed. A user staring at λ = 0.515 needs to know it was 0.670 before
            // the cap, or the only explanation they can find is a wrong fit (D-190).
            if (s.IsCapLimited)
                return $"{base_} (fitted λ = {s.OfferedArrivalRate:F3} before the daily cap; the cap admitted up to {s.CapRate:F3}/min)";
            return base_;
        }));

        string remedy = stages.Count == 1
            ? "The queue would grow without bound; lower the arrival rate or add servers."
            : "The queues would grow without bound at these stages; lower the arrival rate or add servers.";

        return $"{detail}. {remedy}";
    }
}