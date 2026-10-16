namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Wire representation of one page of discoverable artifact identities.</summary>
public sealed record MicroBundleArtifactListDto(
    IReadOnlyList<MicroBundleArtifactAddressDto> Items,
    string? ContinuationToken);
