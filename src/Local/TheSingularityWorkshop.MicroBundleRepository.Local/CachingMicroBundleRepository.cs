using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Local;

/// <summary>
/// Persistent read-through cache over another MicroBundle repository.
/// The cache is keyed by the complete immutable artifact address, so versions and
/// content hashes never overwrite one another.
/// </summary>
public sealed class CachingMicroBundleRepository : IMicroBundleRepository
{
    private readonly FileSystemMicroBundleRepository _cache;
    private readonly IMicroBundleRepository _origin;

    public CachingMicroBundleRepository(string cacheRoot, IMicroBundleRepository origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheRoot);
        ArgumentNullException.ThrowIfNull(origin);

        _cache = new FileSystemMicroBundleRepository(cacheRoot);
        _origin = origin;
    }

    /// <summary>
    /// Returns a verified disk-cache hit, otherwise retrieves from the origin and
    /// persists the verified artifact for subsequent runs.
    /// </summary>
    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var cached = await _cache.GetAsync(address, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
                return cached;
        }
        catch (ArgumentException)
        {
            // A file at the expected content address whose bytes fail SHA-256
            // verification is not a cache hit. A successful origin read below
            // replaces it atomically through FileSystemMicroBundleRepository.
            cancellationToken.ThrowIfCancellationRequested();
        }

        var artifact = await _origin.GetAsync(address, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
            return null;

        // Never persist an origin response under a different identity.
        if (artifact.Address != address)
            throw new InvalidDataException(
                $"Repository returned {artifact.Address} for requested artifact {address}.");

        await _cache.PutAsync(artifact, cancellationToken).ConfigureAwait(false);
        return artifact;
    }

    /// <summary>
    /// Writes to the origin first, then warms the persistent local cache.
    /// A failed origin write must not make the artifact appear durably published.
    /// </summary>
    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        await _origin.PutAsync(artifact, cancellationToken).ConfigureAwait(false);
        await _cache.PutAsync(artifact, cancellationToken).ConfigureAwait(false);
    }
}
