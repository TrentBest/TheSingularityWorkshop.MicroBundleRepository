using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
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
        Assert.True(catalog.TryResolve(1, "1.0.0", out _));
        Assert.False(catalog.TryResolve(1, "2.0.0", out _));
    }

    [Fact]
    public async Task PreloadClosureAsync_does_not_publish_partial_closure_when_dependency_is_missing()
    {
        var rootContent = new byte[] { 11 };
        var dependencyContent = new byte[] { 22 };
        var rootAddress = Address(1, rootContent);
        var dependencyAddress = Address(2, dependencyContent);

        var artifacts = new Dictionary<string, MicroBundleArtifact>
        {
            [rootAddress.ToString()] = new MicroBundleArtifact(rootAddress, rootContent)
        };
        var addresses = new Dictionary<ulong, MicroBundleArtifactAddress>
        {
            [1] = rootAddress
        };
        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new RepositoryTransport(artifacts));
        var catalog = new RestMicroBundleCatalog(repository, addresses, new Materializer());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.PreloadClosureAsync(new[] { 1UL }));

        Assert.False(catalog.TryResolve(1, out _));
        Assert.False(catalog.TryResolve(2, out _));

        // Repair the caller-provided address map and repository, then retry.
        // The first failed attempt must not have cached the root as loaded.
        addresses[2] = dependencyAddress;
        artifacts[dependencyAddress.ToString()] =
            new MicroBundleArtifact(dependencyAddress, dependencyContent);

        await catalog.PreloadClosureAsync(new[] { 1UL });

        Assert.True(catalog.TryResolve(1, out _));
        Assert.True(catalog.TryResolve(2, out _));
    }

    [Fact]
    public async Task PreloadClosureAsync_rejects_address_registered_under_wrong_bundle_id()
    {
        var content = new byte[] { 7 };
        var wrongAddress = Address(2, content);
        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new RepositoryTransport(new Dictionary<string, MicroBundleArtifact>
            {
                [wrongAddress.ToString()] = new MicroBundleArtifact(wrongAddress, content)
            }));
        var catalog = new RestMicroBundleCatalog(
            repository,
            new Dictionary<ulong, MicroBundleArtifactAddress> { [1] = wrongAddress },
            new Materializer());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => catalog.PreloadClosureAsync(new[] { 1UL }));

        Assert.Contains("registered for MicroBundle 1 identifies MicroBundle 2", error.Message);
        Assert.False(catalog.TryResolve(1, out _));
        Assert.False(catalog.TryResolve(2, out _));
    }

    private static MicroBundleArtifactAddress Address(ulong id, byte[] content) =>
        new(id, "1.0.0", Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

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
