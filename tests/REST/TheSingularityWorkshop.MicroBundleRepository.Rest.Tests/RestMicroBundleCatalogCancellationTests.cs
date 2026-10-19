using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using TheSingularityWorkshop.FSM_REST;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleCatalogCancellationTests
{
    [Fact]
    public async Task PreloadClosureAsync_does_not_publish_staged_bundle_when_cancelled_before_commit()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var address = new MicroBundleArtifactAddress(
            42,
            "1.0.0",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var cancellation = new CancellationTokenSource();
        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new CancellingTransport(address, bytes, cancellation));
        var catalog = new RestMicroBundleCatalog(
            repository,
            new Dictionary<ulong, MicroBundleArtifactAddress> { [42] = address },
            new Materializer());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.PreloadClosureAsync(new[] { 42UL }, cancellation.Token));

        Assert.False(catalog.TryResolve(42, out _));
        Assert.False(catalog.TryResolve(42, "1.0.0", out _));
    }

    private sealed class Materializer : IMicroBundleArtifactMaterializer
    {
        public IMicroBundle Materialize(MicroBundleArtifact artifact) =>
            new TestBundle(artifact.Address.BundleId, artifact.Address.Version);
    }

    private sealed class TestBundle(ulong id, string version) : IMicroBundle
    {
        public MicroBundleDescriptor Descriptor { get; } = new(id, version);
        public ulong Id => Descriptor.Id;
        public IReadOnlyList<MicroBundleDependencyRequest> Dependencies { get; } =
            Array.Empty<MicroBundleDependencyRequest>();

        public void Load(IMicroBundleLoadContext context) { }
        public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
    }

    private sealed class CancellingTransport(
        MicroBundleArtifactAddress address,
        byte[] bytes,
        CancellationTokenSource cancellation) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default)
        {
            var dto = new MicroBundleArtifactDto(
                address.BundleId,
                address.Version,
                address.ContentHash,
                Convert.ToBase64String(bytes));
            var response = new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

            // Simulate cancellation after the final artifact has been returned but
            // before the catalog's atomic commit point.
            cancellation.Cancel();
            return Task.FromResult(response);
        }
    }
}
