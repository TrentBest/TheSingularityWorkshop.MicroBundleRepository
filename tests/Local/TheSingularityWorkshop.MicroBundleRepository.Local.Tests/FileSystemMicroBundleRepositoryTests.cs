using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Local.Tests;

public sealed class FileSystemMicroBundleRepositoryTests
{
    private const string Ontology = "TheSingularityWorkshop.Renderer";

    [Fact]
    public async Task Put_and_get_round_trip_preserves_portable_identity_and_content()
    {
        var root = CreateRoot();
        try
        {
            var repository = new FileSystemMicroBundleRepository(root);
            var artifact = CreateArtifact("1.2.3", "stress-microbundle"u8.ToArray());

            await repository.PutAsync(artifact);
            var retrieved = await repository.GetAsync(artifact.Address);

            Assert.NotNull(retrieved);
            Assert.Equal(artifact.Address, retrieved.Address);
            Assert.Equal(artifact.Content.ToArray(), retrieved.Content.ToArray());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Ontology_mapping_survives_repository_recreation()
    {
        var root = CreateRoot();
        try
        {
            var artifact = CreateArtifact("1.2.3", "persistent-map"u8.ToArray());
            await new FileSystemMicroBundleRepository(root).PutAsync(artifact);

            var restarted = new FileSystemMicroBundleRepository(root);
            var retrieved = await restarted.GetAsync(artifact.Address);

            Assert.NotNull(retrieved);
            Assert.Equal(artifact.Content.ToArray(), retrieved.Content.ToArray());
            Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".ontology-index"), "*.json"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Different_repository_roots_resolve_same_portable_address_independently()
    {
        var firstRoot = CreateRoot();
        var secondRoot = CreateRoot();
        try
        {
            var artifact = CreateArtifact("1.2.3", "portable-address"u8.ToArray());
            await new FileSystemMicroBundleRepository(firstRoot).PutAsync(artifact);

            var otherRepository = new FileSystemMicroBundleRepository(secondRoot);
            Assert.Null(await otherRepository.GetAsync(artifact.Address));
            await otherRepository.PutAsync(artifact);
            var retrieved = await otherRepository.GetAsync(artifact.Address);

            Assert.NotNull(retrieved);
            Assert.Equal(artifact.Address, retrieved.Address);
        }
        finally
        {
            Directory.Delete(firstRoot, recursive: true);
            Directory.Delete(secondRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Get_rejects_corrupted_bytes_at_a_valid_address()
    {
        var root = CreateRoot();
        try
        {
            var repository = new FileSystemMicroBundleRepository(root);
            var artifact = CreateArtifact("1.2.3", "original"u8.ToArray());
            await repository.PutAsync(artifact);
            var file = Directory.EnumerateFiles(root, "*.bundle", SearchOption.AllDirectories).Single();
            await File.WriteAllBytesAsync(file, "corrupted"u8.ToArray());

            await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(artifact.Address).AsTask());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Get_honors_pre_cancelled_token()
    {
        var root = CreateRoot();
        try
        {
            var repository = new FileSystemMicroBundleRepository(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var address = CreateArtifact("1.0.0", "cancelled"u8.ToArray()).Address;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => repository.GetAsync(address, cancellation.Token).AsTask());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Observer_lists_artifacts_by_portable_ontology_identity()
    {
        var root = CreateRoot();
        try
        {
            var repository = new FileSystemMicroBundleRepository(root);
            var renderer = CreateArtifact("1.0.0", "renderer"u8.ToArray());
            var visualize = CreateArtifact("1.0.0", "visualize"u8.ToArray(), "TheSingularityWorkshop.Visualize");
            await repository.PutAsync(visualize);
            await repository.PutAsync(renderer);

            var inventory = await repository.ListAsync();

            Assert.Equal(
                new[] { "TheSingularityWorkshop.Renderer", "TheSingularityWorkshop.Visualize" },
                inventory.Select(item => item.Address.Ontology));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static MicroBundleArtifact CreateArtifact(
        string version,
        byte[] content,
        string ontology = Ontology)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return new MicroBundleArtifact(new MicroBundleArtifactAddress(ontology, version, hash), content);
    }
}
