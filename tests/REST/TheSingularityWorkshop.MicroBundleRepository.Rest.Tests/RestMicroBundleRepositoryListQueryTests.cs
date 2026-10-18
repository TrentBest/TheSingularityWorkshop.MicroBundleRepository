using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleRepositoryListQueryTests
{
    [Fact]
    public async Task ListAsync_escapes_opaque_continuation_token_without_changing_it()
    {
        const string token = "opaque+/= &next";
        var transport = new StubTransport(request =>
        {
            Assert.Equal("GET", request.Method);
            Assert.Contains("continuationToken=opaque%2B%2F%3D%20%26next", request.Uri.Query, StringComparison.Ordinal);

            return new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                JsonSerializer.Serialize(new MicroBundleArtifactListDto(
                    Array.Empty<MicroBundleArtifactAddressDto>(),
                    token)));
        });

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            transport);

        var page = await repository.ListAsync(new MicroBundleArtifactListRequest(
            PageSize: 10,
            ContinuationToken: token));

        Assert.Empty(page.Items);
        Assert.Equal(token, page.ContinuationToken);
    }

    private sealed class StubTransport(Func<RestRequest, RestResponse> responder) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(responder(request));
    }
}
