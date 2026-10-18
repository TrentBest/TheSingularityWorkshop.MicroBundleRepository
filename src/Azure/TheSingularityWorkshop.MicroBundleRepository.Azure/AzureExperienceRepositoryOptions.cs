namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>Configuration for an Azure Blob-backed Experience repository.</summary>
public sealed record AzureExperienceRepositoryOptions
{
    /// <summary>Gets the Azure Storage account URI.</summary>
    public required Uri StorageAccountUri { get; init; }

    /// <summary>Gets the Blob container used for published Experiences.</summary>
    public string ContainerName { get; init; } = "experiences";

    /// <summary>
    /// Gets the maximum number of bytes accepted when reading an Experience artifact.
    /// Defaults to 10 MiB. Values must be greater than zero.
    /// </summary>
    public long MaxArtifactBytes { get; init; } = 10 * 1024 * 1024;
}
