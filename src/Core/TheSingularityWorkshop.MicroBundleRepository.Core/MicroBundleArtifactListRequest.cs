namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A bounded query for discovering stored MicroBundle artifact identities.
/// </summary>
/// <param name="PageSize">Requested maximum number of identities, from 1 through 500.</param>
/// <param name="ContinuationToken">Opaque token returned by the previous page, if any.</param>
/// <param name="BundleId">Optional MicroBundle identity filter.</param>
/// <param name="Version">Optional exact version filter; requires <paramref name="BundleId"/>.</param>
public sealed record MicroBundleArtifactListRequest(
    int PageSize = 100,
    string? ContinuationToken = null,
    ulong? BundleId = null,
    string? Version = null)
{
    /// <summary>Validates this query before it reaches a storage or transport adapter.</summary>
    public void Validate()
    {
        if (PageSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page size must be between 1 and 500.");

        if (BundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(BundleId), "Bundle ID must be greater than zero.");

        if (Version is not null && string.IsNullOrWhiteSpace(Version))
            throw new ArgumentException("Version cannot be empty when supplied.", nameof(Version));

        if (Version is not null && BundleId is null)
            throw new ArgumentException("A version filter requires a bundle ID.", nameof(Version));
    }
}
