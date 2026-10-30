namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A publication-time request for one exact MicroBundle ID and version.
/// </summary>
public readonly record struct MicroBundleVersionRequest
{
    public MicroBundleVersionRequest(ulong bundleId, string version)
    {
        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        BundleId = bundleId;
        Version = version;
    }

    public ulong BundleId { get; }
    public string Version { get; }
}
