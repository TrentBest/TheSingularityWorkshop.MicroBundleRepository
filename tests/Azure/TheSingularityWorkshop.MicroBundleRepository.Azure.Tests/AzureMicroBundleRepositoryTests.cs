using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleRepositoryTests
{
    [Fact]
    public void Blob_name_is_deterministic_and_content_addressed()
    {
        var content = "magic bundle"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(300, "1.2.0", hash);

        var blobName = AzureMicroBundleRepository.GetBlobName(address);

        Assert.Equal($"artifacts/300/1.2.0/{hash}.bundle", blobName);
    }

    [Fact]
    public void Artifact_address_can_be_recovered_from_blob_name()
    {
        var hash = new string('b', 64);

        var found = AzureMicroBundleRepository.TryParseAddress(
            $"artifacts/300/1.2.0/{hash}.bundle",
            out var address);

        Assert.True(found);
        Assert.Equal(new MicroBundleArtifactAddress(300, "1.2.0", hash), address);
    }

    [Theory]
    [InlineData("artifacts/0/1.0.0/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.bundle")]
    [InlineData("artifacts/3/1.0.0/not-a-hash.bundle")]
    [InlineData("other/3/1.0.0/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.bundle")]
    public void Invalid_blob_names_are_not_discoverable(string blobName)
    {
        Assert.False(AzureMicroBundleRepository.TryParseAddress(blobName, out _));
    }

    [Fact]
    public void Default_container_name_is_microbundles()
    {
        var options = new AzureMicroBundleRepositoryOptions
        {
            StorageAccountUri = new Uri("https://example.blob.core.windows.net")
        };

        Assert.Equal("microbundles", options.ContainerName);
    }
}
