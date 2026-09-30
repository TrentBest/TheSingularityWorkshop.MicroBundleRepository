namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// The currently published immutable artifact address for an Experience.
/// </summary>
public sealed record ExperiencePublication(ExperienceArtifactAddress Address)
{
    /// <summary>
    /// The Experience identity represented by this publication.
    /// </summary>
    public ulong ExperienceId => Address.ExperienceId;
}
