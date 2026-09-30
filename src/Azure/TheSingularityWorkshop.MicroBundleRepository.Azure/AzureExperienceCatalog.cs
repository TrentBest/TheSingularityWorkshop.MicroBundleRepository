using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Stores Experience publication pointers as small Azure Blob JSON documents.
/// </summary>
public sealed class AzureExperienceCatalog : IExperienceCatalog
{
    private const string PublicationPrefix = "publications";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BlobContainerClient _container;

    public AzureExperienceCatalog(AzureExperienceRepositoryOptions options)
        : this(CreateContainerClient(options))
    {
    }

    internal AzureExperienceCatalog(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    /// <summary>
    /// Creates the configured private container if it does not already exist.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    public async ValueTask<ExperiencePublication?> GetPublishedAsync(
        ulong experienceId,
        CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetBlobName(experienceId));

        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return JsonSerializer.Deserialize<ExperiencePublication>(
                response.Value.Content.ToString(),
                JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async ValueTask PublishAsync(
        ExperiencePublication publication,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);

        var json = JsonSerializer.Serialize(publication, JsonOptions);
        using var stream = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(json),
            writable: false);

        var blob = _container.GetBlobClient(GetBlobName(publication.ExperienceId));

        await blob.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                }
            },
            cancellationToken);
    }

    public async ValueTask UnpublishAsync(
        ulong experienceId,
        CancellationToken cancellationToken = default)
    {
        var blob = _container.GetBlobClient(GetBlobName(experienceId));

        try
        {
            await blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already unpublished.
        }
    }

    internal static string GetBlobName(ulong experienceId) =>
        $"{PublicationPrefix}/{experienceId}.publication.json";

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
