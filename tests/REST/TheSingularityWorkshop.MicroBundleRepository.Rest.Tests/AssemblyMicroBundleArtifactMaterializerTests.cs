using System.IO;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.FSM_COS;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class AssemblyMicroBundleArtifactMaterializerTests
{
    [Fact]
    public void Materialize_loads_bundle_from_assembly_artifact()
    {
        var assemblyPath = typeof(TestBundle).Assembly.Location;
        var bytes = File.ReadAllBytes(assemblyPath);
        var payload = new MicroBundleAssemblyPayload(TestBundle.BundleId, bytes);
        var artifact = MicroBundleArtifact.Create(TestBundle.BundleId, "1.0.0", payload.ToBytes());

        var bundle = new AssemblyMicroBundleArtifactMaterializer().Materialize(artifact);

        Assert.IsType<TestBundle>(bundle);
        Assert.Equal(TestBundle.BundleId, bundle.Id);
    }

    [Fact]
    public void Materialize_rejects_artifact_without_requested_bundle()
    {
        var assemblyPath = typeof(TestBundle).Assembly.Location;
        var bytes = File.ReadAllBytes(assemblyPath);
        var payload = new MicroBundleAssemblyPayload(9999, bytes);
        var artifact = MicroBundleArtifact.Create(9999, "1.0.0", payload.ToBytes());

        var exception = Assert.Throws<InvalidOperationException>(
            () => new AssemblyMicroBundleArtifactMaterializer().Materialize(artifact));

        Assert.Contains("9999", exception.Message, StringComparison.Ordinal);
    }

    public sealed class TestBundle : IMicroBundle
    {
        public const ulong BundleId = 7001;

        public ulong Id => BundleId;

        public MicroBundleDescriptor Descriptor { get; } =
            new(BundleId, "1.0.0");

        public IReadOnlyList<MicroBundleDependencyRequest> Dependencies => [];

        public void Load(IMicroBundleLoadContext context)
        {
        }

        public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
    }
}
