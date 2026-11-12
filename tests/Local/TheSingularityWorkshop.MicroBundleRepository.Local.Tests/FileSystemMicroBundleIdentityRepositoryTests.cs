using System.Security.Cryptography;
using Xunit;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;

namespace TheSingularityWorkshop.MicroBundleRepository.Local.Tests;

public sealed class FileSystemMicroBundleIdentityRepositoryTests
{
    [Fact]
    public async Task Repeated_ingestion_and_reopen_recover_the_same_binding()
    {
        var root = NewRoot();
        try
        {
            var firstRepository = new FileSystemMicroBundleIdentityRepository(root);
            var content = "zeroth ontology"u8.ToArray();

            var first = await firstRepository.PutAsync("workshop/zeroth", "1.0.0", content);
            var repeated = await firstRepository.PutAsync("workshop/zeroth", "1.0.0", content);
            var reopened = new FileSystemMicroBundleIdentityRepository(root);
            var recovered = await reopened.ResolveAsync(first.Identity);
            var reverse = await reopened.ResolveAsync(first.Address.BundleId);

            Assert.Equal(first, repeated);
            Assert.Equal(first, recovered);
            Assert.Equal(first, reverse);
            Assert.NotNull(await reopened.GetAsync(first.Address));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Versions_and_content_revisions_of_one_ontology_receive_distinct_ids()
    {
        var root = NewRoot();
        try
        {
            var repository = new FileSystemMicroBundleIdentityRepository(root);
            var v1 = await repository.PutAsync("workshop/zeroth", "1.0.0", "first"u8.ToArray());
            var v2 = await repository.PutAsync("workshop/zeroth", "2.0.0", "first"u8.ToArray());
            var revised = await repository.PutAsync("workshop/zeroth", "2.0.0", "second"u8.ToArray());

            Assert.NotEqual(v1.Address.BundleId, v2.Address.BundleId);
            Assert.NotEqual(v2.Address.BundleId, revised.Address.BundleId);
            Assert.NotEqual(v2.Identity, revised.Identity);
            Assert.Equal(v1, await repository.ResolveAsync(v1.Identity));
            Assert.Equal(v2, await repository.ResolveAsync(v2.Identity));
            Assert.Equal(revised, await repository.ResolveAsync(revised.Identity));
            Assert.Equal(v1.Identity, (await repository.ResolveAsync(v1.Address.BundleId))?.Identity);
            Assert.Equal(v2.Identity, (await repository.ResolveAsync(v2.Address.BundleId))?.Identity);
            Assert.Equal(revised.Identity, (await repository.ResolveAsync(revised.Address.BundleId))?.Identity);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Concurrent_instances_cannot_allocate_duplicate_or_conflicting_bindings()
    {
        var root = NewRoot();
        try
        {
            var repositories = Enumerable.Range(0, 12)
                .Select(_ => new FileSystemMicroBundleIdentityRepository(root))
                .ToArray();

            var sameContent = "same immutable revision"u8.ToArray();
            var sameIdentityTasks = repositories
                .Select(repository => repository.PutAsync("ontology/shared", "1.0.0", sameContent).AsTask());
            var sameIdentityResults = await Task.WhenAll(sameIdentityTasks);

            Assert.All(sameIdentityResults, result => Assert.Equal(sameIdentityResults[0], result));

            var distinctTasks = repositories
                .Select((repository, index) => repository.PutAsync(
                    "ontology/shared",
                    "2.0.0",
                    System.Text.Encoding.UTF8.GetBytes($"revision-{index}")).AsTask());
            var distinctResults = await Task.WhenAll(distinctTasks);

            Assert.Equal(distinctResults.Length, distinctResults.Select(x => x.Address.BundleId).Distinct().Count());

            var reopened = new FileSystemMicroBundleIdentityRepository(root);
            foreach (var binding in sameIdentityResults.Concat(distinctResults))
            {
                Assert.Equal(binding, await reopened.ResolveAsync(binding.Identity));
                Assert.Equal(binding, await reopened.ResolveAsync(binding.Address.BundleId));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Cancelled_ingestion_does_not_create_a_binding()
    {
        var root = NewRoot();
        try
        {
            var repository = new FileSystemMicroBundleIdentityRepository(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => repository.PutAsync("ontology/cancelled", "1.0.0", "cancelled"u8.ToArray(), cancellation.Token).AsTask());

            var identity = new MicroBundleArtifactIdentity(
                "ontology/cancelled",
                "1.0.0",
                Convert.ToHexString(SHA256.HashData("cancelled"u8)).ToLowerInvariant());
            Assert.Null(await repository.ResolveAsync(identity));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Existing_numeric_artifact_address_contract_remains_usable()
    {
        var root = NewRoot();
        try
        {
            var repository = new FileSystemMicroBundleIdentityRepository(root);
            var bytes = "legacy numeric path"u8.ToArray();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var address = new MicroBundleArtifactAddress(987, "3.2.1", hash);
            var artifact = new MicroBundleArtifact(address, bytes);

            await repository.PutAsync(artifact);
            var retrieved = await repository.GetAsync(address);

            Assert.NotNull(retrieved);
            Assert.Equal(bytes, retrieved.Content.ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Numeric_put_cannot_alias_an_id_already_bound_to_an_ontology_revision()
    {
        var root = NewRoot();
        try
        {
            var repository = new FileSystemMicroBundleIdentityRepository(root);
            var binding = await repository.PutAsync(
                "ontology/bound",
                "1.0.0",
                "original"u8.ToArray());

            var replacementBytes = "different revision"u8.ToArray();
            var replacementHash = Convert.ToHexString(SHA256.HashData(replacementBytes)).ToLowerInvariant();
            var replacement = new MicroBundleArtifact(
                new MicroBundleArtifactAddress(binding.Address.BundleId, "2.0.0", replacementHash),
                replacementBytes);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => repository.PutAsync(replacement).AsTask());

            Assert.Equal(binding, await repository.ResolveAsync(binding.Identity));
            Assert.NotNull(await repository.GetAsync(binding.Address));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
