using System.Reflection;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

/// <summary>
/// Materializes a MicroBundle from an artifact whose content is a managed .NET assembly.
/// </summary>
/// <remarks>
/// The repository verifies the artifact hash before this boundary is reached.
/// The expected runtime ID is independent of the repository-local storage ID.
/// </remarks>
public sealed class AssemblyMicroBundleArtifactMaterializer : IMicroBundleArtifactMaterializer
{
    public IMicroBundle Materialize(MicroBundleArtifact artifact, ulong expectedRuntimeId)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (expectedRuntimeId == 0)
            throw new ArgumentOutOfRangeException(nameof(expectedRuntimeId));

        Assembly assembly;
        try
        {
            assembly = Assembly.Load(artifact.Content.ToArray());
        }
        catch (BadImageFormatException exception)
        {
            throw new InvalidOperationException(
                $"Artifact {artifact.Address} does not contain a valid managed .NET assembly.",
                exception);
        }

        var candidates = GetLoadableTypes(assembly)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(IMicroBundle).IsAssignableFrom(type) &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => Activator.CreateInstance(type))
            .OfType<IMicroBundle>()
            .Where(bundle =>
                bundle.Id == expectedRuntimeId &&
                bundle.Descriptor.Id == expectedRuntimeId)
            .ToArray();

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException(
                $"Assembly artifact {artifact.Address} does not contain a public parameterless MicroBundle with runtime ID {expectedRuntimeId}."),
            _ => throw new InvalidOperationException(
                $"Assembly artifact {artifact.Address} contains multiple MicroBundles with runtime ID {expectedRuntimeId}.")
        };
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
