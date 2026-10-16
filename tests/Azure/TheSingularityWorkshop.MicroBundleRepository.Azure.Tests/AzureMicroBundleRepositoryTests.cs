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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Semantic_address_metadata_round_trips_scalar_and_multidimensional_indices(bool scalar)
    {
        var index = scalar
            ? new TheSingularityWorkshop.Ontology.OntologyIndex(42)
            : TheSingularityWorkshop.Ontology.OntologyIndex.Create(2, 14, 7);

        var address = TheSingularityWorkshop.Ontology.OntologyAddress.Create(
            2110,
            "1.0.0",
            "life/animal/fish/locomotion/swim",
            index);

        var metadata = new Dictionary<string, string>
        {
            [SemanticAddressMetadata.Key] = SemanticAddressMetadata.Write(address)
        };

        var restored = SemanticAddressMetadata.Read(metadata);

        Assert.NotNull(restored);
        Assert.Equal(address, restored.Value);
    }

    [Fact]
    public void Missing_semantic_address_metadata_remains_optional()
    {
        Assert.Null(SemanticAddressMetadata.Read(new Dictionary<string, string>()));
    }

    [Fact]
    public void Invalid_semantic_address_metadata_fails_explicitly()
    {
        var metadata = new Dictionary<string, string>
        {
            [SemanticAddressMetadata.Key] = "not-base64"
        };

        Assert.Throws<InvalidDataException>(() => SemanticAddressMetadata.Read(metadata));
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
