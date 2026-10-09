using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Stores and discovers MicroBundle artifacts as deterministic Azure block blobs.
/// </summary>
public sealed class AzureMicroBundleRepository : IMicroBundleRepository
{
    private const string ArtifactPrefix = "artifacts";

    private readonly BlobContainerClient _container;

    public AzureMicroBundleRepository(AzureMicroBundleRepositoryOptions options)
        : this(CreateContainerClient(options))
    {
    }

    internal AzureMicroBundleRepository(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    /// Creates the configured private container if it does not already exist.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetBlobName(address));

        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            await using var stream = response.Value.Content;
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            return new MicroBundleArtifact(address, memory.ToArray());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var blob = _container.GetBlobClient(GetBlobName(artifact.Address));

        using var stream = new MemoryStream(artifact.Content.ToArray(), writable: false);

        try
        {
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/octet-stream"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["bundle-id"] = artifact.Address.BundleId.ToString(),
                        ["version"] = artifact.Address.Version,
                        ["content-sha256"] = artifact.Address.ContentHash
                    },
                    Conditions = new BlobRequestConditions
                    {
                        IfNoneMatch = ETag.All
                    }
                },
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Content-addressed writes are immutable. If the exact address already
            // exists, the repository is already in the requested state.
        }
    }

    public async ValueTask<MicroBundleArtifactListPage> ListAsync(
        MicroBundleArtifactListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();

        var prefix = request.BundleId is null
            ? $"{ArtifactPrefix}/"
            : request.Version is null
                ? $"{ArtifactPrefix}/{request.BundleId.Value}/"
                : $"{ArtifactPrefix}/{request.BundleId.Value}/{request.Version}/";

        var pages = _container
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, cancellationToken)
            .AsPages(request.ContinuationToken, request.PageSize);

        await foreach (var page in pages.WithCancellation(cancellationToken))
        {
            var addresses = new List<MicroBundleArtifactAddress>(page.Values.Count);
            foreach (var blob in page.Values)
            {
                if (TryParseAddress(blob.Name, out var address))
                    addresses.Add(address);
            }

            return new MicroBundleArtifactListPage(addresses, page.ContinuationToken);
        }

        return new MicroBundleArtifactListPage(Array.Empty<MicroBundleArtifactAddress>(), null);
    }

    internal static string GetBlobName(MicroBundleArtifactAddress address) =>
        $"{ArtifactPrefix}/{address.BundleId}/{address.Version}/{address.ContentHash}.bundle";

    internal static bool TryParseAddress(string blobName, out MicroBundleArtifactAddress address)
    {
        address = default;
        var segments = blobName.Split('/');
        if (segments.Length != 4 ||
            !string.Equals(segments[0], ArtifactPrefix, StringComparison.Ordinal) ||
            !segments[3].EndsWith(".bundle", StringComparison.Ordinal))
            return false;

        if (!ulong.TryParse(segments[1], out var bundleId))
            return false;

        var hash = segments[3][..^".bundle".Length];
        try
        {
            address = new MicroBundleArtifactAddress(bundleId, segments[2], hash);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static BlobContainerClient CreateContainerClient(AzureMicroBundleRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.StorageAccountUri is null)
            throw new ArgumentException("Storage account URI is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new ArgumentException("Container name is required.", nameof(options));

        var service = new BlobServiceClient(
            options.StorageAccountUri,
            new DefaultAzureCredential());

        return service.GetBlobContainerClient(options.ContainerName);
    }
}
