using System.Security.Cryptography;
using System.Text;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Azure;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureExperienceCatalogLiveTests
{
    [Fact]
    public async Task AzureBlob_PublishModifyAndUnpublish_TracksCurrentVersion()
    {
        var storageAccountUri =
            Environment.GetEnvironmentVariable("EXPERIENCE_STORAGE_ACCOUNT_URI");

        if (string.IsNullOrWhiteSpace(storageAccountUri))
            return;

        var options = new AzureExperienceRepositoryOptions
        {
            StorageAccountUri = new Uri(storageAccountUri),
            ContainerName =
                Environment.GetEnvironmentVariable("EXPERIENCE_CONTAINER_NAME")
                ?? "experiences"
        };

        var artifacts = new AzureExperienceRepository(options);
        var catalog = new AzureExperienceCatalog(options);

        await artifacts.InitializeAsync();
        await catalog.InitializeAsync();

        var experienceId = 900000UL + (ulong)Random.Shared.Next(1, 99999);
        var v1 = CreateArtifact(experienceId, "0.1.0-smoke", 1);
        var v2 = CreateArtifact(experienceId, "0.2.0-smoke", 2);

        try
        {
            await artifacts.PutAsync(v1);
            await artifacts.PutAsync(v2);

            await catalog.PublishAsync(new ExperiencePublication(v1.Address));

            var publishedV1 = await catalog.GetPublishedAsync(experienceId);

            Assert.NotNull(publishedV1);
            Assert.Equal(v1.Address, publishedV1!.Address);

            await catalog.PublishAsync(new ExperiencePublication(v2.Address));

            var publishedV2 = await catalog.GetPublishedAsync(experienceId);

            Assert.NotNull(publishedV2);
            Assert.Equal(v2.Address, publishedV2!.Address);

            var originalV1 = await artifacts.GetAsync(v1.Address);

            Assert.NotNull(originalV1);
            Assert.Equal(v1.Content.ToArray(), originalV1!.Content.ToArray());

            await catalog.UnpublishAsync(experienceId);

            Assert.Null(await catalog.GetPublishedAsync(experienceId));
        }
        finally
        {
            await catalog.UnpublishAsync(experienceId);
        }
    }

    private static ExperienceArtifact CreateArtifact(
        ulong experienceId,
        string version,
        int revision)
    {
        var content = Encoding.UTF8.GetBytes(
            $"{{\"experienceId\":{experienceId},\"revision\":{revision}}}");
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(experienceId, version, hash);

        return new ExperienceArtifact(address, content);
    }
}