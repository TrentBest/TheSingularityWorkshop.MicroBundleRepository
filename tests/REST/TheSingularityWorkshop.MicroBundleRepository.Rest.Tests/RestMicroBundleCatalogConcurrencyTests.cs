using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleCatalogConcurrencyTests
{
    [Fact]
    public async Task Concurrent_preloads_of_same_closure_never_commit_a_partial_winner()
    {
        var rootBytes = new byte[] { 1 };
        var dependencyBytes = new byte[] { 2 };
        var rootAddress = Address(1, rootBytes);
        var dependencyAddress = Address(2, dependencyBytes);
        var artifacts = new Dictionary<string, MicroBundleArtifact>
        {
            [rootAddress.ToString()] = new(rootAddress, rootBytes),
            [dependencyAddress.ToString()] = new(dependencyAddress, dependencyBytes)
        };
        var transport = new ConcurrentRootTransport(artifacts);
        var catalog = new RestMicroBundleCatalog(
            new RestMicroBundleRepository(new Uri("https://repository.test"), transport),
            new Dictionary<ulong, MicroBundleArtifactAddress>
            {
                [1] = rootAddress,
                [2] = dependencyAddress
            },
            new Materializer());

        var first = catalog.PreloadClosureAsync(new[] { 1UL });
        var second = catalog.PreloadClosureAsync(new[] { 1UL });
        var results = await Task.WhenAll(Capture(first), Capture(second));

        Assert.Equal(1, results.Count(result => result is null));
        Assert.Single(results.Where(result => result is InvalidOperationException));

        Assert.True(catalog.TryResolve(1, out var root));
        Assert.True(catalog.TryResolve(2, out var dependency));
        Assert.Equal(1UL, root!.Id);
        Assert.Equal(2UL, dependency!.Id);
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static MicroBundleArtifactAddress Address(ulong id, byte[] bytes) =>
        new(id, "1.0.0", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

    private sealed class Materializer : IMicroBundleArtifactMaterializer
    {
        public IMicroBundle Materialize(MicroBundleArtifact artifact) =>
            artifact.Address.BundleId == 1
                ? new TestBundle(1, 2)
                : new TestBundle(2);
    }

    private sealed class TestBundle(ulong id, params ulong[] dependencyIds) : IMicroBundle
    {
        public MicroBundleDescriptor Descriptor { get; } = new(id, "1.0.0");
        public ulong Id => Descriptor.Id;
        public IReadOnlyList<MicroBundleDependencyRequest> Dependencies { get; } =
            dependencyIds.Select(id => MicroBundleDependencyRequest.Pinned(id, "1.0.0")).ToArray();

        public void Load(IMicroBundleLoadContext context) { }
        public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
    }

    private sealed class ConcurrentRootTransport(
        IReadOnlyDictionary<string, MicroBundleArtifact> artifacts) : IRestTransport
    {
        private readonly TaskCompletionSource _bothRootRequests =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _rootRequestCount;

        public async Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default)
        {
            var parts = request.Uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var address = new MicroBundleArtifactAddress(
                ulong.Parse(parts[^3]),
                parts[^2],
                parts[^1]);

            if (address.BundleId == 1 &&
                Interlocked.Increment(ref _rootRequestCount) == 2)
                _bothRootRequests.TrySetResult();

            if (address.BundleId == 1)
                await _bothRootRequests.Task.WaitAsync(cancellationToken);

            if (!artifacts.TryGetValue(address.ToString(), out var artifact))
                return new RestResponse(404, "Not Found", new Dictionary<string, string>(), string.Empty);

            var dto = new MicroBundleArtifactDto(
                artifact.Address.BundleId,
                artifact.Address.Version,
                artifact.Address.ContentHash,
                Convert.ToBase64String(artifact.Content.ToArray()));

            return new RestResponse(
                200,
                "OK",
                new Dictionary<string, string>(),
                JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
}
