using System.Security.Cryptography;
using Xunit;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;

namespace TheSingularityWorkshop.MicroBundleRepository.Local.Tests;

public sealed class FileSystemMicroBundleRepositoryTests
{
    [Fact]
    public async Task Put_and_get_round_trip_preserves_content_and_address()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "stress-microbundle"u8.ToArray());

            await repository.PutAsync(artifact);
            var retrieved = await repository.GetAsync(artifact.Address);

            Assert.NotNull(retrieved);
            Assert.Equal(artifact.Address, retrieved.Address);
            Assert.Equal(artifact.Content.ToArray(), retrieved.Content.ToArray());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Artifact_is_stored_at_deterministic_content_address()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "bundle"u8.ToArray());

            await repository.PutAsync(artifact);

            var expected = Path.Combine(rootPath, "42", "1.2.3", artifact.Address.ContentHash + ".bundle");
            Assert.True(File.Exists(expected));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Get_returns_null_when_artifact_is_missing()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "missing"u8.ToArray());

            Assert.Null(await repository.GetAsync(artifact.Address));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Get_rejects_corrupted_bytes_at_a_valid_address()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "original"u8.ToArray());
            var path = Path.Combine(rootPath, "42", "1.2.3", artifact.Address.ContentHash + ".bundle");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, "corrupted"u8.ToArray());

            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(artifact.Address).AsTask());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Get_honors_pre_cancelled_token_even_when_artifact_is_missing()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "cancelled-read"u8.ToArray());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => repository.GetAsync(artifact.Address, cancellation.Token).AsTask());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Put_honors_pre_cancelled_token()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var artifact = CreateArtifact(42, "1.2.3", "cancelled"u8.ToArray());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => repository.PutAsync(artifact, cancellation.Token).AsTask());
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Observer_lists_artifact_addresses_in_stable_order()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var repository = new FileSystemMicroBundleRepository(rootPath);
            var second = CreateArtifact(8, "1.0.0", "second"u8.ToArray());
            var first = CreateArtifact(3, "1.0.0", "first"u8.ToArray());
            await repository.PutAsync(second);
            await repository.PutAsync(first);

            var inventory = await repository.ListAsync();

            Assert.Equal(new ulong[] { 3, 8 }, inventory.Select(item => item.Address.BundleId));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static MicroBundleArtifact CreateArtifact(ulong bundleId, string version, byte[] content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new MicroBundleArtifact(new MicroBundleArtifactAddress(bundleId, version, hash), content);
    }
}
