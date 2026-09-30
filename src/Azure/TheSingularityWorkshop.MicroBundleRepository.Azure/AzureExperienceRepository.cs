using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Stores published Experience artifacts as immutable Azure block blobs.
/// </summary>
public sealed class AzureExperienceRepository : IExperienceRepository
{
    private readonly BlobContainerClient _container;

    /// <summary>Creates an Experience repository from Azure configuration.</summary>
    public AzureExperienceRepository(AzureExperienceRepositoryOptions options)
        : this(CreateContainerClient(options))
    {
    }

    internal AzureExperienceRepository(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>Creates the configured container if it does not already exist.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    /// <inheritdoc />
    public async ValueTask<ExperienceArtifact?> GetAsync(
        ExperienceArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var blob = _container.GetBlobClient(address.BlobName);

        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            await using var stream = response.Value.Content;
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);

            return new ExperienceArtifact(address, memory.ToArray());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask PutAsync(
        ExperienceArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var blob = _container.GetBlobClient(artifact.Address.BlobName);

        using var stream = new MemoryStream(
            artifact.Content.ToArray(),
            writable: false);

        try
        {
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["experience-id"] = artifact.Address.ExperienceId.ToString(),
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
            // Content-addressed writes are immutable. The exact artifact
            // already exists, so the repository is already in the requested state.
        }
    }

    private static BlobContainerClient CreateContainerClient(
        AzureExperienceRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.StorageAccountUri is null)
            throw new ArgumentException(
                "Storage account URI is required.",
                nameof(options));

        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new ArgumentException(
                "Container name is required.",
                nameof(options));

        var service = new BlobServiceClient(
            options.StorageAccountUri,
            new DefaultAzureCredential());

        return service.GetBlobContainerClient(options.ContainerName);
    }
}
