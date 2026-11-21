namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Request to ingest artifact bytes under a portable ontology revision identity.</summary>
public sealed record MicroBundleIdentityWriteDto(
    string Ontology,
    string Version,
    string ContentBase64);

/// <summary>Portable identity and repository-local binding returned by the REST API.</summary>
public sealed record MicroBundleIdentityBindingDto(
    string Ontology,
    string Version,
    string ContentHash,
    ulong BundleId)
{
    /// <summary>Creates a wire response from a validated repository binding.</summary>
    public static MicroBundleIdentityBindingDto From(TheSingularityWorkshop.MicroBundleRepository.Core.MicroBundleIdentityBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new MicroBundleIdentityBindingDto(
            binding.Identity.Ontology,
            binding.Identity.Version,
            binding.Identity.ContentHash,
            binding.Address.BundleId);
    }
}
