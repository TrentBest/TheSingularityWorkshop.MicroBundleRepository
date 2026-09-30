using System.Security.Cryptography;
using System.Text;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureExperienceRepositoryLiveTests
{
    [Fact]
    public async Task AzureBlob_RoundTrip_PreservesContentAndIdentity()
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

        var content = Encoding.UTF8.GetBytes(
            $"""{{"experienceId":3010,"nonce":"{Guid.NewGuid():N}"}}""");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3010, "0.1.0-smoke", hash);
        var artifact = new ExperienceArtifact(address, content);

        await repository.PutAsync(artifact);
        var retrieved = await repository.GetAsync(address);

        Assert.NotNull(retrieved);
        Assert.Equal(address, retrieved!.Address);
        Assert.Equal(content, retrieved.Content.ToArray());
    }
}
