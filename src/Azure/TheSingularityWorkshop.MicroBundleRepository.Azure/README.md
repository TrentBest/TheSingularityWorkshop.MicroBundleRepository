# TheSingularityWorkshop.MicroBundleRepository.Azure

**Azure Blob Storage adapter for immutable MicroBundle artifacts.**

## ✳️ 00 — Identity

This package implements the platform-neutral Core repository contract using Azure Blob Storage. Azure is a storage substrate; it is not the repository contract or the Workshop's composition boundary.

## 🟦 01 — The problem and short answer

Hosts need durable storage without making Azure-specific types leak into domain or composition contracts. This adapter maps repository operations to deterministic blob locations and supports identity-only, paginated artifact discovery.

## 🟣 02 — Boundary and dependencies

- Core defines artifact identity and `IMicroBundleRepository`.
- This package implements those contracts using Azure Blob Storage.
- FSM_COS remains outside the repository/storage layer.
- Payload meaning, dependency resolution, and runtime composition are not Azure adapter responsibilities.

Authentication uses Azure Identity's `DefaultAzureCredential`; storage account keys are not part of the repository configuration.

## 🩵 03 — Operations and configuration

- `GetAsync` retrieves one artifact by complete immutable address.
- `PutAsync` stores an immutable artifact at its deterministic content-addressed location.
- `ListAsync` enumerates artifact identities without downloading payload bytes.

Listing uses Azure Blob Storage continuation tokens. Callers must treat tokens as opaque and pass them back unchanged. Optional filters narrow listing by bundle ID and exact version.

The configured identity needs permission to read/list and write blobs in the configured private container. Use an appropriate developer credential locally and managed identity in an Azure-hosted environment where available. See [Azure setup](../../../../docs/AZURE_SETUP.md).

## 🟢 04 — See it in a minute

After configuring the adapter with your storage account/container options and an authenticated Azure identity, use it through the Core contract:

```csharp
IMicroBundleRepository repository = new AzureMicroBundleRepository(options);
var page = await repository.ListAsync(
    new MicroBundleArtifactListRequest(pageSize: 100),
    cancellationToken);
```

This is the adapter usage shape; `options` must be populated for your deployment, and the identity must have the required permissions. A successful empty page means no matching artifact identities were found in the configured artifact prefix. The automated test suite does not claim to prove access to a live Azure account.

From the repository root, run the deterministic tests:

```powershell
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

## 🟪 05 — Further reading

- [Azure setup](../../../../docs/AZURE_SETUP.md) — credentials and storage configuration.
- [Repository architecture](../../../../docs/ARCHITECTURE.md) — artifact identity and delivery boundary.
- [Root README](../../../../README.md) — full ecosystem map and HTTP discovery examples.
- [Workshop documentation standard](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS/blob/development/DOCUMENTATION_STANDARD.md).

No live Azure-backed listing claim is made unless the configured cloud operation has actually been exercised. Verify the exact package/dependency versions before installing a published package.
