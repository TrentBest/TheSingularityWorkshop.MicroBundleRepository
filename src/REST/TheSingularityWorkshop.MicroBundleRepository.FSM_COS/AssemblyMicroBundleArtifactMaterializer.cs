using System.Reflection;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

/// <summary>
/// Materializes a MicroBundle artifact whose representation is a .NET assembly.
/// </summary>
/// <remarks>
/// The repository verifies the content hash before this boundary is reached.
/// This adapter belongs to the FSM_COS integration boundary because it discovers
/// executable composition contracts from repository artifacts.
/// </remarks>
public sealed class AssemblyMicroBundleArtifactMaterializer : IMicroBundleArtifactMaterializer
{
    /// <summary>Loads and discovers the uniquely matching MicroBundle in an assembly artifact.</summary>
    public IMicroBundle Materialize(MicroBundleArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var payload = MicroBundleAssemblyPayload.FromBytes(artifact.Content);
        if (payload.BundleId != artifact.Address.BundleId)
            throw new InvalidOperationException(
                $"MicroBundle payload ID {payload.BundleId} does not match artifact ID {artifact.Address.BundleId}.");

        var assembly = Assembly.Load(payload.AssemblyBytes.ToArray());
        var candidates = assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(IMicroBundle).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => Activator.CreateInstance(type))
            .OfType<IMicroBundle>()
            .ToArray();

        var matches = candidates
            .Where(bundle => bundle.Id == artifact.Address.BundleId)
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Assembly artifact {artifact.Address} does not contain a public parameterless MicroBundle with ID {artifact.Address.BundleId}."),
            _ => throw new InvalidOperationException(
                $"Assembly artifact {artifact.Address} contains multiple MicroBundles with ID {artifact.Address.BundleId}.")
        };
    }
}
