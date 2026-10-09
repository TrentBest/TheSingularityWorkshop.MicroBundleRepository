using TheSingularityWorkshop.MicroBundleRepository.Core;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleArtifactListRequestTests
{
    [Fact]
    public void Default_request_is_valid()
    {
        var request = new MicroBundleArtifactListRequest();

        request.Validate();

        Assert.Equal(100, request.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public void Validate_rejects_out_of_range_page_sizes(int pageSize)
    {
        var request = new MicroBundleArtifactListRequest(PageSize: pageSize);

        Assert.Throws<ArgumentOutOfRangeException>(request.Validate);
    }

    [Fact]
    public void Version_filter_requires_bundle_id()
    {
        var request = new MicroBundleArtifactListRequest(Version: "1.0.0");

        Assert.Throws<ArgumentException>(request.Validate);
    }
}
