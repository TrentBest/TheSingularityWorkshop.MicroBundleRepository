using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Rest;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleRepositoryObserverTests
{
    [Fact]
    public async Task ListAsync_materializes_artifact_inventory()
    {
        var hash = new string('a', 64);
        var payload = JsonSerializer.Serialize(new[]
        {
            new
            {
                bundleId = 42UL,
                version = "1.0.0",
                contentHash = hash,
                contentLength = 128L,
                lastModified = "2026-10-01T19:00:00+00:00"
            }
        });

        var transport = new StubTransport(request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Equal(
                "https://repository.test/api/microbundles",
                request.Uri.ToString());

            return new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                payload);
        });

        var observer = new RestMicroBundleRepositoryObserver(
            new Uri("https://repository.test"),
            transport);

        var observations = await observer.ListAsync();

        var observation = Assert.Single(observations);
        Assert.Equal(42UL, observation.Address.BundleId);
        Assert.Equal("1.0.0", observation.Address.Version);
        Assert.Equal(hash, observation.Address.ContentHash);
        Assert.Equal(128L, observation.ContentLength);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero),
            observation.LastModified);
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
