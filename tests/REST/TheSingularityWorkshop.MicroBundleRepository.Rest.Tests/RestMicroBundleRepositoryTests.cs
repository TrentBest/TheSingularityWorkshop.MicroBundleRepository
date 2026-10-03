using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using TheSingularityWorkshop.Ontology;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleRepositoryTests
{
    [Fact]
    public async Task GetAsync_materializes_verified_artifact()
    {
        var content = new byte[] { 1, 2, 3, 5, 8 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(42, "1.0.0", hash);
        var semanticAddress = OntologyAddress.Create(42, "1.0.0", "life/animal/fish");
        var dto = new MicroBundleArtifactDto(42, "1.0.0", hash, Convert.ToBase64String(content), semanticAddress);

        var transport = new StubTransport(request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Equal(
                $"https://repository.test/api/microbundles/42/1.0.0/{hash}",
                request.Uri.ToString());

            return new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                JsonSerializer.Serialize(dto));
        });

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            transport);

        var artifact = await repository.GetAsync(address);

        Assert.NotNull(artifact);
        Assert.Equal(content, artifact!.Content.ToArray());
        Assert.Equal(address, artifact.Address);
        Assert.Equal(semanticAddress, artifact.SemanticAddress);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_not_found()
    {
        var transport = new StubTransport(_ =>
            new RestResponse(404, "Not Found", new Dictionary<string, string>(), string.Empty));

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test/"),
            transport);

        var content = new byte[] { 1 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var result = await repository.GetAsync(new MicroBundleArtifactAddress(1, "1.0.0", hash));

        Assert.Null(result);
    }

    [Fact]
    public async Task PutAsync_sends_content_addressed_artifact()
    {
        var content = new byte[] { 13, 21, 34 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(9, "2.0.0", hash);
        var semanticAddress = OntologyAddress.Create(42, "1.0.0", "physics/materials");
        var artifact = new MicroBundleArtifact(address, content, semanticAddress);

        var transport = new StubTransport(request =>
        {
            Assert.Equal("PUT", request.Method);
            Assert.Equal(
                $"https://repository.test/api/microbundles/9/2.0.0/{hash}",
                request.Uri.ToString());
            Assert.Contains(Convert.ToBase64String(content), request.Body, StringComparison.Ordinal);
            Assert.Contains("physics/materials", request.Body, StringComparison.Ordinal);
            return new RestResponse(204, "No Content", new Dictionary<string, string>(), string.Empty);
        });

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            transport);

        await repository.PutAsync(artifact);
    }

    [Fact]
    public async Task GetAsync_rejects_identity_mismatch()
    {
        var content = new byte[] { 1, 2 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var address = new MicroBundleArtifactAddress(4, "1.0.0", hash);
        var dto = new MicroBundleArtifactDto(5, "1.0.0", hash, Convert.ToBase64String(content));

        var transport = new StubTransport(_ =>
            new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                JsonSerializer.Serialize(dto)));

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            transport);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetAsync(address).AsTask());
    }

    private sealed class StubTransport(
        Func<RestRequest, RestResponse> responder) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responder(request));
    }
}
