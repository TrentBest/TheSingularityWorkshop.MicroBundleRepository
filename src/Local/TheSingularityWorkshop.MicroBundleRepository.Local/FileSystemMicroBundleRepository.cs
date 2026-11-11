using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Local;

/// <summary>
/// Local repository with a persistent ontology-to-local-handle index.
/// The numeric handle is an implementation detail and never leaves this adapter.
/// </summary>
public sealed class FileSystemMicroBundleRepository : IMicroBundleRepository, IMicroBundleRepositoryObserver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _root;
    private readonly string _indexRoot;
    private readonly SemaphoreSlim _indexGate = new(1, 1);

    public FileSystemMicroBundleRepository(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _indexRoot = Path.Combine(_root, ".ontology-index");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_indexRoot);
    }

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        var handle = await ResolveHandleAsync(address.Ontology, create: false, cancellationToken);
        if (handle is null)
            return null;

        var path = GetPath(handle.Value, address);
        if (!File.Exists(path))
            return null;

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new MicroBundleArtifact(address, content);
    }

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var handle = await ResolveHandleAsync(artifact.Address.Ontology, create: true, cancellationToken);
        var path = GetPath(handle!.Value, artifact.Address);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temporary, artifact.Content.ToArray(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public async ValueTask<IReadOnlyList<MicroBundleArtifactObservation>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<MicroBundleArtifactObservation>();
        foreach (var indexFile in Directory.EnumerateFiles(_indexRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = await ReadMappingAsync(indexFile, cancellationToken);
            var handleRoot = Path.Combine(_root, mapping.Handle.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!Directory.Exists(handleRoot))
                continue;

            foreach (var file in Directory.EnumerateFiles(handleRoot, "*.bundle", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(handleRoot, file);
                var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (segments.Length != 2 || string.IsNullOrWhiteSpace(segments[0]))
                    continue;

                var hash = Path.GetFileNameWithoutExtension(segments[1]);
                if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
                    continue;

                var info = new FileInfo(file);
                results.Add(new MicroBundleArtifactObservation(
                    new MicroBundleArtifactAddress(mapping.Ontology, segments[0], hash),
                    info.Length,
                    info.LastWriteTimeUtc));
            }
        }

        return results.OrderBy(x => x.Address.Ontology, StringComparer.Ordinal)
            .ThenBy(x => x.Address.Version, StringComparer.Ordinal)
            .ThenBy(x => x.Address.ContentHash, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<ulong?> ResolveHandleAsync(
        string ontology,
        bool create,
        CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ontology)))
            .ToLowerInvariant();
        var mappingPath = Path.Combine(_indexRoot, key + ".json");

        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(mappingPath))
            {
                var existing = await ReadMappingAsync(mappingPath, cancellationToken);
                if (!string.Equals(existing.Ontology, ontology, StringComparison.Ordinal))
                    throw new InvalidDataException("Ontology index hash collision detected.");
                return existing.Handle;
            }

            if (!create)
                return null;

            var handle = CreateHandle();
            var mapping = new OntologyHandleMapping(ontology, handle);
            var temporary = mappingPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(mapping, JsonOptions), cancellationToken);
                File.Move(temporary, mappingPath, overwrite: false);
                return handle;
            }
            catch (IOException) when (File.Exists(mappingPath))
            {
                var winner = await ReadMappingAsync(mappingPath, cancellationToken);
                if (!string.Equals(winner.Ontology, ontology, StringComparison.Ordinal))
                    throw new InvalidDataException("Ontology index hash collision detected.");
                return winner.Handle;
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
        finally
        {
            _indexGate.Release();
        }
    }

    private static async ValueTask<OntologyHandleMapping> ReadMappingAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<OntologyHandleMapping>(json, JsonOptions)
            ?? throw new InvalidDataException($"Ontology mapping '{path}' is empty.");
    }

    private static ulong CreateHandle()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        var handle = BitConverter.ToUInt64(bytes);
        return handle == 0 ? 1UL : handle;
    }

    private string GetPath(ulong handle, MicroBundleArtifactAddress address) =>
        Path.Combine(
            _root,
            handle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            address.Version,
            address.ContentHash + ".bundle");

    private sealed record OntologyHandleMapping(string Ontology, ulong Handle);
}
