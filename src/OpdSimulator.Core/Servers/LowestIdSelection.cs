namespace OpdSimulator.Core.Servers;

using OpdSimulator.Core.Distributions;

/// <summary>
/// Reproduces the lowest-ID server assignment from Milestone 1, where the
/// first idle server in index order was always picked (latent bug, D-017).
/// </summary>
/// <remarks>
/// <strong>Test-only.</strong> With multiple symmetric servers this policy
/// loads the low-index servers first, creating a measurable utilisation
/// imbalance (> 0.10 threshold) that the <see cref="RandomIdleSelection"/>
/// eliminates. The matching negative regression test
/// (<c>EngineTests.Run_NetworkSymmetricServers2_LowestIdPolicy_RecreatesImbalance</c>)
/// proves the threshold catches the latent bug.
/// </remarks>
public sealed class LowestIdSelection : IServerSelectionPolicy
{
    /// <inheritdoc />
    public Server SelectIdleServer(IReadOnlyList<Server> servers, IRandomSource random)
        => servers.First(s => !s.IsBusy);
}