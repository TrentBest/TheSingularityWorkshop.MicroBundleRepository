using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Local.Tests;

public sealed class CachingMicroBundleRepositoryTests
{
    [Fact]
    public async Task Cold_read_fetches_origin_and_persists_for_next_instance()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository();
            var artifact = CreateArtifact(42, "1.0.0", "cold-read"u8.ToArray());
            origin.Artifacts[artifact.Address] = artifact;

            var firstRun = new CachingMicroBundleRepository(root, origin);
            var first = await firstRun.GetAsync(artifact.Address);

            Assert.NotNull(first);
            Assert.Equal(1, origin.GetCount);

            // A new adapter instance models a later application process.
            var secondRun = new CachingMicroBundleRepository(root, origin);
            var second = await secondRun.GetAsync(artifact.Address);

            Assert.NotNull(second);
            Assert.Equal(1, origin.GetCount);
            Assert.Equal(artifact.Content.ToArray(), second.Content.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_origin_artifact_returns_null_without_creating_cache_entry()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository();
            var artifact = CreateArtifact(42, "1.0.0", "missing"u8.ToArray());
            var cache = new CachingMicroBundleRepository(root, origin);

            Assert.Null(await cache.GetAsync(artifact.Address));
            Assert.Equal(1, origin.GetCount);
            Assert.False(File.Exists(ArtifactPath(root, artifact.Address)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Corrupt_cache_is_replaced_by_verified_origin_bytes()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository();
            var artifact = CreateArtifact(42, "1.0.0", "trusted-origin"u8.ToArray());
            origin.Artifacts[artifact.Address] = artifact;

            var path = ArtifactPath(root, artifact.Address);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, "tampered"u8.ToArray());

            var cache = new CachingMicroBundleRepository(root, origin);
            var retrieved = await cache.GetAsync(artifact.Address);

            Assert.NotNull(retrieved);
            Assert.Equal(artifact.Content.ToArray(), retrieved.Content.ToArray());
            Assert.Equal(1, origin.GetCount);

            var subsequent = await new CachingMicroBundleRepository(root, origin)
                .GetAsync(artifact.Address);
            Assert.NotNull(subsequent);
            Assert.Equal(1, origin.GetCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Different_versions_and_hashes_remain_separate_cache_entries()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository();
            var v1 = CreateArtifact(42, "1.0.0", "version-one"u8.ToArray());
            var v2 = CreateArtifact(42, "2.0.0", "version-two"u8.ToArray());
            origin.Artifacts[v1.Address] = v1;
            origin.Artifacts[v2.Address] = v2;

            var cache = new CachingMicroBundleRepository(root, origin);
            await cache.GetAsync(v1.Address);
            await cache.GetAsync(v2.Address);

            Assert.True(File.Exists(ArtifactPath(root, v1.Address)));
            Assert.True(File.Exists(ArtifactPath(root, v2.Address)));
            Assert.Equal(2, origin.GetCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Pre_cancelled_read_does_not_contact_origin()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository();
            var artifact = CreateArtifact(42, "1.0.0", "cancelled"u8.ToArray());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var cache = new CachingMicroBundleRepository(root, origin);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cache.GetAsync(artifact.Address, cancellation.Token).AsTask());
            Assert.Equal(0, origin.GetCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_origin_write_does_not_warm_local_cache()
    {
        var root = NewRoot();
        try
        {
            var origin = new InMemoryRepository { ThrowOnPut = true };
            var artifact = CreateArtifact(42, "1.0.0", "not-published"u8.ToArray());
            var cache = new CachingMicroBundleRepository(root, origin);

            await Assert.ThrowsAsync<IOException>(
                () => cache.PutAsync(artifact).AsTask());

            Assert.False(File.Exists(ArtifactPath(root, artifact.Address)));
            Assert.Equal(0, origin.GetCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ArtifactPath(string root, MicroBundleArtifactAddress address) =>
        Path.Combine(root, address.BundleId.ToString(), address.Version, address.ContentHash + ".bundle");

    private static MicroBundleArtifact CreateArtifact(ulong bundleId, string version, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new MicroBundleArtifact(new MicroBundleArtifactAddress(bundleId, version, hash), content);
    }

    private sealed class InMemoryRepository : IMicroBundleRepository
    {
        public Dictionary<MicroBundleArtifactAddress, MicroBundleArtifact> Artifacts { get; } = new();
        public int GetCount { get; private set; }
        public bool ThrowOnPut { get; init; }

        public ValueTask<MicroBundleArtifact?> GetAsync(
            MicroBundleArtifactAddress address,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCount++;
            Artifacts.TryGetValue(address, out var artifact);
            return ValueTask.FromResult(artifact);
        }

        public ValueTask PutAsync(
            MicroBundleArtifact artifact,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowOnPut)
                throw new IOException("Simulated origin write failure.");

            Artifacts[artifact.Address] = artifact;
            return ValueTask.CompletedTask;
        }
    }
}
