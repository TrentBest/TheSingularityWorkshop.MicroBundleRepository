# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery for immutable MicroBundle artifacts.

This package is a **repository transport adapter**. It implements the platform-neutral
`IMicroBundleRepository` contract over `TheSingularityWorkshop.FSM_Rest`.

It deliberately does **not** know about:

- `FSM_COS`
- `IMicroBundle`
- dependency arbitration or closure
- runtime composition
- assembly loading
- artifact materialization

Those concerns belong on the composition/host side, after an opaque
`MicroBundleArtifact` has been retrieved and verified.

## Boundary

```text
Composition / Host
       │
       │ requests an opaque artifact
       ▼
IMicroBundleRepository
       │
       ▼
MicroBundleRepository.Rest
       │
       ▼
FSM_Rest transport
       │
       ▼
Repository API / durable storage
```

The REST adapter neither interprets nor executes the artifact bytes. It retrieves
and stores the immutable artifact identified by:

```text
bundleId + version + SHA-256 content hash
```

## Endpoints

```text
GET /api/microbundles/{bundleId}/{version}/{sha256}
PUT /api/microbundles/{bundleId}/{version}/{sha256}
```

The MVP wire representation is JSON with base64-encoded artifact bytes. The
`MicroBundleArtifact` boundary verifies the content hash when the artifact is
constructed.

A later binary wire representation can replace the JSON encoding without changing
`IMicroBundleRepository`.

## Composition boundary

A host such as AnyApp may combine this repository adapter with its own artifact
materializer and `FSM_COS` composition pipeline:

```text
REST repository → verified artifact bytes → host materializer → FSM_COS
```

Keeping that final arrow outside this package prevents the repository from becoming
a runtime composition system.
