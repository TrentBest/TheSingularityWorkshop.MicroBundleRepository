using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactTests
{
    [Fact]
    public void Address_requires_canonical_ontology_identity()
    {
        var hash = new string('0', 64);

        Assert.Throws<ArgumentException>(() => new MicroBundleArtifactAddress(" ", "1.0.0", hash));
        Assert.Throws<ArgumentException>(() => new MicroBundleArtifactAddress(" ontology ", "1.0.0", hash));
        Assert.Throws<ArgumentException>(() => new MicroBundleArtifactAddress("ontology/child", "1.0.0", hash));
    }

    [Fact]
    public void Address_requires_valid_sha256_identity()
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactAddress("TheSingularityWorkshop.Renderer", "1.0.0", "not-a-hash"));
    }

    [Fact]
    public void Address_rejects_path_separators_in_version()
    {
        var hash = new string('0', 64);

        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactAddress("TheSingularityWorkshop.Renderer", "1.0/0", hash));
    }

    [Fact]
    public void Address_preserves_portable_ontology_identity()
    {
        var hash = new string('A', 64);
        var address = new MicroBundleArtifactAddress(
            "TheSingularityWorkshop.Renderer",
            "1.2.0",
            hash);

        Assert.Equal("TheSingularityWorkshop.Renderer", address.Ontology);
        Assert.Equal(hash.ToLowerInvariant(), address.ContentHash);
        Assert.Equal($"TheSingularityWorkshop.Renderer/1.2.0/{hash.ToLowerInvariant()}", address.ToString());
    }

    [Fact]
    public void Artifact_rejects_content_that_does_not_match_address()
    {
        var address = new MicroBundleArtifactAddress(
            "TheSingularityWorkshop.Renderer",
            "1.0.0",
            new string('0', 64));

        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifact(address, "weapon"u8.ToArray()));
    }

    [Fact]
    public void Artifact_copies_content_and_exposes_immutable_memory()
    {
        var content = "weapon"u8.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress("TheSingularityWorkshop.Renderer", "1.0.0", hash);

        var artifact = new MicroBundleArtifact(address, content);
        content[0] = 0;

        Assert.Equal((byte)'w', artifact.Content.Span[0]);
        Assert.Equal(address, artifact.Address);
    }
}
