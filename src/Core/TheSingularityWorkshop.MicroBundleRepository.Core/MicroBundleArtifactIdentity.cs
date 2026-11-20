namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Portable identity for one immutable MicroBundle artifact revision.
/// The repository-local numeric address is deliberately not part of this identity.
/// </summary>
/// <remarks>
/// Ontology identifiers use canonical Unicode Form C and may be hierarchical
/// (for example, <c>workshop/example</c>). Empty, relative, whitespace-bearing,
/// control-character, and URI-delimiter segments are rejected so the identity
/// has one unambiguous serialized representation.
/// </remarks>
public sealed record MicroBundleArtifactIdentity
{
    public MicroBundleArtifactIdentity(string ontology, string version, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        MicroBundleArtifactAddress.ValidateVersion(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (!string.Equals(
                ontology,
                ontology.Normalize(System.Text.NormalizationForm.FormC),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Ontology must use canonical Unicode Form C normalization.",
                nameof(ontology));
        }

        if (ontology.Any(char.IsControl) ||
            ontology.Any(char.IsWhiteSpace) ||
            ontology.IndexOfAny(['\\', '?', '#', '%']) >= 0 ||
            ontology[0] == '/' ||
            ontology[^1] == '/' ||
            ontology.Contains("//", StringComparison.Ordinal) ||
            ontology.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException(
                "Ontology must be a canonical hierarchical identifier without empty, relative, whitespace, control, or URI-delimiter segments.",
                nameof(ontology));
        }

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException(
                "Content hash must be a 64-character hexadecimal SHA-256 value.",
                nameof(contentHash));

        Ontology = ontology;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    public string Ontology { get; }
    public string Version { get; }
    public string ContentHash { get; }

    public override string ToString() => $"{Ontology}/{Version}/{ContentHash}";
}
