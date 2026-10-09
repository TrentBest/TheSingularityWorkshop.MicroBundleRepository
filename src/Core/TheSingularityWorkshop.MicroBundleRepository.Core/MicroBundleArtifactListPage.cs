namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>A page of artifact identities and an optional continuation token.</summary>
/// <param name="Items">The immutable artifact identities in this page.</param>
/// <param name="ContinuationToken">Opaque token for the next page, or null when enumeration is complete.</param>
public sealed record MicroBundleArtifactListPage(
    IReadOnlyList<MicroBundleArtifactAddress> Items,
    string? ContinuationToken);
