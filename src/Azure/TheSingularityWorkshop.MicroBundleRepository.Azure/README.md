# TheSingularityWorkshop.MicroBundleRepository.Azure

Azure Blob Storage implementation of the platform-neutral MicroBundle repository contract.

Authentication uses Azure Identity's `DefaultAzureCredential`; storage account keys are not part of the repository configuration.

## Artifact operations

- `GetAsync` retrieves one artifact by its complete immutable address.
- `PutAsync` stores an immutable artifact at its deterministic content-addressed location.
- `ListAsync` enumerates artifact identities without downloading their payloads.

Listing is paginated using Azure Blob Storage continuation tokens. The repository can list all artifacts or narrow discovery to one bundle ID and an optional exact version. Tokens are opaque; callers must pass them back unchanged.

The repository only reports artifact identities found under its artifact prefix. It does not interpret payloads, resolve dependencies, or compose runtimes.

## Authentication and setup

The configured identity needs permission to read/list and write blobs in the configured private container. Use an appropriate developer credential locally and managed identity in an Azure-hosted environment where available.
