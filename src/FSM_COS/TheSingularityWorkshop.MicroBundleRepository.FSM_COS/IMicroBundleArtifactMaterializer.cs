using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

/// <summary>
/// Converts verified repository artifact bytes into the requested runtime MicroBundle.
/// </summary>
public interface IMicroBundleArtifactMaterializer
{
    /// <summary>
    /// Materializes an artifact and selects the MicroBundle with the expected domain/runtime identity.
    /// </summary>
    IMicroBundle Materialize(MicroBundleArtifact artifact, ulong expectedRuntimeId);
}
