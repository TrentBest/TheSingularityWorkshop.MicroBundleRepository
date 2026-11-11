namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Portable identity for one immutable, versioned MicroBundle artifact.
/// Repository-local numeric handles are deliberately excluded from this address.
/// </summary>
public readonly record struct MicroBundleArtifactAddress
{
    /// <summary>Creates a portable artifact address from ontology, version, and SHA-256.</summary>
    public MicroBundleArtifactAddress(string ontology, string version, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (!string.Equals(ontology, ontology.Trim(), StringComparison.Ordinal) ||
            ontology.Any(char.IsControl) ||
            ontology.Contains('/') ||
            ontology.Contains('\\'))
            throw new ArgumentException("Ontology must be a canonical, non-empty identity string.", nameof(ontology));

        if (version.Contains('/') || version.Contains('\\'))
            throw new ArgumentException("Version must be a single storage-safe path segment.", nameof(version));

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 value.", nameof(contentHash));

        Ontology = ontology;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    /// <summary>The canonical semantic identity of the MicroBundle.</summary>
    public string Ontology { get; }

    /// <summary>The explicit artifact version.</summary>
    public string Version { get; }

    /// <summary>The lowercase SHA-256 content identity.</summary>
    public string ContentHash { get; }

    /// <summary>Returns the portable ontology/version/content address.</summary>
    public override string ToString() => $"{Ontology}/{Version}/{ContentHash}";
}
