namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A publication-time request for one exact ontology identity and version.
/// </summary>
public readonly record struct MicroBundleVersionRequest
{
    public MicroBundleVersionRequest(string ontology, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!string.Equals(ontology, ontology.Trim(), StringComparison.Ordinal) ||
            ontology.Any(char.IsControl) ||
            ontology.Contains('/') ||
            ontology.Contains('\\'))
            throw new ArgumentException("Ontology must be a canonical, non-empty identity string.", nameof(ontology));

        Ontology = ontology;
        Version = version;
    }

    public string Ontology { get; }
    public string Version { get; }
}
