using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Local;

/// <summary>
/// Local development repository that preserves the same artifact contract as durable storage.
/// It is an emulator of repository storage, not a second artifact model.
/// </summary>
public sealed class FileSystemMicroBundleRepository : IMicroBundleRepository, IMicroBundleRepositoryObserver, IMicroBundleArtifactCatalog
{
    private readonly string _root;

    public FileSystemMicroBundleRepository(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetPath(address);
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
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetPath(artifact.Address);
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
            // A cancelled or failed write must not leave temporary artifacts behind.
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

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

    /// <summary>
    /// Lists a deterministic page of artifact identities. The opaque cursor records the
    /// last returned identity; concurrent mutations are not snapshot-isolated.
    /// </summary>
    public ValueTask<MicroBundleArtifactListPage> ListPageAsync(
        MicroBundleArtifactListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var observations = new List<MicroBundleArtifactObservation>();
        foreach (var file in Directory.EnumerateFiles(_root, "*.bundle", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(_root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Length != 3 ||
                !ulong.TryParse(segments[0], out var bundleId) ||
                string.IsNullOrWhiteSpace(segments[1]))
                continue;

            var hash = Path.GetFileNameWithoutExtension(segments[2]);
            if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))
                continue;

            try
            {
                var address = new MicroBundleArtifactAddress(bundleId, segments[1], hash);
                if (request.BundleId is ulong requestedBundleId && address.BundleId != requestedBundleId)
                    continue;
                if (request.Version is not null &&
                    !string.Equals(address.Version, request.Version, StringComparison.Ordinal))
                    continue;

                var info = new FileInfo(file);
                observations.Add(new MicroBundleArtifactObservation(address, info.Length, info.LastWriteTimeUtc));
            }
            catch (ArgumentException)
            {
                // Ignore malformed identities just as the observer inventory does.
            }
        }

        var ordered = observations
            .Select(x => x.Address)
            .Distinct()
            .OrderBy(x => x.BundleId)
            .ThenBy(x => x.Version, StringComparer.Ordinal)
            .ThenBy(x => x.ContentHash, StringComparer.Ordinal);

        if (request.ContinuationToken is not null)
        {
            var cursor = DecodeCursor(request.ContinuationToken);
            ordered = ordered.Where(x => Compare(x, cursor) > 0)
                .OrderBy(x => x.BundleId)
                .ThenBy(x => x.Version, StringComparer.Ordinal)
                .ThenBy(x => x.ContentHash, StringComparer.Ordinal);
        }

        var pageItems = ordered.Take(request.PageSize + 1).ToArray();
        var hasMore = pageItems.Length > request.PageSize;
        var items = pageItems.Take(request.PageSize).ToArray();
        var continuation = hasMore ? EncodeCursor(items[^1]) : null;

        return ValueTask.FromResult(new MicroBundleArtifactListPage(items, continuation));
    }

    private static string EncodeCursor(MicroBundleArtifactAddress address) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(
            address.BundleId, address.Version, address.ContentHash)));

    private static MicroBundleArtifactAddress DecodeCursor(string token)
    {
        try
        {
            var cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(token))
                ?? throw new FormatException();
            return new MicroBundleArtifactAddress(cursor.BundleId, cursor.Version, cursor.ContentHash);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new ArgumentException("The local artifact continuation token is invalid.", nameof(token), ex);
        }
    }

    private static int Compare(MicroBundleArtifactAddress left, MicroBundleArtifactAddress right)
    {
        var result = left.BundleId.CompareTo(right.BundleId);
        if (result != 0) return result;
        result = StringComparer.Ordinal.Compare(left.Version, right.Version);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left.ContentHash, right.ContentHash);
    }

    private sealed record Cursor(ulong BundleId, string Version, string ContentHash);

    private string GetPath(MicroBundleArtifactAddress address) =>
        Path.Combine(
            _root,
            address.BundleId.ToString(),
            address.Version,
            address.ContentHash + ".bundle");
}
