namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>Configuration for an Azure Blob-backed MicroBundle repository.</summary>
public sealed record AzureMicroBundleRepositoryOptions
{
    /// <summary>Azure Blob Storage account URI used by the repository.</summary>
    public required Uri StorageAccountUri { get; init; }

    /// <summary>Blob container used for MicroBundle artifacts. Must be lowercase.</summary>
    public string ContainerName { get; init; } = "microbundles";
}
