using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Local;

/// <summary>
/// Filesystem-backed repository with durable, repository-local ontology revision bindings.
/// </summary>
/// <remarks>
/// Writers across processes serialize through an exclusive lock file. Artifact bytes are
/// durably flushed and atomically renamed before the binding index is atomically replaced.
/// Therefore an interrupted write can leave an unreferenced artifact, but cannot publish
/// a binding to bytes that were not committed first.
/// </remarks>
public sealed class FileSystemMicroBundleIdentityRepository : IMicroBundleIdentityRepository
{
    private const string IndexFileName = "identity-bindings.json";
    private const string LockFileName = "identity-bindings.lock";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _root;
    private readonly string _indexPath;
    private readonly string _lockPath;
    private readonly string _artifactRoot;
    private readonly Func<CancellationToken, Task>? _beforeIndexCommit;

    public FileSystemMicroBundleIdentityRepository(string root)
        : this(root, beforeIndexCommit: null)
    {
    }

    internal FileSystemMicroBundleIdentityRepository(
        string root,
        Func<CancellationToken, Task>? beforeIndexCommit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _artifactRoot = _root;
        _indexPath = Path.Combine(_root, IndexFileName);
        _lockPath = Path.Combine(_root, LockFileName);
        _beforeIndexCommit = beforeIndexCommit;
        Directory.CreateDirectory(_artifactRoot);
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

        await using var repositoryLock = await AcquireLockAsync(cancellationToken);
        var records = await ReadIndexAsync(cancellationToken);

        var existing = records.SingleOrDefault(record => Matches(record, identity));
        if (existing is not null)
        {
            var existingBinding = ToBinding(existing);
            var stored = await GetAsync(existingBinding.Address, cancellationToken);
            if (stored is null || !stored.Content.Span.SequenceEqual(content.Span))
                throw new InvalidDataException("The existing identity binding does not resolve to the expected verified artifact bytes.");

            return existingBinding;
        }

        var bundleId = AllocateUniqueId(records);
        var address = new MicroBundleArtifactAddress(bundleId, version, hash);
        var artifact = new MicroBundleArtifact(address, content);
        await WriteArtifactDurablyAsync(artifact, cancellationToken);

        if (_beforeIndexCommit is not null)
            await _beforeIndexCommit(cancellationToken);

        records.Add(new BindingRecord(ontology, version, hash, bundleId));
        await WriteIndexDurablyAsync(records, cancellationToken);

        return new MicroBundleIdentityBinding(identity, address);
    }

    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        MicroBundleArtifactIdentity identity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = await ReadIndexAsync(cancellationToken);
        var record = records.SingleOrDefault(item => Matches(item, identity));
        if (record is null)
            return null;

        var binding = ToBinding(record);
        await EnsureArtifactExistsAndVerifiesAsync(binding, cancellationToken);
        return binding;
    }

    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        ulong bundleId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bundleId == 0)
            return null;

        var records = await ReadIndexAsync(cancellationToken);
        var record = records.SingleOrDefault(item => item.BundleId == bundleId);
        if (record is null)
            return null;

        var binding = ToBinding(record);
        await EnsureArtifactExistsAndVerifiesAsync(binding, cancellationToken);
        return binding;
    }

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetArtifactPath(address);
        if (!File.Exists(path))
            return null;

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return new MicroBundleArtifact(address, bytes);
    }

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        await using var repositoryLock = await AcquireLockAsync(cancellationToken);
        var records = await ReadIndexAsync(cancellationToken);
        if (records.Any(record => record.BundleId == artifact.Address.BundleId))
            throw new InvalidOperationException(
                "A repository-local ID already bound to a portable ontology revision cannot be written through the unbound numeric API.");

        await WriteArtifactDurablyAsync(artifact, cancellationToken);
    }

    private async Task EnsureArtifactExistsAndVerifiesAsync(
        MicroBundleIdentityBinding binding,
        CancellationToken cancellationToken)
    {
        var artifact = await GetAsync(binding.Address, cancellationToken);
        if (artifact is null)
            throw new InvalidDataException("The identity index points to an artifact that is not present in storage.");
    }

    private async Task WriteArtifactDurablyAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken)
    {
        var path = GetArtifactPath(artifact.Address);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(artifact.Content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private async Task WriteIndexDurablyAsync(
        List<BindingRecord> records,
        CancellationToken cancellationToken)
    {
        var temporary = _indexPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, records, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, _indexPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private async Task<List<BindingRecord>> ReadIndexAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_indexPath))
            return [];

        await using var stream = new FileStream(
            _indexPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var records = await JsonSerializer.DeserializeAsync<List<BindingRecord>>(
            stream, JsonOptions, cancellationToken) ?? [];

        var identities = new HashSet<MicroBundleArtifactIdentity>();
        var ids = new HashSet<ulong>();
        foreach (var record in records)
        {
            var identity = new MicroBundleArtifactIdentity(
                record.Ontology, record.Version, record.ContentHash);
            if (record.BundleId == 0 ||
                !identities.Add(identity) ||
                !ids.Add(record.BundleId))
                throw new InvalidDataException("The persisted identity index contains an invalid or conflicting binding.");
        }

        return records;
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    options: FileOptions.WriteThrough);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
            }
        }
    }

    private string GetArtifactPath(MicroBundleArtifactAddress address) =>
        Path.Combine(
            _artifactRoot,
            address.BundleId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.Version,
            address.ContentHash + ".bundle");

    private ulong AllocateUniqueId(IReadOnlyCollection<BindingRecord> records)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        var occupied = records.Select(record => record.BundleId).ToHashSet();

        // Collision handling is an enforced uniqueness check under the cross-process
        // repository lock, including legacy numeric artifacts that predate this index.
        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var candidate = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
            var legacyArtifactDirectory = Path.Combine(
                _artifactRoot,
                candidate.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (candidate != 0 &&
                !occupied.Contains(candidate) &&
                !Directory.Exists(legacyArtifactDirectory))
                return candidate;
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

    private sealed record BindingRecord(
        string Ontology,
        string Version,
        string ContentHash,
        ulong BundleId);
}
