namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Wire representation of an artifact identity without its payload bytes.</summary>
public sealed record MicroBundleArtifactAddressDto(
    ulong BundleId,
    string Version,
    string ContentHash);
