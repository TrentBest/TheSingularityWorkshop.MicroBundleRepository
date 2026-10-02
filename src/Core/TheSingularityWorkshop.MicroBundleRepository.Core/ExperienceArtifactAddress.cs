namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Complete semantic, version, and content address for a published Experience artifact.
/// </summary>
public sealed record ExperienceArtifactAddress
{
    /// <summary>
    /// Creates a complete content-addressed Experience identity.
    /// </summary>
    public ExperienceArtifactAddress(
        ulong experienceId,
        string version,
        string contentHash)
    {
        if (experienceId == 0)
            throw new ArgumentOutOfRangeException(nameof(experienceId));

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (version.Contains('/') || version.Contains('\\'))
        {
            throw new ArgumentException(
                "Version must be a single storage-safe path segment.",
                nameof(version));
        }

        if (contentHash.Length != 64 || contentHash.Any(c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException(
                "Content hash must be a 64-character hexadecimal SHA-256 value.",
                nameof(contentHash));
        }

        ExperienceId = experienceId;
        Version = version;
        ContentHash = contentHash.ToLowerInvariant();
    }

    /// <summary>Gets the stable Experience identity.</summary>
    public ulong ExperienceId { get; }

    /// <summary>Gets the explicit artifact version.</summary>
    public string Version { get; }

    /// <summary>Gets the lowercase SHA-256 content identity.</summary>
    public string ContentHash { get; }

    /// <summary>Gets the deterministic blob path for this artifact.</summary>
    public string BlobName =>
        $"artifacts/{ExperienceId}/{Version}/{ContentHash}.experience";
}
