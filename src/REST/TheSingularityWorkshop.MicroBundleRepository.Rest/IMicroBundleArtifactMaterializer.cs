using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Converts verified repository artifact bytes into a runtime MicroBundle.</summary>
public interface IMicroBundleArtifactMaterializer
{
    IMicroBundle Materialize(MicroBundleArtifact artifact);
}
