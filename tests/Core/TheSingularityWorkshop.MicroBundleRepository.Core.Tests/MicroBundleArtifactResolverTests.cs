using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactResolverTests
{
    [Fact]
    public void ResolveExact_returns_the_single_address_for_each_exact_version()
    {
        var first = Address(10, "1.0.0", 'a');
        var second = Address(10, "2.0.0", 'b');
        var requests = new[]
        {
            new MicroBundleVersionRequest(10, "1.0.0"),
            new MicroBundleVersionRequest(10, "2.0.0")
        };
        var inventory = new[]
        {
            Observation(first),
            Observation(first),
            Observation(second)
        };

        var resolved = MicroBundleArtifactResolver.ResolveExact(requests, inventory);

        Assert.Equal(2, resolved.Count);
        Assert.Equal(first, resolved[requests[0]]);
        Assert.Equal(second, resolved[requests[1]]);
    }

    [Fact]
    public void ResolveExact_rejects_missing_exact_version()
    {
        var request = new MicroBundleVersionRequest(10, "1.0.0");

        var error = Assert.Throws<InvalidOperationException>(() =>
            MicroBundleArtifactResolver.ResolveExact(
                new[] { request },
                new[] { Observation(Address(10, "2.0.0", 'a')) }));

        Assert.Contains("No immutable artifact exists", error.Message);
    }

    [Fact]
    public void ResolveExact_rejects_multiple_hashes_for_same_id_and_version()
    {
        var request = new MicroBundleVersionRequest(10, "1.0.0");

        var error = Assert.Throws<InvalidOperationException>(() =>
            MicroBundleArtifactResolver.ResolveExact(
                new[] { request },
                new[]
                {
                    Observation(Address(10, "1.0.0", 'a')),
                    Observation(Address(10, "1.0.0", 'b'))
                }));

        Assert.Contains("is ambiguous", error.Message);
    }

    [Fact]
    public void ResolveExact_does_not_treat_a_different_version_as_a_match()
    {
        var request = new MicroBundleVersionRequest(10, "1.0.0");

        Assert.Throws<InvalidOperationException>(() =>
            MicroBundleArtifactResolver.ResolveExact(
                new[] { request },
                new[] { Observation(Address(10, "1.0.1", 'a')) }));
    }

    private static MicroBundleArtifactAddress Address(ulong id, string version, char hashCharacter) =>
        new(id, version, new string(hashCharacter, 64));

    private static MicroBundleArtifactObservation Observation(MicroBundleArtifactAddress address) =>
        new(address, 1, null);
}
