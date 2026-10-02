using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class ExperienceArtifactIdentityTests
{
    [Fact]
    public void Address_NormalizesHashAndBuildsDeterministicBlobName()
    {
        var hash = new string('A', 64);

        var address = new ExperienceArtifactAddress(3010, "1.0.0", hash);

        Assert.Equal(hash.ToLowerInvariant(), address.ContentHash);
        Assert.Equal(
            $"artifacts/3010/1.0.0/{hash.ToLowerInvariant()}.experience",
            address.BlobName);
    }

    [Fact]
    public void Address_RejectsZeroExperienceId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExperienceArtifactAddress(0, "1.0.0", new string('a', 64)));
    }

    [Fact]
    public void Address_RejectsPathSeparatorsInVersion()
    {
        var hash = new string('a', 64);

        Assert.Throws<ArgumentException>(
            () => new ExperienceArtifactAddress(3010, "1/0/0", hash));
    }

    [Fact]
    public void Publication_RequiresAnAddress()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ExperiencePublication(null!));
    }

    [Fact]
    public void Artifact_CopiesInputBytes()
    {
        var content = "experience"u8.ToArray();
        var hash = Convert.ToHexString(
            SHA256.HashData(content)).ToLowerInvariant();

        var artifact = new ExperienceArtifact(
            new ExperienceArtifactAddress(3010, "1.0.0", hash),
            content);

        content[0] = 0;

        Assert.Equal("experience"u8.ToArray(), artifact.Content.ToArray());
    }
}
