using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureExperienceRepositoryTests
{
    [Fact]
    public void Blob_name_is_deterministic_and_content_addressed()
    {
        var content = "workshop experience"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3010, "1.2.0", hash);

        Assert.Equal(
            $"artifacts/3010/1.2.0/{hash}.experience",
            address.BlobName);
    }

    [Fact]
    public void Default_container_name_is_experiences()
    {
        var options = new AzureExperienceRepositoryOptions
        {
            StorageAccountUri = new Uri("https://example.blob.core.windows.net")
        };

        Assert.Equal("experiences", options.ContainerName);
    }
}
