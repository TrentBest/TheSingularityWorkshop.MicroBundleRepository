using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactIdentityTests
{
    [Fact]
    public void Address_RejectsZeroBundleId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MicroBundleArtifactAddress(0, "1.0.0", new string('a', 64)));
    }

    [Fact]
    public void Address_NormalizesHashToLowercase()
    {
        var hash = new string('A', 64);

        var address = new MicroBundleArtifactAddress(7001, "1.0.0", hash);

        Assert.Equal(hash.ToLowerInvariant(), address.ContentHash);
    }

    [Theory]
    [InlineData(7001, "")]
    [InlineData(7001, "   ")]
    public void Address_RejectsMissingVersion(ulong bundleId, string version)
    {
        var hash = new string('a', 64);

        Assert.Throws<ArgumentException>(
            () => new MicroBundleArtifactAddress(bundleId, version, hash));
    }

    [Fact]
    public void Address_RejectsPathSeparatorsInVersion()
    {
        var hash = new string('a', 64);

        Assert.Throws<ArgumentException>(
            () => new MicroBundleArtifactAddress(7001, "1/0/0", hash));

        Assert.Throws<ArgumentException>(
            () => new MicroBundleArtifactAddress(7001, "1\\0\\0", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not-a-sha256-value")]
    public void Address_RejectsInvalidSha256Hash(string hash)
    {
        Assert.Throws<ArgumentException>(
            () => new MicroBundleArtifactAddress(7001, "1.0.0", hash));
    }

    [Fact]
    public void Artifact_RejectsContentWhenHashDoesNotMatch()
    {
        var content = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexString(
            SHA256.HashData(new byte[] { 9, 8, 7 })).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(7001, "1.0.0", hash);

        Assert.Throws<ArgumentException>(
            () => new MicroBundleArtifact(address, content));
    }

    [Fact]
    public void Artifact_CopiesInputBytes()
    {
        var content = new byte[] { 1, 2, 3 };
        var artifact = MicroBundleArtifact.Create(7001, "1.0.0", content);

        content[0] = 99;

        Assert.Equal(new byte[] { 1, 2, 3 }, artifact.Content.ToArray());
    }
}
