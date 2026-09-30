namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>Configuration for an Azure Blob-backed Experience repository.</summary>
public sealed record AzureExperienceRepositoryOptions
{
    /// <summary>Gets the Azure Storage account URI.</summary>
    public required Uri StorageAccountUri { get; init; }

    /// <summary>Gets the Blob container used for published Experiences.</summary>
    public string ContainerName { get; init; } = "experiences";
}
