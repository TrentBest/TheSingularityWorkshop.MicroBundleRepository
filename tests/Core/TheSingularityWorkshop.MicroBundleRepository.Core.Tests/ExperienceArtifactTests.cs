using System.Security.Cryptography;
using System.Text;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class ExperienceArtifactTests
{
    [Fact]
    public void Address_UsesDeterministicContentAddressedPath()
    {
        var address = new ExperienceArtifactAddress(
            3010,
            "1.0.0",
            "abcdef123456");

        Assert.Equal(
            "artifacts/3010/1.0.0/abcdef123456.experience",
            address.BlobName);
    }

    [Fact]
    public void Artifact_RejectsContentWithIncorrectHash()
    {
        var content = Encoding.UTF8.GetBytes("experience");
        var address = new ExperienceArtifactAddress(
            3010,
            "1.0.0",
            "not-the-content-hash");

        Assert.Throws<ArgumentException>(
            () => new ExperienceArtifact(address, content));
    }

    [Fact]
    public void Artifact_PreservesVerifiedContentAndIdentity()
    {
        var content = Encoding.UTF8.GetBytes("{"id":3010}");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3010, "1.0.0", hash);

        var artifact = new ExperienceArtifact(address, content);

        Assert.Equal(address, artifact.Address);
        Assert.Equal(content, artifact.Content.ToArray());
    }
}
