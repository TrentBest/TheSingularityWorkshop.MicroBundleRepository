using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactIdentityTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Identity_uses_canonical_ontology_version_and_normalized_hash()
    {
        var identity = new MicroBundleArtifactIdentity(
            "TheSingularityWorkshop.Renderer",
            "1.2.3",
            Hash.ToUpperInvariant());

        Assert.Equal("TheSingularityWorkshop.Renderer", identity.Ontology);
        Assert.Equal("1.2.3", identity.Version);
        Assert.Equal(Hash, identity.ContentHash);
        Assert.Equal(
            $"TheSingularityWorkshop.Renderer/1.2.3/{Hash}",
            identity.ToString());
    }

    [Fact]
    public void Identity_equality_is_portable_and_ordinal()
    {
        var first = new MicroBundleArtifactIdentity("Workshop.Renderer", "1.0.0", Hash);
        var same = new MicroBundleArtifactIdentity("Workshop.Renderer", "1.0.0", Hash.ToUpperInvariant());
        var differentOntology = new MicroBundleArtifactIdentity("workshop.Renderer", "1.0.0", Hash);

        Assert.Equal(first, same);
        Assert.NotEqual(first, differentOntology);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Workshop Renderer")]
    [InlineData("Workshop/Renderer")]
    [InlineData("Workshop\\Renderer")]
    [InlineData("Workshop?Renderer")]
    [InlineData("Workshop#Renderer")]
    [InlineData("Workshop%2FRenderer")]
    public void Identity_rejects_noncanonical_or_route_ambiguous_ontology(string ontology)
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity(ontology, "1.0.0", Hash));
    }

    [Fact]
    public void Identity_rejects_non_normalized_unicode()
    {
        var decomposed = "Workshop.Cafe\u0301";

        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity(decomposed, "1.0.0", Hash));
    }

    [Fact]
    public void Identity_rejects_path_separators_in_version()
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("Workshop.Renderer", "../1.0.0", Hash));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("1.0/0")]
    [InlineData("1.0\\\\0")]
    [InlineData("1.0?0")]
    [InlineData("1.0#0")]
    [InlineData("1.0%2F0")]
    [InlineData("1.0 0")]
    public void Identity_rejects_ambiguous_version_path_segments(string version)
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("Workshop.Renderer", version, Hash));
    }

    [Fact]
    public void Identity_requires_sha256_hex()
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("Workshop.Renderer", "1.0.0", "not-a-hash"));
    }
}
