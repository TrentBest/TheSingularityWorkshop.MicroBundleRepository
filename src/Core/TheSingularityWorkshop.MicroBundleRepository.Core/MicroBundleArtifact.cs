using System.Security.Cryptography;

namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Immutable MicroBundle bytes together with their content identity.
/// </summary>
public sealed class MicroBundleArtifact
{
    private readonly byte[] _content;

    public MicroBundleArtifact(MicroBundleArtifactAddress address, ReadOnlyMemory<byte> content)
    {
        ArgumentNullException.ThrowIfNull(address.Version);
        ArgumentNullException.ThrowIfNull(address.ContentHash);

        _content = content.ToArray();

        var actualHash = Convert.ToHexString(SHA256.HashData(_content)).ToLowerInvariant();
        if (!string.Equals(actualHash, address.ContentHash, StringComparison.Ordinal))
            throw new ArgumentException("The supplied content does not match the artifact content hash.", nameof(content));

        Address = address;
    }

    public MicroBundleArtifactAddress Address { get; }

    /// <summary>Returns a read-only view of the artifact bytes.</summary>
    public ReadOnlyMemory<byte> Content => _content;
}
