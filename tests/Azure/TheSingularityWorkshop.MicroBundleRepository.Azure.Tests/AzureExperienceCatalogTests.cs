using TheSingularityWorkshop.MicroBundleRepository.Azure;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureExperienceCatalogTests
{
    [Fact]
    public void Publication_blob_name_is_deterministic()
    {
        Assert.Equal(
            "publications/3010.publication.json",
            AzureExperienceCatalog.GetBlobName(3010));
    }

    [Fact]
    public void Default_container_name_is_experiences()
    {
        var options = new AzureExperienceRepositoryOptions
        {
            StorageAccountUri = new Uri("https://example.blob.core.windows.net")
        };

        Assert.Equal("experiences", options.ContainerName);
    }
}
