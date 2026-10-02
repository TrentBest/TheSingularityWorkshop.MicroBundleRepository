using Xunit;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Core.Tests;

public sealed class MicroBundleAssemblyPayloadTests
{
    [Fact]
    public void Round_trip_preserves_bundle_identity_and_bytes()
    {
        var original = new MicroBundleAssemblyPayload(7001, new byte[] { 1, 2, 3, 4 });

        var restored = MicroBundleAssemblyPayload.FromBytes(original.ToBytes());

        Assert.Equal(7001UL, restored.BundleId);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, restored.AssemblyBytes.ToArray());
    }
}
