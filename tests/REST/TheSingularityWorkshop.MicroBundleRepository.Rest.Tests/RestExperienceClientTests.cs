using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestExperienceClientTests
{
    [Fact]
    public async Task GetPublishedAddressAsync_returns_complete_immutable_address()
    {
        var transport = new StubTransport(request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Equal("https://repository.test/api/experiences/3101/publication", request.Uri.ToString());
            return new RestResponse(200, "OK", new Dictionary<string, string>(),
                """{"experienceId":3101,"version":"1.0.0","contentHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");
        });

        var client = new RestExperienceClient(new Uri("https://repository.test"), transport);
        var address = await client.GetPublishedAddressAsync(3101);

        Assert.NotNull(address);
        Assert.Equal((ulong)3101, address!.ExperienceId);
        Assert.Equal("1.0.0", address.Version);
        Assert.Equal(new string('a', 64), address.ContentHash);
    }

    [Fact]
    public async Task GetPublishedAddressAsync_returns_null_when_unpublished()
    {
        var client = new RestExperienceClient(
            new Uri("https://repository.test/"),
            new StubTransport(_ => new RestResponse(404, "Not Found", new Dictionary<string, string>(), string.Empty)));

        Assert.Null(await client.GetPublishedAddressAsync(3101));
    }

    [Fact]
    public async Task GetArtifactAsync_verifies_and_materializes_immutable_bytes()
    {
        var content = new byte[] { 2, 3, 5, 7, 11 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3101, "1.0.0", hash);
        var response = JsonSerializer.Serialize(new
        {
            experienceId = address.ExperienceId,
            version = address.Version,
            contentHash = address.ContentHash,
            contentBase64 = Convert.ToBase64String(content)
        });

        var client = new RestExperienceClient(
            new Uri("https://repository.test"),
            new StubTransport(request =>
            {
                Assert.Equal("GET", request.Method);
                Assert.Equal($"https://repository.test/api/experiences/3101/1.0.0/{hash}", request.Uri.ToString());
                return new RestResponse(200, "OK", new Dictionary<string, string>(), response);
            }));

        var artifact = await client.GetArtifactAsync(address);

        Assert.NotNull(artifact);
        Assert.Equal(content, artifact!.Content.ToArray());
        Assert.Equal(address, artifact.Address);
    }

    [Fact]
    public async Task GetArtifactAsync_rejects_corrupt_bytes_even_when_server_returns_success()
    {
        var expectedContent = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexString(SHA256.HashData(expectedContent)).ToLowerInvariant();
        var address = new ExperienceArtifactAddress(3101, "1.0.0", hash);
        var wrongContent = new byte[] { 9, 9, 9 };
        var response = JsonSerializer.Serialize(new
        {
            experienceId = address.ExperienceId,
            version = address.Version,
            contentHash = address.ContentHash,
            contentBase64 = Convert.ToBase64String(wrongContent)
        });

        var client = new RestExperienceClient(
            new Uri("https://repository.test"),
            new StubTransport(_ => new RestResponse(200, "OK", new Dictionary<string, string>(), response)));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetArtifactAsync(address).AsTask());
    }

    private sealed class StubTransport(Func<RestRequest, RestResponse> responder) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responder(request));
    }
}
