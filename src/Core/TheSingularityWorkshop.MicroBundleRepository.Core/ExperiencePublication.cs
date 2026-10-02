namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// The currently published immutable artifact address for an Experience.
/// </summary>
public sealed record ExperiencePublication
{
    /// <summary>
    /// Creates a publication pointer to an immutable artifact.
    /// </summary>
    public ExperiencePublication(ExperienceArtifactAddress address)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
    }

    /// <summary>
    /// Gets the immutable artifact address currently selected for publication.
    /// </summary>
    public ExperienceArtifactAddress Address { get; }

    /// <summary>
    /// Gets the Experience identity represented by this publication.
    /// </summary>
    public ulong ExperienceId => Address.ExperienceId;
}
