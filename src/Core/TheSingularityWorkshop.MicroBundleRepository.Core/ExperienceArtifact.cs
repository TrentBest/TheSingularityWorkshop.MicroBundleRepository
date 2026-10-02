using System.Security.Cryptography;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable Experience bytes together with their content identity.
/// </summary>
public sealed class ExperienceArtifact
{
    private readonly byte[] _content;

    /// <summary>
    /// Creates an artifact and verifies its declared SHA-256 identity.
    /// </summary>
    public ExperienceArtifact(
        ExperienceArtifactAddress address,
        ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(address);

        _content = content.ToArray();

        var actualHash = Convert.ToHexString(
            SHA256.HashData(_content)).ToLowerInvariant();

        if (!string.Equals(
                actualHash,
                address.ContentHash,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The supplied content does not match the artifact content hash.",
                nameof(content));
        }

        Address = address;
    }

    /// <summary>Gets the complete artifact address.</summary>
    public ExperienceArtifactAddress Address { get; }

    /// <summary>Returns a read-only view of the artifact bytes.</summary>
    public ReadOnlyMemory<byte> Content => _content;
}
