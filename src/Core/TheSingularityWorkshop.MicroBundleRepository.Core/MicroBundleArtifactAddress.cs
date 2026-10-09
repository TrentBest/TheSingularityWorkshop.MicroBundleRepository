namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable identity for one versioned MicroBundle artifact.
/// </summary>
/// <remarks>
/// The content hash is normalized to lowercase. A complete address identifies exact bytes,
/// not a mutable "latest" version or publication pointer.
/// </remarks>
public readonly record struct MicroBundleArtifactAddress
{
    /// <summary>Creates a validated immutable artifact address.</summary>
    /// <param name="bundleId">Stable, non-zero MicroBundle ID.</param>
    /// <param name="version">Explicit version represented as one storage-safe path segment.</param>
    /// <param name="contentHash">64-character hexadecimal SHA-256 content hash.</param>
    /// <exception cref="ArgumentOutOfRangeException">The bundle ID is zero.</exception>
    /// <exception cref="ArgumentException">The version or content hash is invalid.</exception>
    public MicroBundleArtifactAddress(ulong bundleId, string version, string contentHash)
    {
        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (version.Contains('/') || version.Contains('\\'))
            throw new ArgumentException("Version must be a single storage-safe path segment.", nameof(version));

        if (contentHash.Length != 64 || !contentHash.All(Uri.IsHexDigit))
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 value.", nameof(contentHash));

        BundleId = bundleId;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    /// <summary>Gets the stable MicroBundle ID.</summary>
    public ulong BundleId { get; }

    /// <summary>Gets the explicit artifact version.</summary>
    public string Version { get; }

    /// <summary>Gets the lowercase SHA-256 content identity.</summary>
    public string ContentHash { get; }

    /// <summary>Returns the compact slash-delimited artifact address.</summary>
    public override string ToString() => $"{BundleId}/{Version}/{ContentHash}";
}
