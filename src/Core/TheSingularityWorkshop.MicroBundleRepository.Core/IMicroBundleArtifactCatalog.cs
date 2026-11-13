namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Provides bounded, identity-only artifact discovery independently of operational observation.
/// </summary>
public interface IMicroBundleArtifactCatalog
{
    /// <summary>
    /// Lists one bounded page. Continuation tokens are opaque and must be passed back
    /// unchanged to the same adapter; they do not promise snapshot isolation.
    /// </summary>
    ValueTask<MicroBundleArtifactListPage> ListPageAsync(
        MicroBundleArtifactListRequest request,
        CancellationToken cancellationToken = default);
}
