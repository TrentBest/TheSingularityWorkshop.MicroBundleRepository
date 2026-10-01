namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Operational observation of one stored MicroBundle artifact.
/// </summary>
public sealed record MicroBundleArtifactObservation(
    MicroBundleArtifactAddress Address,
    long ContentLength,
    DateTimeOffset? LastModified);
