namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Identity-only page returned by bounded artifact discovery.</summary>
public sealed record MicroBundleArtifactListPageDto(
    IReadOnlyList<MicroBundleArtifactIdentityDto> Items,
    string? ContinuationToken);

/// <summary>Stable identity of one stored MicroBundle artifact.</summary>
public sealed record MicroBundleArtifactIdentityDto(
    ulong BundleId,
    string Version,
    string ContentHash);
