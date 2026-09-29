using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleRepositoryLiveTests
{
    [Fact]
    public async Task AzureBlob_RoundTrip_PreservesContentAndIdentity()
    {
        var storageAccountUri = Environment.GetEnvironmentVariable("MICROBUNDLE_STORAGE_ACCOUNT_URI");
        if (string.IsNullOrWhiteSpace(storageAccountUri))
            return;

        var repository = new AzureMicroBundleRepository(
            new AzureMicroBundleRepositoryOptions
            {
                StorageAccountUri = new Uri(storageAccountUri),
                ContainerName = Environment.GetEnvironmentVariable("MICROBUNDLE_CONTAINER_NAME") ?? "microbundles"
            });

        await repository.InitializeAsync();

        var content = System.Text.Encoding.UTF8.GetBytes(
            $"MicroBundleRepository Azure smoke test {Guid.NewGuid():N}");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(0x534D4F4B455F415AUL, "0.1.0-smoke", hash);
        var artifact = new MicroBundleArtifact(address, content);

        await repository.PutAsync(artifact);
        var retrieved = await repository.GetAsync(address);

        Assert.NotNull(retrieved);
        Assert.Equal(address, retrieved!.Address);
        Assert.Equal(content, retrieved.Content.ToArray());
    }
}
