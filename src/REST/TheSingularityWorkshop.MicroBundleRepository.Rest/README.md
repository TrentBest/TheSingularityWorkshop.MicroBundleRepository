![The Singularity Workshop animated moniker](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Renderer/development/docs/assets/moniker.svg)

# TheSingularityWorkshop.MicroBundleRepository.Rest

REST delivery for immutable MicroBundle artifacts and their portable revision bindings.

This package uses `TheSingularityWorkshop.FSM_REST` as its transport boundary and implements the platform-neutral repository contracts from `TheSingularityWorkshop.MicroBundleRepository.Core`.

```text
WebForge / host
  -> FSM_REST transport
  -> MicroBundle Repository REST API
  -> IMicroBundleRepository / IMicroBundleIdentityRepository
  -> filesystem or Azure storage adapter
```

The REST package owns transport and repository delivery only. It does not reference MicroBundleDomain or FSM_COS and does not materialize or compose runtime MicroBundles. The composition-facing adapter is provided separately by `TheSingularityWorkshop.MicroBundleRepository.FSM_COS`.

## HTTP surface

- `GET /api/microbundles/{bundleId}/{version}/{contentHash}` — retrieve immutable bytes by repository-local address.
- `PUT /api/microbundles/{bundleId}/{version}/{contentHash}` — legacy address-based write, retained for compatibility.
- `PUT /api/microbundles/identity` — ingest bytes and receive the durable portable identity binding.
- `GET /api/microbundles/identity?ontology=...&version=...&contentHash=...` — resolve a complete portable revision.
- `GET /api/microbundles/identity/{bundleId}` — reverse-resolve a repository-local numeric address.
- `GET /api/microbundles` — read-only paginated artifact catalog.
- `GET /api/experiences/{experienceId}/publication` and `GET /api/experiences/{experienceId}/{version}/{contentHash}` — read-only immutable Experience publication and artifact retrieval.

The MVP wire representation is JSON with base64 artifact bytes. The repository computes and verifies SHA-256; the client validates returned bindings and reads newly published bytes back before reporting success.

## Publisher authorization

MicroBundle and Experience mutations are disabled unless the host has `Repository:WorkshopPublisherKey` configured. The host requires `X-Workshop-Publisher-Key` on mutation requests and fails closed when the configured key is missing. The old generic `X-Repository-Write-Key` header grants no authority.

The REST client accepts the Workshop-issued credential through its optional publisher-key constructor argument. The CLI obtains it from `MICRO_BUNDLE_REPOSITORY_WORKSHOP_PUBLISHER_KEY`; it is never accepted as a command-line argument or printed.

Remote publishing requires HTTPS. Plain HTTP is accepted only for loopback development. The authenticated CLI disables automatic redirects so the credential-bearing request cannot be forwarded to another origin.

The host bounds MicroBundle PUT bodies with `Repository:MaxMicroBundleRequestBytes` (default 16 MiB) and Experience reads with `Repository:MaxExperienceArtifactBytes` (default 10 MiB). Cross-origin browser access is denied by default. Configure `Repository:AllowedCorsOrigins` as an explicit array of trusted origins when needed; do not use a wildcard in production. CORS is separate from authorization.

## Experience retrieval

The REST client package includes `RestExperienceClient` for resolving a publication address and retrieving/locally verifying an immutable Experience artifact through `IRestTransport`. It is read-only: it does not publish/unpublish, parse manifests, or compose bundles.

The development host exposes read-only Experience routes backed by the separate Azure `experiences` container. Local host mode has no local Experience adapter and returns `503 Service Unavailable` for these routes.

See the [REST package release checklist](../../../docs/REST_RELEASE_READINESS.md) for the complete test matrix and release gates.
