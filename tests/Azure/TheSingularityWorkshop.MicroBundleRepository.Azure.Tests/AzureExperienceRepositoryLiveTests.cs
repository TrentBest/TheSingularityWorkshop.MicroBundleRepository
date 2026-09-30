using System.Security.Cryptography;
using System.Text;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureExperienceRepositoryLiveTests
{
    [Fact]
    public async Task AzureBlob_PublishModifyAndRetrieve_PreservesImmutableVersions()
    {
        var storageAccountUri =
            Environment.GetEnvironmentVariable("EXPERIENCE_STORAGE_ACCOUNT_URI");

        if (string.IsNullOrWhiteSpace(storageAccountUri))
            return;

        var repository = new AzureExperienceRepository(
            new AzureExperienceRepositoryOptions
            {
                StorageAccountUri = new Uri(storageAccountUri),
                ContainerName =
                    Environment.GetEnvironmentVariable("EXPERIENCE_CONTAINER_NAME")
                    ?? "experiences"
            });

        await repository.InitializeAsync();

        var v1 = CreateArtifact(
            version: "0.1.0-smoke",
            payload: """{"experienceId":3010,"revision":1}""");

        await repository.PutAsync(v1);

        var retrievedV1 = await repository.GetAsync(v1.Address);

        Assert.NotNull(retrievedV1);
        Assert.Equal(v1.Address, retrievedV1!.Address);
        Assert.Equal(v1.Content.ToArray(), retrievedV1.Content.ToArray());

        var v2 = CreateArtifact(
            version: "0.2.0-smoke",
            payload: """{"experienceId":3010,"revision":2}""");

        await repository.PutAsync(v2);

        var retrievedV2 = await repository.GetAsync(v2.Address);
        var retrievedV1Again = await repository.GetAsync(v1.Address);

        Assert.NotNull(retrievedV2);
        Assert.Equal(v2.Address, retrievedV2!.Address);
        Assert.Equal(v2.Content.ToArray(), retrievedV2.Content.ToArray());

        Assert.NotNull(retrievedV1Again);
        Assert.Equal(v1.Address, retrievedV1Again!.Address);
        Assert.Equal(v1.Content.ToArray(), retrievedV1Again.Content.ToArray());
        Assert.NotEqual(v1.Address.ContentHash, v2.Address.ContentHash);
    }

    private static ExperienceArtifact CreateArtifact(string version, string payload)
    {
        var content = Encoding.UTF8.GetBytes(payload);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3010, version, hash);

        return new ExperienceArtifact(address, content);
    }
}
