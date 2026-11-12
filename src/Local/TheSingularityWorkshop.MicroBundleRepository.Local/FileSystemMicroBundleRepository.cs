using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Local;

/// <summary>
/// Local development repository that preserves the same artifact contract as durable storage.
/// It is an emulator of repository storage, not a second artifact model.
/// </summary>
public sealed class FileSystemMicroBundleRepository : IMicroBundleIdentityRepository, IMicroBundleRepositoryObserver
{
    private readonly string _root;
    private readonly FileSystemMicroBundleIdentityRepository _identityRepository;

    public FileSystemMicroBundleRepository(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
        _identityRepository = new FileSystemMicroBundleIdentityRepository(_root);
    }

    public ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default) =>
        _identityRepository.GetAsync(address, cancellationToken);

    public ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default) =>
        _identityRepository.PutAsync(artifact, cancellationToken);

    public ValueTask<MicroBundleIdentityBinding> PutAsync(
        string ontology,
        string version,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default) =>
        _identityRepository.PutAsync(ontology, version, content, cancellationToken);

    public ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        MicroBundleArtifactIdentity identity,
        CancellationToken cancellationToken = default) =>
        _identityRepository.ResolveAsync(identity, cancellationToken);

    public ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        ulong bundleId,
        CancellationToken cancellationToken = default) =>
        _identityRepository.ResolveAsync(bundleId, cancellationToken);

    public ValueTask<IReadOnlyList<MicroBundleArtifactObservation>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<MicroBundleArtifactObservation>();
        foreach (var file in Directory.EnumerateFiles(_root, "*.bundle", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(_root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (segments.Length != 3 ||
                !ulong.TryParse(segments[0], out var bundleId) ||
                string.IsNullOrWhiteSpace(segments[1]))
                continue;

            var hash = Path.GetFileNameWithoutExtension(segments[2]);
            if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
                continue;

            var info = new FileInfo(file);
            results.Add(new MicroBundleArtifactObservation(
                new MicroBundleArtifactAddress(bundleId, segments[1], hash),
                info.Length,
                info.LastWriteTimeUtc));
        }

        return ValueTask.FromResult<IReadOnlyList<MicroBundleArtifactObservation>>(
            results.OrderBy(x => x.Address.BundleId)
                .ThenBy(x => x.Address.Version)
                .ThenBy(x => x.Address.ContentHash)
                .ToArray());
    }

    private string GetPath(MicroBundleArtifactAddress address) =>
        Path.Combine(
            _root,
            address.BundleId.ToString(),
            address.Version,
            address.ContentHash + ".bundle");
}
