namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable identity for one versioned MicroBundle artifact.
/// </summary>
public readonly record struct MicroBundleArtifactAddress
{
    /// <summary>
    /// Creates a complete content-addressed MicroBundle identity.
    /// </summary>
    public MicroBundleArtifactAddress(ulong bundleId, string version, string contentHash)
    {
        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (version.Contains('/') || version.Contains('\\'))
            throw new ArgumentException(
                "Version must be a single storage-safe path segment.",
                nameof(version));

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException(
                "Content hash must be a 64-character hexadecimal SHA-256 value.",
                nameof(contentHash));

        BundleId = bundleId;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    /// <summary>Gets the stable MicroBundle identity.</summary>
    public ulong BundleId { get; }

    /// <summary>Gets the explicit artifact version.</summary>
    public string Version { get; }

    /// <summary>Gets the lowercase SHA-256 content identity.</summary>
    public string ContentHash { get; }

    /// <summary>Returns the deterministic human-readable artifact address.</summary>
    public override string ToString() => $"{BundleId}/{Version}/{ContentHash}";
}
