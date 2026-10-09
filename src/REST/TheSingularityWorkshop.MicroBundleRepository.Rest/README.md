# TheSingularityWorkshop.MicroBundleRepository.Rest

**HTTP transport adapter for MicroBundle artifact storage and discovery.**

## ✳️ 00 — Identity

This package exposes repository operations over HTTP and implements the platform-neutral `IMicroBundleRepository` contract. It provides transport, not domain interpretation or runtime composition.

## 🟦 01 — The problem and short answer

A host may need to use repository storage across a process or deployment boundary. The REST adapter lets a client list artifact identities and retrieve or store an artifact without depending directly on the storage SDK.

## 🟣 02 — Boundary and dependencies

The intended path is:

`host → FSM_REST transport → repository REST API → IMicroBundleRepository → storage adapter`

Core owns artifact identity. This package owns HTTP request/response mapping. Azure storage implementation and FSM_COS composition are separate choices. Do not infer that an artifact is compatible or executable merely because it appears in an inventory response.

## 🩵 03 — Endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/microbundles?pageSize=100` | List stored artifact identities |
| GET | `/api/microbundles?bundleId=2110` | List artifacts for one MicroBundle |
| GET | `/api/microbundles?bundleId=2110&version=1.0.0` | List a specific version |
| GET | `/api/microbundles/{bundleId}/{version}/{sha256}` | Retrieve one exact artifact |
| PUT | `/api/microbundles/{bundleId}/{version}/{sha256}` | Store one immutable artifact |

The listing response contains an `items` array with `bundleId`, `version`, and `contentHash`, plus an optional `continuationToken`. Page sizes are bounded from 1 through 500. Tokens are opaque and should be passed back unchanged. Listing does not include artifact payload bytes.

The MVP artifact wire representation is JSON with base64 payload bytes. The repository verifies SHA-256 when materializing `MicroBundleArtifact`.

## 🟢 04 — See it in a minute

With a compatible REST host running, ask for a page of artifact identities:

```http
GET /api/microbundles?pageSize=100
```

The expected response shape is a JSON object containing `items` and an optional `continuationToken`. Each item describes an artifact identity; it is not the artifact's payload. An empty `items` array is valid if the repository has no matching artifacts.

Run the adapter tests from the repository root:

```powershell
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

The test suite validates the client/contract behavior; it does not, by itself, prove a production deployment or live Azure connectivity.

## 🟪 05 — Further reading

- [Root README](../../../../README.md) — Workshop context and full repository boundary.
- [Repository architecture](../../../../docs/ARCHITECTURE.md) — separation of discovery, retrieval, and composition.
- [FSM_COS integration package](../TheSingularityWorkshop.MicroBundleRepository.FSM_COS/README.md) — optional composition-side adapter.
- [Workshop documentation standard](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS/blob/development/DOCUMENTATION_STANDARD.md).

Check the REST adapter's declared FSM_REST/FSM_COS dependency versions against the versions available on NuGet before treating this package as install-ready.
