namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>REST representation of a stored MicroBundle artifact observation.</summary>
public sealed record MicroBundleArtifactObservationDto(
    string Ontology,
    string Version,
    string ContentHash,
    long ContentLength,
    DateTimeOffset? LastModified);
