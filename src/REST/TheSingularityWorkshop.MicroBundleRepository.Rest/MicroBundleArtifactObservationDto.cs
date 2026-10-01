namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

internal sealed record MicroBundleArtifactObservationDto(
    ulong BundleId,
    string Version,
    string ContentHash,
    long ContentLength,
    DateTimeOffset? LastModified);
