namespace TheSingularityWorkshop.MicroBundleRepository.Azure;

/// <summary>Configuration for an Azure Blob-backed MicroBundle repository.</summary>
public sealed record AzureMicroBundleRepositoryOptions
{
    public required Uri StorageAccountUri { get; init; }

    /// <summary>Blob container used for MicroBundle artifacts. Must be lowercase.</summary>
    public string ContainerName { get; init; } = "microbundles";
}
