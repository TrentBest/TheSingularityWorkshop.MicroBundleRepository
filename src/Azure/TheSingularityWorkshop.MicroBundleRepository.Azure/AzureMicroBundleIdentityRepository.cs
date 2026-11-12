using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>
/// Azure Blob-backed repository with ETag-serialized durable revision identity bindings.
/// </summary>
/// <remarks>
/// Artifact blobs are committed immutably before the single identity-index blob is
/// conditionally replaced. Azure's If-Match/If-None-Match conditions serialize competing
/// writers across processes and hosts. A failed index commit can leave an unreferenced
/// artifact blob, but cannot expose a binding to an artifact that was not committed first.
/// </remarks>
public sealed class AzureMicroBundleIdentityRepository : IMicroBundleIdentityRepository
{
    private const string IndexBlobName = "identity/identity-bindings.json";
    private readonly AzureMicroBundleRepository _artifacts;
    private readonly BlobContainerClient _container;
    private readonly BlobClient _index;

    public AzureMicroBundleIdentityRepository(AzureMicroBundleRepositoryOptions options)
        : this(CreateContainer(options))
    {
    }

    internal AzureMicroBundleIdentityRepository(BlobContainerClient container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _artifacts = new AzureMicroBundleRepository(container);
        _index = container.GetBlobClient(IndexBlobName);
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _artifacts.InitializeAsync(cancellationToken);

    public ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default) =>
        _artifacts.GetAsync(address, cancellationToken);

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var records = await ReadIndexAsync(cancellationToken);
        if (records.Any(record => record.BundleId == artifact.Address.BundleId))
            throw new InvalidOperationException(
                "A repository-local ID already bound to an ontology revision cannot be written through the unbound numeric API.");

        await _artifacts.PutAsync(artifact, cancellationToken);
    }

    public async ValueTask<MicroBundleIdentityBinding> PutAsync(
        string ontology,
        string version,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var hash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        var identity = new MicroBundleArtifactIdentity(ontology, version, hash);
        var addresslessArtifactHash = hash;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await ReadIndexSnapshotAsync(cancellationToken);
            var existing = snapshot.Records.SingleOrDefault(record => Matches(record, identity));
            if (existing is not null)
            {
                var existingBinding = ToBinding(existing);
                var stored = await _artifacts.GetAsync(existingBinding.Address, cancellationToken);
                if (stored is null || !stored.Content.Span.SequenceEqual(content.Span))
                    throw new InvalidDataException("The Azure identity index points to missing or mismatched artifact bytes.");
                return existingBinding;
            }

            var bundleId = await AllocateUniqueIdAsync(snapshot.Records, cancellationToken);
            var address = new MicroBundleArtifactAddress(bundleId, version, addresslessArtifactHash);
            var artifact = new MicroBundleArtifact(address, content);
            await _artifacts.PutAsync(artifact, cancellationToken);

            var updated = new List<BindingRecord>(snapshot.Records)
            {
                new(ontology, version, hash, bundleId)
            };

            try
            {
                await WriteIndexAsync(updated, snapshot.ETag, snapshot.Exists, cancellationToken);
                return new MicroBundleIdentityBinding(identity, address);
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // Another writer changed/created the index. Re-read and retry with
                // its committed state; this attempt's artifact is safely orphaned.
            }
        }
    }

    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        MicroBundleArtifactIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await ReadIndexSnapshotAsync(cancellationToken);
        var record = snapshot.Records.SingleOrDefault(item => Matches(item, identity));
        if (record is null)
            return null;

        var binding = ToBinding(record);
        if (await _artifacts.GetAsync(binding.Address, cancellationToken) is null)
            throw new InvalidDataException("The Azure identity index points to a missing artifact.");
        return binding;
    }

    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        ulong bundleId,
        CancellationToken cancellationToken = default)
    {
        if (bundleId == 0)
            return null;

        var snapshot = await ReadIndexSnapshotAsync(cancellationToken);
        var record = snapshot.Records.SingleOrDefault(item => item.BundleId == bundleId);
        if (record is null)
            return null;

        var binding = ToBinding(record);
        if (await _artifacts.GetAsync(binding.Address, cancellationToken) is null)
            throw new InvalidDataException("The Azure identity index points to a missing artifact.");
        return binding;
    }

    private async Task<ulong> AllocateUniqueIdAsync(
        IReadOnlyCollection<BindingRecord> records,
        CancellationToken cancellationToken)
    {
        var occupied = records.Select(record => record.BundleId).ToHashSet();
        var bytes = new byte[sizeof(ulong)];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RandomNumberGenerator.Fill(bytes);
            var candidate = BitConverter.ToUInt64(bytes);
            if (candidate == 0 || occupied.Contains(candidate))
                continue;

            // Also protect numeric IDs already used by artifacts written before
            // identity indexing was introduced.
            await foreach (var _ in _container.GetBlobsAsync(
                traits: BlobTraits.None,
                states: BlobStates.None,
                prefix: $"artifacts/{candidate}/",
                cancellationToken: cancellationToken))
            {
                candidate = 0;
                break;
            }

            if (candidate != 0)
                return candidate;
        }
    }

    private async Task<IndexSnapshot> ReadIndexSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _index.DownloadContentAsync(cancellationToken);
            var records = response.Value.Content.ToObjectFromJson<List<BindingRecord>>() ?? [];
            ValidateIndex(records);
            return new IndexSnapshot(records, response.Value.Details.ETag, true);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new IndexSnapshot([], default, false);
        }
    }

    private async Task<List<BindingRecord>> ReadIndexAsync(CancellationToken cancellationToken) =>
        (await ReadIndexSnapshotAsync(cancellationToken)).Records;

    private async Task WriteIndexAsync(
        List<BindingRecord> records,
        ETag etag,
        bool exists,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(records));
        var conditions = exists
            ? new BlobRequestConditions { IfMatch = etag }
            : new BlobRequestConditions { IfNoneMatch = ETag.All };

        await _index.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                Conditions = conditions
            },
            cancellationToken);
    }

    private static void ValidateIndex(IReadOnlyCollection<BindingRecord> records)
    {
        var identities = new HashSet<MicroBundleArtifactIdentity>();
        var ids = new HashSet<ulong>();
        foreach (var record in records)
        {
            var identity = new MicroBundleArtifactIdentity(record.Ontology, record.Version, record.ContentHash);
            if (record.BundleId == 0 || !identities.Add(identity) || !ids.Add(record.BundleId))
                throw new InvalidDataException("The Azure identity index contains invalid or conflicting bindings.");
        }
    }

    private static bool Matches(BindingRecord record, MicroBundleArtifactIdentity identity) =>
        StringComparer.Ordinal.Equals(record.Ontology, identity.Ontology) &&
        StringComparer.Ordinal.Equals(record.Version, identity.Version) &&
        StringComparer.Ordinal.Equals(record.ContentHash, identity.ContentHash);

    private static MicroBundleIdentityBinding ToBinding(BindingRecord record)
    {
        var identity = new MicroBundleArtifactIdentity(record.Ontology, record.Version, record.ContentHash);
        var address = new MicroBundleArtifactAddress(record.BundleId, record.Version, record.ContentHash);
        return new MicroBundleIdentityBinding(identity, address);
    }

    private static BlobContainerClient CreateContainer(AzureMicroBundleRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.StorageAccountUri is null)
            throw new ArgumentException("Storage account URI is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new ArgumentException("Container name is required.", nameof(options));

        var service = new BlobServiceClient(options.StorageAccountUri, new Azure.Identity.DefaultAzureCredential());
        return service.GetBlobContainerClient(options.ContainerName);
    }

    private sealed record BindingRecord(string Ontology, string Version, string ContentHash, ulong BundleId);
    private sealed record IndexSnapshot(List<BindingRecord> Records, ETag ETag, bool Exists);
}
