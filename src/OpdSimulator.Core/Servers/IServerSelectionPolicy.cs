namespace OpdSimulator.Core.Servers;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Strategy that picks one idle server when multiple are available at a stage.
/// </summary>
/// <remarks>
/// Random selection among idle servers avoids the lowest-ID starvation bias
/// that was present in Milestone 1 (D-017). Two implementations exist:
/// <list type="bullet">
///   <item><see cref="RandomIdleSelection"/> — production default; picks uniformly
///   among idle servers using one <see cref="IRandomSource.NextDouble"/> draw
///   (only when ≥2 idle, so the single-server M1 metrics are preserved).</item>
///   <item><see cref="LowestIdSelection"/> — reproduces the latent M1 bug so a
///   negative regression test can prove the balance threshold catches it.</item>
/// </list>
/// </remarks>
public interface IServerSelectionPolicy
{
    /// <summary>
    /// Selects one idle server from the stage's server array.
    /// </summary>
    /// <param name="servers">The stage's servers; at least one is idle.</param>
    /// <param name="random">Random source for the uniform draw.</param>
    /// <returns>The selected idle server.</returns>
    Server SelectIdleServer(IReadOnlyList<Server> servers, IRandomSource random);
}