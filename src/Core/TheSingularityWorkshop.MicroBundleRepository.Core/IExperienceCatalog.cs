namespace TheSingularityWorkshop.MicroBundleRepository.Core;


/// <summary>
/// Durable publication state for Experiences.
/// </summary>
public interface IExperienceCatalog
{
    /// <summary>
    /// Gets all currently published Experience pointers.
    /// </summary>
    ValueTask<IReadOnlyList<ExperiencePublication>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currently published artifact for an Experience, or null when unpublished.
    /// </summary>
    ValueTask<ExperiencePublication?> GetPublishedAsync(
        ulong experienceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the publication pointer to the supplied immutable artifact address.
    /// </summary>
    ValueTask PublishAsync(
        ExperiencePublication publication,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the publication pointer without deleting immutable artifact bytes.
    /// </summary>
    ValueTask UnpublishAsync(
        ulong experienceId,
        CancellationToken cancellationToken = default);
}
