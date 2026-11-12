namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Portable identity for one immutable MicroBundle artifact revision.
/// The repository-local numeric address is deliberately not part of this identity.
/// </summary>
public readonly record struct MicroBundleArtifactIdentity
{
    public MicroBundleArtifactIdentity(string ontology, string version, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ontology);
        MicroBundleArtifactAddress.ValidateVersion(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Content hash must be a 64-character hexadecimal SHA-256 value.", nameof(contentHash));

        Ontology = ontology;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    public string Ontology { get; }
    public string Version { get; }
    public string ContentHash { get; }
}
