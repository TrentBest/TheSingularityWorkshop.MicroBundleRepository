using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleRepositoryTests
{
    [Fact]
    public void Blob_name_uses_repository_local_handle_and_content_address()
    {
        var content = "magic bundle"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress("TheSingularityWorkshop.Renderer", "1.2.0", hash);

        var blobName = AzureMicroBundleRepository.GetBlobName(300, address);

        Assert.Equal($"artifacts/300/1.2.0/{hash}.bundle", blobName);
        Assert.DoesNotContain(address.Ontology, blobName, StringComparison.Ordinal);
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
