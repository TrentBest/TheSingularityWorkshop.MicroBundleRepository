using System.Security.Cryptography;
using TheSingularityWorkshop.Ontology;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable MicroBundle bytes together with their content identity.
/// </summary>
public sealed class MicroBundleArtifact
{
    private readonly byte[] _content;

    /// <summary>Creates an artifact and optionally associates a semantic ontology address.</summary>
    public MicroBundleArtifact(
        MicroBundleArtifactAddress address,
        ReadOnlyMemory<byte> content,
        OntologyAddress? semanticAddress = null)
    {
        ArgumentNullException.ThrowIfNull(address.Version);
        ArgumentNullException.ThrowIfNull(address.ContentHash);

        _content = content.ToArray();

        var actualHash = Convert.ToHexString(SHA256.HashData(_content)).ToLowerInvariant();
        if (!string.Equals(actualHash, address.ContentHash, StringComparison.Ordinal))
            throw new ArgumentException("The supplied content does not match the artifact content hash.", nameof(content));

        Address = address;
        SemanticAddress = semanticAddress;
    }

    /// <summary>
    /// Creates a content-addressed artifact from raw MicroBundle representation bytes.
    /// </summary>
    public static MicroBundleArtifact Create(
        ulong bundleId,
        string version,
        ReadOnlyMemory<byte> content)
    {
        var hash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        return new MicroBundleArtifact(
            new MicroBundleArtifactAddress(bundleId, version, hash),
            content);
    }

    public MicroBundleArtifactAddress Address { get; }

    /// <summary>
    /// Gets the optional semantic ontology address associated with this artifact.
    /// This metadata does not participate in content identity or storage identity.
    /// </summary>
    public OntologyAddress? SemanticAddress { get; }

    /// <summary>Returns a read-only view of the artifact bytes.</summary>
    public ReadOnlyMemory<byte> Content => _content;
}
