namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// A durable repository-local numeric address bound to one portable immutable revision.
/// </summary>
public readonly record struct MicroBundleIdentityBinding
{
    public MicroBundleIdentityBinding(
        MicroBundleArtifactIdentity identity,
        MicroBundleArtifactAddress address)
    {
        if (address.BundleId == 0)
            throw new ArgumentOutOfRangeException(nameof(address));

        if (!StringComparer.Ordinal.Equals(identity.Version, address.Version) ||
            !StringComparer.OrdinalIgnoreCase.Equals(identity.ContentHash, address.ContentHash))
        {
            throw new ArgumentException(
                "The local address version and content hash must exactly match the portable identity.",
                nameof(address));
        }

        Identity = identity;
        Address = address;
    }

    public MicroBundleArtifactIdentity Identity { get; }
    public MicroBundleArtifactAddress Address { get; }
}
