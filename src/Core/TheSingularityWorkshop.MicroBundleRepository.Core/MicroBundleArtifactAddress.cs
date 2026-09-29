namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable identity for one versioned MicroBundle artifact.
/// </summary>
public readonly record struct MicroBundleArtifactAddress
{
    public MicroBundleArtifactAddress(ulong bundleId, string version, string contentHash)
    {
        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 value.", nameof(contentHash));

        BundleId = bundleId;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    public ulong BundleId { get; }
    public string Version { get; }
    public string ContentHash { get; }

    public override string ToString() => $"{BundleId}/{Version}/{ContentHash}";
}
