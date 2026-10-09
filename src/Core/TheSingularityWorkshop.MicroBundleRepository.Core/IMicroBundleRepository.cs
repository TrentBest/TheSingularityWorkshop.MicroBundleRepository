namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Deterministic artifact storage and retrieval boundary for MicroBundles.
/// </summary>
public interface IMicroBundleRepository
{
    /// <summary>
    /// Retrieves an artifact by its complete semantic/version/content address.
    /// </summary>
    ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an artifact at its deterministic content-addressed location.
    /// </summary>
    ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists immutable artifact identities without downloading artifact content.
    /// The continuation token is opaque and must be passed back unchanged.
    /// </summary>
    ValueTask<MicroBundleArtifactListPage> ListAsync(
        MicroBundleArtifactListRequest request,
        CancellationToken cancellationToken = default);
}
