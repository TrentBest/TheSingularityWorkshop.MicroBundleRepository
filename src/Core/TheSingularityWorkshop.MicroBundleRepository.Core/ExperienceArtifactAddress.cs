namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Complete semantic, version, and content address for a published Experience artifact.
/// </summary>
public sealed record ExperienceArtifactAddress(
    ulong ExperienceId,
    string Version,
    string ContentHash)
{
    /// <summary>Gets the deterministic blob path for this artifact.</summary>
    public string BlobName =>
        $"artifacts/{ExperienceId}/{Version}/{ContentHash}.experience";
}
