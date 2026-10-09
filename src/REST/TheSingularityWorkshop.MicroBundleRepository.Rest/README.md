# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery and discovery for immutable MicroBundle artifacts.

The adapter uses TheSingularityWorkshop.FSM_Rest as its transport boundary and implements the platform-neutral `IMicroBundleRepository` contract.

## Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/microbundles?pageSize=100` | List stored artifact identities |
| GET | `/api/microbundles?bundleId=2110` | List artifacts for one MicroBundle |
| GET | `/api/microbundles?bundleId=2110&version=1.0.0` | List a specific version |
| GET | `/api/microbundles/{bundleId}/{version}/{sha256}` | Retrieve one exact artifact |
| PUT | `/api/microbundles/{bundleId}/{version}/{sha256}` | Store one immutable artifact |

The listing response contains an `items` array of `bundleId`, `version`, and `contentHash` identities plus an optional `continuationToken`. Page sizes must be between 1 and 500. To continue, send the token unchanged with the next request. The list endpoint returns metadata only; it does not include artifact bytes.

The MVP artifact wire representation is JSON with base64 payload bytes. The repository remains content-addressed and verifies the SHA-256 when materializing `MicroBundleArtifact`.

## Architectural boundary

WebForge / host → FSM_REST transport → MicroBundle Repository REST API → `IMicroBundleRepository` → Azure Blob Storage.

Discovery answers **what immutable artifact addresses are stored?** It does not answer whether a bundle is compatible, resolve its dependencies, or compose it. Those remain separate responsibilities.
