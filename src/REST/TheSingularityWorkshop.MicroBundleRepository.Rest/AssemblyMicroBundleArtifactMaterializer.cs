using System.Reflection;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>
/// Materializes a MicroBundle artifact whose representation is a .NET assembly.
/// </summary>
/// <remarks>
/// The repository verifies the content hash before this boundary is reached.
/// The materializer then discovers the requested <see cref="IMicroBundle"/> by
/// bundle ID. Assembly dependencies are resolved by the host's normal load context.
/// </remarks>
public sealed class AssemblyMicroBundleArtifactMaterializer : IMicroBundleArtifactMaterializer
{
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
