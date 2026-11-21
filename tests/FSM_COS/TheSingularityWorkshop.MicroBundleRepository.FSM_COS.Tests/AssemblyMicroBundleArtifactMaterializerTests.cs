using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.FSM_COS;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS.Tests;

public sealed class AssemblyMicroBundleArtifactMaterializerTests
{
    [Fact]
    public void Materialize_uses_expected_runtime_id_not_repository_storage_id()
    {
        var assemblyPath = typeof(AssemblyMicroBundleArtifactMaterializerTests).Assembly.Location;
        var bytes = File.ReadAllBytes(assemblyPath);
        var address = Address(700, bytes);
        var artifact = new MicroBundleArtifact(address, bytes);

        var bundle = new AssemblyMicroBundleArtifactMaterializer().Materialize(artifact, 987654);

        Assert.Equal(987654UL, bundle.Id);
        Assert.Equal(987654UL, bundle.Descriptor.Id);
    }

    [Fact]
    public void Materialize_rejects_an_expected_runtime_id_not_present_in_the_assembly()
    {
        var assemblyPath = typeof(AssemblyMicroBundleArtifactMaterializerTests).Assembly.Location;
        var bytes = File.ReadAllBytes(assemblyPath);
        var artifact = new MicroBundleArtifact(Address(700, bytes), bytes);

        var error = Assert.Throws<InvalidOperationException>(
            () => new AssemblyMicroBundleArtifactMaterializer().Materialize(artifact, 123456));

        Assert.Contains("runtime ID 123456", error.Message);
    }

    [Fact]
    public void Materialize_rejects_non_assembly_artifact_bytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var artifact = new MicroBundleArtifact(Address(700, bytes), bytes);

        var error = Assert.Throws<InvalidOperationException>(
            () => new AssemblyMicroBundleArtifactMaterializer().Materialize(artifact, 987654));

        Assert.Contains("valid managed .NET assembly", error.Message);
        Assert.IsType<BadImageFormatException>(error.InnerException);
    }

    private static MicroBundleArtifactAddress Address(ulong storageId, byte[] bytes) =>
        new(storageId, "1.0.0", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
}

public sealed class MaterializerFixtureBundle : IMicroBundle
{
    public MicroBundleDescriptor Descriptor { get; } = new(987654, "1.0.0");
    public ulong Id => Descriptor.Id;
    public IReadOnlyList<MicroBundleDependencyRequest> Dependencies { get; } =
        Array.Empty<MicroBundleDependencyRequest>();

    public void Load(IMicroBundleLoadContext context) { }

    public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
}
