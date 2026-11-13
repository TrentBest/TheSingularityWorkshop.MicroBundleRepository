namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// One identity-only page of MicroBundle artifacts. A non-null continuation token
/// indicates that the adapter may have more results, including when Items is empty.
/// </summary>
public sealed record MicroBundleArtifactListPage
{
    public MicroBundleArtifactListPage(
        IReadOnlyList<MicroBundleArtifactAddress> items,
        string? continuationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count > MicroBundleArtifactListRequest.MaximumPageSize)
            throw new ArgumentException("A page cannot exceed the maximum supported page size.", nameof(items));

        Items = Array.AsReadOnly(items.ToArray());
        ContinuationToken = continuationToken;
    }

    public IReadOnlyList<MicroBundleArtifactAddress> Items { get; }
    public string? ContinuationToken { get; }
}
