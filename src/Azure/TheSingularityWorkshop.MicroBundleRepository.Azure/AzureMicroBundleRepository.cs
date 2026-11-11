using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Stores MicroBundle artifacts under repository-local numeric handles while exposing
/// only portable ontology/version/content identity to callers.
/// </summary>
public sealed class AzureMicroBundleRepository : IMicroBundleRepository, IMicroBundleRepositoryObserver
{
    private const string ArtifactPrefix = "artifacts";
    private const string OntologyIndexPrefix = "ontology-index";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly BlobContainerClient _container;

    public AzureMicroBundleRepository(AzureMicroBundleRepositoryOptions options)
        : this(CreateContainerClient(options))
    {
    }

    internal AzureMicroBundleRepository(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        var handle = await ResolveHandleAsync(address.Ontology, create: false, cancellationToken);
        if (handle is null)
            return null;

        var blob = _container.GetBlobClient(GetBlobName(handle.Value, address));
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

    public async ValueTask<IReadOnlyList<MicroBundleArtifactObservation>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var observations = new List<MicroBundleArtifactObservation>();
        await foreach (var blob in _container.GetBlobsAsync(
                           traits: BlobTraits.Metadata,
                           states: BlobStates.None,
                           prefix: ArtifactPrefix + "/",
                           cancellationToken: cancellationToken))
        {
            if (TryParseArtifactObservation(blob, out var observation))
                observations.Add(observation);
        }

        return observations
            .OrderBy(x => x.Address.Ontology, StringComparer.Ordinal)
            .ThenBy(x => x.Address.Version, StringComparer.Ordinal)
            .ThenBy(x => x.Address.ContentHash, StringComparer.Ordinal)
            .ToArray();
    }

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var handle = await ResolveHandleAsync(artifact.Address.Ontology, create: true, cancellationToken);
        var blob = _container.GetBlobClient(GetBlobName(handle!.Value, artifact.Address));
        using var stream = new MemoryStream(artifact.Content.ToArray(), writable: false);

        try
        {
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
                    Metadata = new Dictionary<string, string>
                    {
                        ["ontology"] = artifact.Address.Ontology,
                        ["repository-local-handle"] = handle.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["version"] = artifact.Address.Version,
                        ["content-sha256"] = artifact.Address.ContentHash
                    },
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                },
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Content-addressed writes are immutable. The requested artifact already exists.
        }
    }

    internal static string GetBlobName(ulong localHandle, MicroBundleArtifactAddress address) =>
        $"{ArtifactPrefix}/{localHandle}/{address.Version}/{address.ContentHash}.bundle";

    private async ValueTask<ulong?> ResolveHandleAsync(
        string ontology,
        bool create,
        CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ontology))).ToLowerInvariant();
        var mappingBlob = _container.GetBlobClient($"{OntologyIndexPrefix}/{key}.json");

        try
        {
            var existing = await ReadMappingAsync(mappingBlob, cancellationToken);
            if (!string.Equals(existing.Ontology, ontology, StringComparison.Ordinal))
                throw new InvalidDataException("Ontology index hash collision detected.");
            return existing.Handle;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            if (!create)
                return null;
        }

        var mapping = new OntologyHandleMapping(ontology, CreateHandle());
        try
        {
            await mappingBlob.UploadAsync(
                BinaryData.FromString(JsonSerializer.Serialize(mapping, JsonOptions)),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                },
                cancellationToken);
            return mapping.Handle;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            // Another writer created the canonical mapping first; use its handle.
            var winner = await ReadMappingAsync(mappingBlob, cancellationToken);
            if (!string.Equals(winner.Ontology, ontology, StringComparison.Ordinal))
                throw new InvalidDataException("Ontology index hash collision detected.");
            return winner.Handle;
        }
    }

    private static async Task<OntologyHandleMapping> ReadMappingAsync(
        BlobClient blob,
        CancellationToken cancellationToken)
    {
        var response = await blob.DownloadContentAsync(cancellationToken);
        return JsonSerializer.Deserialize<OntologyHandleMapping>(response.Value.Content.ToString(), JsonOptions)
            ?? throw new InvalidDataException("The stored ontology mapping is empty.");
    }

    private static ulong CreateHandle()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        var handle = BitConverter.ToUInt64(bytes);
        return handle == 0 ? 1UL : handle;
    }

    private static bool TryParseArtifactObservation(
        BlobItem blob,
        out MicroBundleArtifactObservation observation)
    {
        observation = default!;
        var parts = blob.Name.Split('/');
        if (parts.Length != 4 ||
            !string.Equals(parts[0], ArtifactPrefix, StringComparison.Ordinal) ||
            !ulong.TryParse(parts[1], out _) ||
            !parts[3].EndsWith(".bundle", StringComparison.Ordinal) ||
            blob.Metadata is null ||
            !blob.Metadata.TryGetValue("ontology", out var ontology))
            return false;

        var hash = parts[3][..^".bundle".Length];
        try
        {
            var address = new MicroBundleArtifactAddress(ontology, parts[2], hash);
            observation = new MicroBundleArtifactObservation(
                address,
                blob.Properties.ContentLength ?? 0,
                blob.Properties.LastModified);
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

        var service = new BlobServiceClient(options.StorageAccountUri, new DefaultAzureCredential());
        return service.GetBlobContainerClient(options.ContainerName);
    }

    private sealed record OntologyHandleMapping(string Ontology, ulong Handle);
}
