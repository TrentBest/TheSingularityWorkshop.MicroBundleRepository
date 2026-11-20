namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Portable identity for an immutable MicroBundle artifact.
/// This identity is independent of runtime numeric IDs and repository-local storage keys.
/// </summary>
public readonly record struct MicroBundleArtifactIdentity
{
    public MicroBundleArtifactIdentity(string ontology, string version, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (!string.Equals(ontology, ontology.Normalize(System.Text.NormalizationForm.FormC), StringComparison.Ordinal))
            throw new ArgumentException("Ontology must use canonical Unicode Form C normalization.", nameof(ontology));

        if (ontology.Any(char.IsControl) || ontology.Any(char.IsWhiteSpace) ||
            ontology.IndexOfAny(['/', '\\\\', '?', '#', '%']) >= 0)
            throw new ArgumentException("Ontology must be a canonical, route-safe identifier without whitespace or path/query delimiters.", nameof(ontology));

        if (version.Contains('/') || version.Contains('\\\\') || version.Any(char.IsControl))
            throw new ArgumentException("Version must be a single storage-safe path segment.", nameof(version));

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 value.", nameof(contentHash));

        Ontology = ontology;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    public string Ontology { get; }
    public string Version { get; }
    public string ContentHash { get; }

    public override string ToString() => $"{Ontology}/{Version}/{ContentHash}";
}
