using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using TheSingularityWorkshop.MicroBundleRepository.FSM_COS;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleCatalogTests
{
    [Fact]
    public async Task PreloadClosureAsync_fetches_declared_dependencies()
    {
        var rootContent = new byte[] { 1 };
        var dependencyContent = new byte[] { 2 };

        var rootAddress = Address(1, rootContent);
        var dependencyAddress = Address(2, dependencyContent);

        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new RepositoryTransport(new Dictionary<string, MicroBundleArtifact>
            {
                [rootAddress.ToString()] = new MicroBundleArtifact(rootAddress, rootContent),
                [dependencyAddress.ToString()] = new MicroBundleArtifact(dependencyAddress, dependencyContent)
            }));

        var catalog = new RestMicroBundleCatalog(
            repository,
            new Dictionary<ulong, MicroBundleArtifactAddress>
            {
                [1] = rootAddress,
                [2] = dependencyAddress
            },
            new Materializer());

        await catalog.PreloadClosureAsync(new[] { 1UL });

        Assert.True(catalog.TryResolve(1, out var root));
        Assert.True(catalog.TryResolve(2, out var dependency));
        Assert.Equal(1UL, root!.Id);
        Assert.Equal(2UL, dependency!.Id);
    }

    private static MicroBundleArtifactAddress Address(ulong id, byte[] content) =>
        new(id, "1.0.0", Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

    private sealed class Materializer : IMicroBundleArtifactMaterializer
    {
        public TheSingularityWorkshop.MicroBundleDomain.IMicroBundle Materialize(MicroBundleArtifact artifact) =>
            artifact.Address.BundleId == 1
                ? new TestBundle(1, 2)
                : new TestBundle(2);
    }

    private sealed class TestBundle(ulong id, params ulong[] dependencyIds) : IMicroBundle
    {
        public ulong Id { get; } = id;
        public TheSingularityWorkshop.MicroBundleDomain.MicroBundleDescriptor Descriptor { get; } =
            new(id, "1.0.0");
        public IReadOnlyList<MicroBundleDependencyRequest> Dependencies { get; } =
            dependencyIds.Select(MicroBundleDependencyRequest.Unconfigured).ToArray();

        public void Load(IMicroBundleLoadContext context) { }

        public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
    }

    private sealed class RepositoryTransport(
        IReadOnlyDictionary<string, MicroBundleArtifact> artifacts) : TheSingularityWorkshop.FSM_REST.IRestTransport
    {
        public Task<TheSingularityWorkshop.FSM_REST.RestResponse> SendAsync(
            TheSingularityWorkshop.FSM_REST.RestRequest request,
            CancellationToken cancellationToken = default)
        {
            var parts = request.Uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var address = new MicroBundleArtifactAddress(
                ulong.Parse(parts[^3]),
                parts[^2],
                parts[^1]);

            if (!artifacts.TryGetValue(address.ToString(), out var artifact))
                return Task.FromResult(
                    new TheSingularityWorkshop.FSM_REST.RestResponse(
                        404,
                        "Not Found",
                        new Dictionary<string, string>(),
                        string.Empty));

            var dto = new MicroBundleArtifactDto(
                artifact.Address.BundleId,
                artifact.Address.Version,
                artifact.Address.ContentHash,
                Convert.ToBase64String(artifact.Content.ToArray()));

            return Task.FromResult(
                new TheSingularityWorkshop.FSM_REST.RestResponse(
                    200,
                    "OK",
                    new Dictionary<string, string>(),
                    JsonSerializer.Serialize(dto)));
        }
    }
}
