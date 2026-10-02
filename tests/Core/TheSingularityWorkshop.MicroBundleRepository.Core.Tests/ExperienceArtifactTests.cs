using System.Security.Cryptography;
using System.Text;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class ExperienceArtifactTests
{
    [Fact]
    public void Address_UsesDeterministicContentAddressedPath()
    {
        var hash = new string('a', 64);
        var address = new ExperienceArtifactAddress(
            3010,
            "1.0.0",
            hash);

        Assert.Equal(
            "artifacts/3010/1.0.0/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.experience",
            address.BlobName);
    }

    [Fact]
    public void Artifact_RejectsContentWithIncorrectHash()
    {
        var content = Encoding.UTF8.GetBytes("experience");
        var otherContent = Encoding.UTF8.GetBytes("different");
        var hash = Convert.ToHexString(
            SHA256.HashData(otherContent)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(
            3010,
            "1.0.0",
            hash);

        Assert.Throws<ArgumentException>(
            () => new ExperienceArtifact(address, content));
    }

    [Fact]
    public void Artifact_PreservesVerifiedContentAndIdentity()
    {
        var content = Encoding.UTF8.GetBytes("{"id":3010}");
        var hash = Convert.ToHexString(
            SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3010, "1.0.0", hash);

        var artifact = new ExperienceArtifact(address, content);

        Assert.Equal(address, artifact.Address);
        Assert.Equal(content, artifact.Content.ToArray());
    }
}
