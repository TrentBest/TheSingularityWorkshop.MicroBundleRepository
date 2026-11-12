namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Repository-owned translation between portable ontology revision identities
/// and repository-local numeric artifact addresses.
/// </summary>
/// <remarks>
/// Implementations must persist a one-to-one mapping keyed by the complete
/// (ontology, version, SHA-256) tuple. Binding creation and artifact persistence
/// are one logical operation: a binding must never become visible unless the
/// corresponding verified artifact bytes have been durably committed.
/// </remarks>
public interface IMicroBundleIdentityRepository : IMicroBundleRepository
{
    /// <summary>
    /// Verifies and stores the bytes, then atomically binds their immutable identity
    /// to a non-zero repository-local numeric address.
    /// </summary>
    ValueTask<MicroBundleIdentityBinding> PutAsync(
        string ontology,
        string version,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a complete portable revision identity to its durable local binding.
    /// </summary>
    ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        MicroBundleArtifactIdentity identity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a repository-local numeric ID to its exact portable revision identity.
    /// Returns null when the ID has no binding.
    /// </summary>
    ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        ulong bundleId,
        CancellationToken cancellationToken = default);
}
