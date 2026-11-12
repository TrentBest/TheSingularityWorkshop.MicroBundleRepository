namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A publication-time request for one exact ontology identity and version.
/// </summary>
public readonly record struct MicroBundleVersionRequest
{
    /// <summary>Creates an exact-version request for a canonical ontology identity.</summary>
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

    /// <summary>The portable semantic identity.</summary>
    public string Ontology { get; }
    /// <summary>The exact requested version.</summary>
    public string Version { get; }
}
