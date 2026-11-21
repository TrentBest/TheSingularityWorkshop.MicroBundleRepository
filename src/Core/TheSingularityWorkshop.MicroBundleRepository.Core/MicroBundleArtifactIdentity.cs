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
    /// <summary>Creates a portable identity from a canonical ontology, exact version, and SHA-256 hash.</summary>
    /// <param name="ontology">Canonical hierarchical ontology identifier.</param>
    /// <param name="version">Exact immutable artifact version.</param>
    /// <param name="contentHash">SHA-256 digest in hexadecimal form.</param>
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

    /// <summary>Gets the canonical hierarchical ontology identifier.</summary>
    public string Ontology { get; }
    /// <summary>Gets the exact artifact version.</summary>
    public string Version { get; }
    /// <summary>Gets the lowercase SHA-256 digest of the artifact bytes.</summary>
    public string ContentHash { get; }

    /// <summary>Returns the portable ontology/version/hash representation.</summary>
    public override string ToString() => $"{Ontology}/{Version}/{ContentHash}";
}
