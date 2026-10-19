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

MicroBundle writes are disabled by default at the host unless `Repository:WriteApiKey` is configured. When configured, the REST host requires `X-Repository-Write-Key`; pass the same secret as the optional third constructor argument `writeApiKey` to `RestMicroBundleRepository`. Keep this value in deployment secret configuration, never source control. GET requests do not send the write key. The host bounds PUT request bodies with `Repository:MaxMicroBundleRequestBytes` (default 16 MiB). Cross-origin browser access is denied by default. Configure `Repository:AllowedCorsOrigins` as an explicit array of trusted origins (for example, `https://your-webpage.example`) when a browser consumer needs cross-origin access; do not use a wildcard in production. CORS is separate from write authorization.

A later binary endpoint can replace the wire encoding without changing IMicroBundleRepository.

The REST client package also includes `RestExperienceClient` for resolving a publication address and retrieving/locally verifying an immutable Experience artifact through `IRestTransport`. It is read-only and does not publish/unpublish, parse manifests, or compose bundles.

The development host also exposes read-only Experience routes backed by the separate Azure `experiences` container: the publication route returns the complete immutable address, and the artifact route returns verified bytes as base64 JSON. Host contract tests and current-head CI are passing, but end-to-end composition/activation proof remains open. Azure Experience artifact reads enforce `Repository:MaxExperienceArtifactBytes` (default 10 MiB) before and during buffering; the REST response check remains a secondary guard. Local host mode has no local Experience adapter and returns `503 Service Unavailable` for these routes. See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) before treating the full WebPage flow as release-ready.

## Release readiness

The REST package is not yet release-ready for the full WebPage consumer flow. Experience publication lookup and immutable Experience retrieval are wired into the current development host, and host-level contract tests plus current-head CI are passing. Authenticated MicroBundle writes and request-body limits are implemented, while the trusted bundle-version-to-content-hash rule, end-to-end composition/activation proof, CORS policy, and final package compatibility checks remain open. See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) for the complete domain map, security and HTTP contract requirements, test matrix, dependency checks, and release gates.
