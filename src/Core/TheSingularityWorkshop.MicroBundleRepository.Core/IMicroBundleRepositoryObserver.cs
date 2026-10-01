namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Provides operational inventory observation without adding semantic query or composition behavior.
/// </summary>
public interface IMicroBundleRepositoryObserver
{
    /// <summary>
    /// Lists the immutable MicroBundle artifacts currently visible to the repository storage adapter.
    /// </summary>
    ValueTask<IReadOnlyList<MicroBundleArtifactObservation>> ListAsync(
        CancellationToken cancellationToken = default);
}
