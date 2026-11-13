using System.Text.Json;
using Xunit;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleArtifactCatalogTests
{
    [Fact]
    public async Task ListPageAsync_escapes_opaque_query_values_and_maps_identity_page()
    {
        var hash = new string('b', 64);
        var payload = JsonSerializer.Serialize(new
        {
            items = new[] { new { bundleId = 17UL, version = "1.0+beta", contentHash = hash } },
            continuationToken = "next+/="
        });

        var transport = new StubTransport(request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Contains("pageSize=25", request.Uri.Query);
            Assert.Contains("continuationToken=cursor%2B%2F%3D", request.Uri.Query);
            Assert.Contains("version=1.0%2Bbeta", request.Uri.Query);
            Assert.Contains("bundleId=17", request.Uri.Query);
            return new RestResponse(200, "OK", new Dictionary<string, string>(), payload);
        });

        var catalog = new RestMicroBundleArtifactCatalog(new Uri("https://repository.test"), transport);
        var page = await catalog.ListPageAsync(new MicroBundleArtifactListRequest(
            pageSize: 25,
            continuationToken: "cursor+ /=",
            bundleId: 17,
            version: "1.0+beta"));

        var item = Assert.Single(page.Items);
        Assert.Equal(17UL, item.BundleId);
        Assert.Equal("1.0+beta", item.Version);
        Assert.Equal(hash, item.ContentHash);
        Assert.Equal("next+/=", page.ContinuationToken);
    }

    private sealed class StubTransport(Func<RestRequest, RestResponse> responder) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responder(request));
    }
}
