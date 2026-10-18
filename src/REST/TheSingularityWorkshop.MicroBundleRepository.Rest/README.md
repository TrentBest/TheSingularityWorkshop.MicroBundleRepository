# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery for immutable MicroBundle artifacts.

The adapter uses TheSingularityWorkshop.FSM_Rest as its transport boundary and implements the platform-neutral IMicroBundleRepository contract.

WebForge / host -> FSM_REST transport -> MicroBundle Repository REST API -> IMicroBundleRepository -> Azure Blob Storage

Endpoint:

GET /api/microbundles/{bundleId}/{version}/{sha256}
PUT /api/microbundles/{bundleId}/{version}/{sha256}

The MVP wire representation is JSON with base64 artifact bytes. The repository remains content-addressed and verifies the SHA-256 when materializing MicroBundleArtifact.

A later binary endpoint can replace the wire encoding without changing IMicroBundleRepository.

## Release readiness

The REST package is not yet release-ready for the full WebPage consumer flow. Experience publication lookup and immutable Experience retrieval are not wired into the current development host, and the bundle-version-to-content-hash resolution rule remains open. See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) for the complete domain map, security and HTTP contract requirements, test matrix, dependency checks, and release gates.
