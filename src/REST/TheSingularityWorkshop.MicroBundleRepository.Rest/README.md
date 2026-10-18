![The Singularity Workshop animated moniker](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Renderer/development/docs/assets/moniker.svg)

# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery for immutable MicroBundle artifacts.

The adapter uses TheSingularityWorkshop.FSM_Rest as its transport boundary and implements the platform-neutral IMicroBundleRepository contract.

WebForge / host -> FSM_REST transport -> MicroBundle Repository REST API -> IMicroBundleRepository -> Azure Blob Storage

Endpoint:

GET /api/microbundles/{bundleId}/{version}/{sha256}
PUT /api/microbundles/{bundleId}/{version}/{sha256}
GET /api/experiences/{experienceId}/publication
GET /api/experiences/{experienceId}/{version}/{sha256}

The MVP wire representation is JSON with base64 artifact bytes. The repository remains content-addressed and verifies the SHA-256 when materializing MicroBundleArtifact.

A later binary endpoint can replace the wire encoding without changing IMicroBundleRepository.

The REST client package also includes `RestExperienceClient` for resolving a publication address and retrieving/locally verifying an immutable Experience artifact through `IRestTransport`. It is read-only and does not publish/unpublish, parse manifests, or compose bundles.

The development host also exposes read-only Experience routes backed by the separate Azure `experiences` container: the publication route returns the complete immutable address, and the artifact route returns verified bytes as base64 JSON. These routes are newly added and do not yet have host-level contract tests or end-to-end CI evidence. Azure Experience artifact reads enforce `Repository:MaxExperienceArtifactBytes` (default 10 MiB) before and during buffering; the REST response check remains a secondary guard. Local host mode has no local Experience adapter and returns `503 Service Unavailable` for these routes. See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) before treating the full WebPage flow as release-ready.

## Release readiness

The REST package is not yet release-ready for the full WebPage consumer flow. Experience publication lookup and immutable Experience retrieval are wired into the current development host, but host-level contract tests, exact-commit CI evidence, and the bundle-version-to-content-hash resolution rule remain open. See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) for the complete domain map, security and HTTP contract requirements, test matrix, dependency checks, and release gates.
