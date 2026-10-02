using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactTests
{
    [Fact]
    public void Create_derives_content_hash_and_address()
    {
        var content = new byte[] { 1, 2, 3, 4 };

        var artifact = MicroBundleArtifact.Create(7001, "1.0.0", content);

        Assert.Equal(7001UL, artifact.Address.BundleId);
        Assert.Equal("1.0.0", artifact.Address.Version);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            artifact.Address.ContentHash);
        Assert.Equal(content, artifact.Content.ToArray());
    }
}
