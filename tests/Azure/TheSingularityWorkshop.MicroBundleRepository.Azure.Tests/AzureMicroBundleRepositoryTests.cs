using System.Security.Cryptography;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleRepositoryTests
{
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Blob_name_is_deterministic_and_content_addressed()
    {
        var content = "magic bundle"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(300, "1.2.0", hash);

        var blobName = AzureMicroBundleRepository.GetBlobName(address);

        Assert.Equal($"artifacts/300/1.2.0/{hash}.bundle", blobName);
    }

    [Fact]
    public void Default_container_name_is_microbundles()
    {
        var options = new AzureMicroBundleRepositoryOptions
        {
            StorageAccountUri = new Uri("https://example.blob.core.windows.net")
        };

        Assert.Equal("microbundles", options.ContainerName);
    }

    [Fact]
    public async Task ListPage_uses_one_provider_page_and_forwards_opaque_continuation()
    {
        var container = new StubBlobContainerClient(
            new[]
            {
                Page("next-token",
                    Blob("artifacts/300/1.2.0/" + ValidHash + ".bundle")),
                Page(null,
                    Blob("artifacts/300/1.3.0/" + ValidHash + ".bundle"))
            });
        var repository = new AzureMicroBundleRepository(container);

        var result = await repository.ListPageAsync(
            new MicroBundleArtifactListRequest(pageSize: 7, continuationToken: "provider-token", bundleId: 300));

        Assert.Equal("artifacts/300/", container.Prefix);
        Assert.Equal("provider-token", container.Pageable!.ContinuationToken);
        Assert.Equal(7, container.Pageable.PageSizeHint);
        Assert.Single(result.Items);
        Assert.Equal("next-token", result.ContinuationToken);
        Assert.Equal(1, container.Pageable.PagesRequested);
    }

    [Fact]
    public async Task ListPage_applies_exact_version_filter_and_preserves_token_for_empty_page()
    {
        var container = new StubBlobContainerClient(
            new[]
            {
                Page("more",
                    Blob("artifacts/300/1.2.0/" + ValidHash + ".bundle"),
                    Blob("artifacts/300/1.3.0/" + ValidHash + ".bundle"))
            });
        var repository = new AzureMicroBundleRepository(container);

        var result = await repository.ListPageAsync(
            new MicroBundleArtifactListRequest(bundleId: 300, version: "9.9.9"));

        Assert.Empty(result.Items);
        Assert.Equal("more", result.ContinuationToken);
    }

    [Fact]
    public async Task ListPage_skips_malformed_and_overflowing_artifact_identities()
    {
        var container = new StubBlobContainerClient(
            new[]
            {
                Page(null,
                    Blob("artifacts/18446744073709551616/1.0.0/" + ValidHash + ".bundle"),
                    Blob("artifacts/300/1.0.0/not-a-hash.bundle"),
                    Blob("artifacts/300/1.0.0/" + ValidHash + ".bundle"))
            });
        var repository = new AzureMicroBundleRepository(container);

        var result = await repository.ListPageAsync(new MicroBundleArtifactListRequest());

        var address = Assert.Single(result.Items);
        Assert.Equal(300UL, address.BundleId);
        Assert.Equal("1.0.0", address.Version);
    }

    [Fact]
    public async Task ListPage_honors_cancellation_while_processing_provider_page()
    {
        var container = new StubBlobContainerClient(
            new[] { Page(null, Blob("artifacts/300/1.0.0/" + ValidHash + ".bundle")) });
        var repository = new AzureMicroBundleRepository(container);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await repository.ListPageAsync(new MicroBundleArtifactListRequest(), cancellation.Token));
    }

    private static BlobItem Blob(string name) =>
        BlobsModelFactory.BlobItem(
            name: name,
            properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: false,
                contentLength: 12,
                lastModified: DateTimeOffset.UnixEpoch));

    private static StubPage Page(string? continuationToken, params BlobItem[] values) =>
        new(values, continuationToken);

    private sealed class StubBlobContainerClient : BlobContainerClient
    {
        private readonly IReadOnlyList<StubPage> _pages;

        public StubBlobContainerClient(IReadOnlyList<StubPage> pages) => _pages = pages;

        public string? Prefix { get; private set; }
        public StubAsyncPageable? Pageable { get; private set; }

        public override AsyncPageable<BlobItem> GetBlobsAsync(
            BlobTraits traits,
            BlobStates states,
            string prefix,
            CancellationToken cancellationToken)
        {
            Prefix = prefix;
            Pageable = new StubAsyncPageable(_pages);
            return Pageable;
        }
    }

    private sealed class StubAsyncPageable : AsyncPageable<BlobItem>
    {
        private readonly IReadOnlyList<StubPage> _pages;

        public StubAsyncPageable(IReadOnlyList<StubPage> pages) => _pages = pages;

        public string? ContinuationToken { get; private set; }
        public int? PageSizeHint { get; private set; }
        public int PagesRequested { get; private set; }

        public override async IAsyncEnumerable<Page<BlobItem>> AsPages(
            string? continuationToken = null,
            int? pageSizeHint = null)
        {
            ContinuationToken = continuationToken;
            PageSizeHint = pageSizeHint;

            foreach (var page in _pages)
            {
                PagesRequested++;
                yield return page;
                await Task.Yield();
            }
        }
    }

    private sealed class StubPage : Page<BlobItem>
    {
        public StubPage(IReadOnlyList<BlobItem> values, string? continuationToken)
        {
            Values = values;
            ContinuationToken = continuationToken;
        }

        public override IReadOnlyList<BlobItem> Values { get; }
        public override string? ContinuationToken { get; }
        public override Response GetRawResponse() => null!;
    }
}
