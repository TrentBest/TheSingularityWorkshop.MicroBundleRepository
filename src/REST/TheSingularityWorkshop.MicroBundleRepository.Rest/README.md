# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery for immutable MicroBundle artifacts.

The adapter uses **TheSingularityWorkshop.FSM_Rest** as its transport boundary and implements the platform-neutral **IMicroBundleRepository** contract.

```text
WebForge / host
      │
      ▼
FSM_REST transport
      │
      ▼
Repository REST API
      │
      ▼
IMicroBundleRepository
      │
      ▼
Azure Blob / other storage
```

The repository adapter deliberately stops at **artifact delivery**.

It does not:

- implement FSM_COS;
- materialize an assembly into IMicroBundle;
- preload dependency closures;
- interpret manifests;
- arbitrate capabilities;
- own RuntimeAssembly.

Those are composition responsibilities. FSM_COS or another host can consume the verified artifact bytes and decide how to materialize them.

## Endpoints

```text
GET /api/microbundles/{bundleId}/{version}/{sha256}
PUT /api/microbundles/{bundleId}/{version}/{sha256}
```

The MVP wire representation is JSON with base64 artifact bytes. The repository remains content-addressed and verifies the SHA-256 when constructing MicroBundleArtifact.

A later binary endpoint can replace the wire encoding without changing IMicroBundleRepository.

> **The repository delivers the artifact. The composition layer gives the artifact runtime meaning.**