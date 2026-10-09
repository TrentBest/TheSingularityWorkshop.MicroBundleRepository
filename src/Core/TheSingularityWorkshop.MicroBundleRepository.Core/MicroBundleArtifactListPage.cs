namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>A page of artifact identities and an optional continuation token.</summary>
public sealed record MicroBundleArtifactListPage(
    IReadOnlyList<MicroBundleArtifactAddress> Items,
    string? ContinuationToken);
