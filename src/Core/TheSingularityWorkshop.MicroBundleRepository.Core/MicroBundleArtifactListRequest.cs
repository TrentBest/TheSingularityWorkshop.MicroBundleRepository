namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A bounded request for an identity-only page of stored MicroBundle artifacts.
/// Continuation tokens are opaque and belong to the storage adapter that issued them.
/// </summary>
public sealed record MicroBundleArtifactListRequest
{
    public const int DefaultPageSize = 100;
    public const int MaximumPageSize = 500;

    public MicroBundleArtifactListRequest(
        int pageSize = DefaultPageSize,
        string? continuationToken = null,
        ulong? bundleId = null,
        string? version = null)
    {
        if (pageSize is < 1 or > MaximumPageSize)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize), $"Page size must be between 1 and {MaximumPageSize}.");

        if (bundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(bundleId));

        if (version is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(version);
            if (bundleId is null)
                throw new ArgumentException("A version filter requires a BundleId.", nameof(version));
            if (version.Contains('/') || version.Contains('\\'))
                throw new ArgumentException("Version must be a single storage-safe path segment.", nameof(version));
        }

        if (continuationToken is not null && string.IsNullOrWhiteSpace(continuationToken))
            throw new ArgumentException("A continuation token cannot be empty or whitespace.", nameof(continuationToken));

        PageSize = pageSize;
        ContinuationToken = continuationToken;
        BundleId = bundleId;
        Version = version;
    }

    public int PageSize { get; }
    public string? ContinuationToken { get; }
    public ulong? BundleId { get; }
    public string? Version { get; }
}
