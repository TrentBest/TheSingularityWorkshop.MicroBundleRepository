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
    private readonly long _maxArtifactBytes;

    /// <summary>Creates an Experience repository from Azure configuration.</summary>
    public AzureExperienceRepository(AzureExperienceRepositoryOptions options)
        : this(CreateContainerClient(options), options.MaxArtifactBytes)
    {
    }

    internal AzureExperienceRepository(BlobContainerClient container)
        : this(container, 10 * 1024 * 1024)
    {
    }

    internal AzureExperienceRepository(BlobContainerClient container, long maxArtifactBytes)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        if (maxArtifactBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxArtifactBytes), "The artifact read limit must be greater than zero.");

        _maxArtifactBytes = maxArtifactBytes;
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

            // Reject a known oversized blob before allocating a buffer. The copy
            // loop below also enforces the limit if the stream exceeds its declared
            // content length or the length is not available.
            var contentLength = response.Value.Details.ContentLength;
            if (contentLength > _maxArtifactBytes)
                throw new ExperienceArtifactTooLargeException(_maxArtifactBytes);

            using var memory = new MemoryStream(
                contentLength is >= 0 and <= int.MaxValue
                    ? (int)Math.Min(Math.Min(contentLength.Value, _maxArtifactBytes), 1024 * 1024)
                    : 0);
            var buffer = new byte[81920];
            long totalBytes = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                    break;

                totalBytes += read;
                if (totalBytes > _maxArtifactBytes)
                    throw new ExperienceArtifactTooLargeException(_maxArtifactBytes);

                await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

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
        if (artifact.Content.Length > _maxArtifactBytes)
            throw new ExperienceArtifactTooLargeException(_maxArtifactBytes);

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

        if (options.MaxArtifactBytes <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The artifact read limit must be greater than zero.");

        var service = new BlobServiceClient(
            options.StorageAccountUri,
            new DefaultAzureCredential());

        return service.GetBlobContainerClient(options.ContainerName);
    }
}

/// <summary>Raised when an Experience artifact exceeds the configured storage limit.</summary>
public sealed class ExperienceArtifactTooLargeException : Exception
{
    /// <summary>Creates the exception for the configured byte limit.</summary>
    public ExperienceArtifactTooLargeException(long maxArtifactBytes)
        : base($"The Experience artifact exceeds the configured limit of {maxArtifactBytes} bytes.")
    {
        MaxArtifactBytes = maxArtifactBytes;
    }

    /// <summary>Gets the configured maximum artifact size in bytes.</summary>
    public long MaxArtifactBytes { get; }
}
