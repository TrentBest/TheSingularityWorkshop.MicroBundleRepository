namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Wire representation of one immutable MicroBundle artifact.</summary>
public sealed record MicroBundleArtifactDto(
    ulong BundleId,
    string Version,
    string ContentHash,
    string ContentBase64);
