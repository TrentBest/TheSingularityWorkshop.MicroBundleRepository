using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactIdentityTests
{
    private const string Hash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";

    [Fact]
    public void Identity_normalizes_hash_case_without_changing_ontology()
    {
        var identity = new MicroBundleArtifactIdentity("workshop/example", "1.0.0", Hash);

        Assert.Equal("workshop/example", identity.Ontology);
        Assert.Equal("1.0.0", identity.Version);
        Assert.Equal(Hash.ToLowerInvariant(), identity.ContentHash);
    }

    [Fact]
    public void Identity_rejects_missing_or_invalid_components()
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity(" ", "1.0.0", Hash));
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("ontology", " ", Hash));
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("ontology", "1.0.0", "invalid"));
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactIdentity("ontology", "../1.0.0", Hash));
    }

    [Fact]
    public void Different_versions_and_hashes_are_distinct_revision_identities()
    {
        var original = new MicroBundleArtifactIdentity("ontology", "1.0.0", Hash);
        var nextVersion = new MicroBundleArtifactIdentity("ontology", "2.0.0", Hash);
        var nextContent = new MicroBundleArtifactIdentity(
            "ontology",
            "1.0.0",
            new string('0', 64));

        Assert.NotEqual(original, nextVersion);
        Assert.NotEqual(original, nextContent);
    }

    [Fact]
    public void Binding_requires_version_and_hash_to_match_numeric_address()
    {
        var identity = new MicroBundleArtifactIdentity("ontology", "1.0.0", Hash);
        var address = new MicroBundleArtifactAddress(42, "1.0.0", Hash);

        var binding = new MicroBundleIdentityBinding(identity, address);

        Assert.Equal(identity, binding.Identity);
        Assert.Equal(address, binding.Address);
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleIdentityBinding(
                identity,
                new MicroBundleArtifactAddress(42, "2.0.0", Hash)));
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleIdentityBinding(
                identity,
                new MicroBundleArtifactAddress(42, "1.0.0", new string('0', 64))));
    }

    [Fact]
    public void Binding_rejects_zero_local_id()
    {
        var identity = new MicroBundleArtifactIdentity("ontology", "1.0.0", Hash);
        var address = default(MicroBundleArtifactAddress);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MicroBundleIdentityBinding(identity, address));
    }
}
