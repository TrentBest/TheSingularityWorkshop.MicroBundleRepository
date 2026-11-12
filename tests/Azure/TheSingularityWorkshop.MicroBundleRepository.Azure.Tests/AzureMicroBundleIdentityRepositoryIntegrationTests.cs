using Azure.Storage.Blobs;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleIdentityRepositoryIntegrationTests
{
    [Fact]
    public async Task Identity_ingestion_is_idempotent_and_resolves_both_directions()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var content = "revision one"u8.ToArray();
            var first = await repository.PutAsync("sample.ontology", "1.0.0", content);
            var reopened = new AzureMicroBundleIdentityRepository(container);

            var repeated = await reopened.PutAsync("sample.ontology", "1.0.0", content);
            var byIdentity = await reopened.ResolveAsync(first.Identity);
            var byId = await reopened.ResolveAsync(first.Address.BundleId);

            Assert.Equal(first, repeated);
            Assert.Equal(first, byIdentity);
            Assert.Equal(first, byId);
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Different_revisions_of_the_same_ontology_receive_distinct_ids()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var first = await repository.PutAsync("sample.ontology", "1.0.0", "revision one"u8.ToArray());
            var second = await repository.PutAsync("sample.ontology", "2.0.0", "revision two"u8.ToArray());

            Assert.NotEqual(first.Address.BundleId, second.Address.BundleId);
            Assert.NotEqual(first.Identity, second.Identity);
            Assert.Equal(first, await repository.ResolveAsync(first.Identity));
            Assert.Equal(second, await repository.ResolveAsync(second.Identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Concurrent_repository_instances_commit_distinct_bindings_without_aliasing()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            var repositories = Enumerable.Range(0, 8)
                .Select(_ => new AzureMicroBundleIdentityRepository(container))
                .ToArray();
            var tasks = repositories.Select((repository, index) =>
                repository.PutAsync(
                    $"sample.ontology.{index}",
                    "1.0.0",
                    System.Text.Encoding.UTF8.GetBytes($"revision-{index}")).AsTask());

            var bindings = await Task.WhenAll(tasks);

            Assert.Equal(bindings.Length, bindings.Select(binding => binding.Address.BundleId).Distinct().Count());
            foreach (var binding in bindings)
            {
                var resolved = await repositories[0].ResolveAsync(binding.Address.BundleId);
                Assert.Equal(binding, resolved);
            }
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Numeric_write_cannot_reuse_an_identity_bound_id()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var binding = await repository.PutAsync("sample.ontology", "1.0.0", "revision one"u8.ToArray());
            var hash = binding.Identity.ContentHash;
            var numericArtifact = new MicroBundleArtifact(
                new MicroBundleArtifactAddress(binding.Address.BundleId, binding.Address.Version, hash),
                "revision one"u8.ToArray());

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await repository.PutAsync(numericArtifact));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Corrupt_identity_index_fails_closed()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var index = container.GetBlobClient("identity/identity-bindings.json");
            await index.UploadAsync(BinaryData.FromString(
                """[{"Ontology":"sample.ontology","Version":"1.0.0","ContentHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","BundleId":0}]"""));

            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await repository.ResolveAsync(
                    new MicroBundleArtifactIdentity(
                        "sample.ontology",
                        "1.0.0",
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    private static async Task<(BlobContainerClient Container, AzureMicroBundleIdentityRepository Repository)> CreateRepositoryAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "AZURITE_CONNECTION_STRING must point to the Azurite blob service.");

        var service = new BlobServiceClient(connectionString);
        var container = service.GetBlobContainerClient("mbri" + Guid.NewGuid().ToString("N")[..20]);
        await container.CreateAsync();
        return (container, new AzureMicroBundleIdentityRepository(container));
    }
}
