using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Stores MicroBundle artifacts as deterministic Azure block blobs.
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
                }
            },
            cancellationToken);
    }

    internal static string GetBlobName(MicroBundleArtifactAddress address) =>
        $"{ArtifactPrefix}/{address.BundleId}/{address.Version}/{address.ContentHash}.bundle";

    private static BlobContainerClient CreateContainerClient(AzureMicroBundleRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.StorageAccountUri is null)
            throw new ArgumentException("Storage account URI is required.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new ArgumentException("Container name is required.", nameof(options));

        return new BlobContainerClient(
            options.StorageAccountUri,
            options.ContainerName,
            new DefaultAzureCredential());
    }
}
