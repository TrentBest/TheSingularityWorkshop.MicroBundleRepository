using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactListRequestTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Request_rejects_page_sizes_outside_bounds(int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MicroBundleArtifactListRequest(pageSize));
    }

    [Fact]
    public void Request_accepts_default_and_maximum_page_sizes()
    {
        Assert.Equal(100, new MicroBundleArtifactListRequest().PageSize);
        Assert.Equal(500, new MicroBundleArtifactListRequest(500).PageSize);
    }

    [Fact]
    public void Request_rejects_zero_bundle_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MicroBundleArtifactListRequest(bundleId: 0));
    }

    [Theory]
    [InlineData("1.0/0")]
    [InlineData("1.0\\\\0")]
    public void Request_rejects_version_path_separators(string version)
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactListRequest(bundleId: 42, version: version));
    }

    [Fact]
    public void Request_requires_bundle_id_when_version_is_supplied()
    {
        Assert.Throws<ArgumentException>(() =>
            new MicroBundleArtifactListRequest(version: "1.0.0"));
    }

    [Fact]
    public void Request_preserves_opaque_continuation_token()
    {
        const string token = "opaque+/=%2F&cursor";
        var request = new MicroBundleArtifactListRequest(continuationToken: token);

        Assert.Equal(token, request.ContinuationToken);
    }

    [Fact]
    public void Page_rejects_more_than_maximum_items()
    {
        var items = Enumerable.Range(1, MicroBundleArtifactListRequest.MaximumPageSize + 1)
            .Select(index => new MicroBundleArtifactAddress(
                (ulong)index, "1.0.0", new string('a', 64)))
            .ToArray();

        Assert.Throws<ArgumentException>(() => new MicroBundleArtifactListPage(items, null));
    }

    [Fact]
    public void Page_copies_the_supplied_collection()
    {
        var supplied = new List<MicroBundleArtifactAddress>
        {
            new(42, "1.0.0", new string('a', 64))
        };

        var page = new MicroBundleArtifactListPage(supplied, null);
        supplied.Clear();

        Assert.Single(page.Items);
    }

    [Fact]
    public void Page_allows_empty_items_with_continuation_token()
    {
        var page = new MicroBundleArtifactListPage(
            Array.Empty<MicroBundleArtifactAddress>(),
            "next");

        Assert.Empty(page.Items);
        Assert.Equal("next", page.ContinuationToken);
    }
}
