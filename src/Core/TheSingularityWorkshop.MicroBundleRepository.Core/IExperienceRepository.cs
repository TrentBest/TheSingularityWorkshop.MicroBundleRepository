namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Durable delivery boundary for published Experience artifacts.
/// </summary>
public interface IExperienceRepository
{
    /// <summary>Retrieves an Experience artifact by its complete address.</summary>
    ValueTask<ExperienceArtifact?> GetAsync(
        ExperienceArtifactAddress address,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an immutable Experience artifact at its deterministic content-addressed location.</summary>
    ValueTask PutAsync(
        ExperienceArtifact artifact,
        CancellationToken cancellationToken = default);
}
