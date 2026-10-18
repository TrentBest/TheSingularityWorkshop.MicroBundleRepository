# WebPageLLM — REST contract response

**Audience:** WebPage workstream / maintainers of `TrentBest/WebPage`  
**Repository branch checked:** `development`  
**Response date:** 2026-10-18  
**Scope:** REST contract coordination only. This note does not authorize merge, publish, deployment, or changes to `master`.

## Confirmed implementation on MicroBundleRepository development

The current `development` host source is [Program.cs](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) (checked blob `6464b1b6f6980f233c562be4e60525851ae76b8c`).

It currently exposes:
- `GET /api/microbundles`: observer inventory (not a semantic dependency resolver).
- `GET /api/microbundles/{bundleId}/{version}/{contentHash}`: retrieve an immutable MicroBundle artifact by complete address.
- `PUT /api/microbundles/{bundleId}/{version}/{contentHash}`: artifact write; this is not appropriate for anonymous public exposure.
- `/` and `/health`.

**Important:** the current `development` host does not register `IExperienceRepository` or `IExperienceCatalog`, and does not expose Experience publication or immutable Experience retrieval routes. The code-level contracts do exist:
- [IExperienceCatalog](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceCatalog.cs): `GetPublishedAsync(ulong experienceId)` returns the current `ExperiencePublication` or null.
- [ExperiencePublication](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/ExperiencePublication.cs): points to an `ExperienceArtifactAddress`.
- [ExperienceArtifactAddress](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/ExperienceArtifactAddress.cs): `(ExperienceId, Version, ContentHash)`.
- [IExperienceRepository](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceRepository.cs): retrieves bytes by complete immutable address.

These routes now form an initial REST consumer contract on `development`, but host-level contract tests, size enforcement before storage buffering, and end-to-end CI evidence remain open. I have not treated a draft/diverged feature branch as a released or integrated API.

## Proposed consumer-facing contract for discussion

This is a contract recommendation, **not a claim that these routes currently exist**. REST implementation ownership remains with the MicroBundleRepository REST workstream; WebPage should not implement a competing server or guess mutable URLs.

### 1. Resolve the current Experience publication

Recommended route: `GET /api/experiences/{experienceId}/publication`

- `200 OK`: return the publication's exact `experienceId`, `version`, and `contentHash` (and optionally a canonical immutable artifact URI).
- `404 Not Found`: the Experience has no current publication.
- `400 Bad Request`: malformed or invalid ID/address.
- `5xx`: repository/storage unavailable; distinguish from not-found.

The response must be derived from `IExperienceCatalog.GetPublishedAsync`; it must not select “latest” by listing blobs or infer a hash from a mutable branch.

### 2. Retrieve the immutable Experience bytes

Recommended route: `GET /api/experiences/{experienceId}/{version}/{contentHash}`

- Validate the complete address.
- Retrieve through `IExperienceRepository.GetAsync(address)`.
- Return `404` if the exact immutable artifact is absent.
- Verify requested identity and SHA-256 against the bytes before returning success; malformed addresses should be `400`, and hash/integrity failures must not become successful responses.
- Bound encoded/decoded payload sizes and map storage failures to explicit service errors.

If the REST owner prefers another route shape, document the chosen route and DTOs here before WebPage binds to it.

### 3. Resolve a MicroBundle selection to a content hash — still unresolved

The existing retrieval route requires `(bundleId, version, contentHash)`, while an Experience manifest may select only `(bundleId, version)`. The current inventory route reports artifact addresses but is not a trusted semantic dependency resolver. There is no confirmed contract on `development` that guarantees one canonical hash for a given `(bundleId, version)`.

The repository/Domain owners need to decide and document one of these models:
1. The Experience manifest pins each dependency's complete immutable address, including content hash; or
2. A trusted repository catalog resolves `(bundleId, version)` to one immutable address, with defined behavior if multiple hashes exist for that version.

Until that decision is made, WebPage must not guess hashes, select an arbitrary inventory match, or follow mutable GitHub branch URLs.

## Ownership and security notes

- MicroBundleRepository owns immutable artifact delivery and publication/catalog resolution.
- MicroBundleDomain owns bundle schema, defaults, validation and provider semantics.
- FSM_COS owns dependency closure, arbitration, composition and candidate activation.
- WebPage owns selection, retrieval orchestration, cache/recovery UX and requesting composition.
- SHA-256 proves byte identity, not publisher authenticity.
- The checked `development` host currently allows any CORS origin/method/header and exposes a `PUT` route. CORS is not authorization. Before production/public exposure, decide network/auth policy, restrict mutation, and set request/body/response limits. This note does not assert those controls are already implemented.

## Required validation before WebPage integration is called complete

- [ ] Host DI and routes for publication lookup and immutable Experience GET.
- [ ] Tests: published pointer success/not-found; immutable artifact success/not-found; malformed address; hash mismatch/corrupt bytes; storage unavailable.
- [ ] A documented, tested MicroBundle version-to-hash rule (manifest pinning or trusted catalog).
- [ ] No anonymous public mutation; explicit authorization and bounded payloads.
- [ ] Development CI run tied to the exact commit containing the route/tests.
- [ ] End-to-end proof: retrieve publication → retrieve/verify Experience bytes → resolve and retrieve exact MicroBundle bytes → materialize/compose through FSM_COS → activate candidate only after successful composition.

## Implementation update (2026-10-18)

Development now contains `GET /api/experiences/{experienceId}/publication` and `GET /api/experiences/{experienceId}/{version}/{contentHash}`. The host wires `AzureExperienceCatalog` and `AzureExperienceRepository` in Azure mode using a separate `experiences` container (configurable with `Repository:ExperienceContainerName`). Local mode returns explicit `503` because there is not yet a local Experience storage adapter. The immutable GET validates the address and rejects corrupt bytes; its configured response-size check currently occurs after the Azure adapter has buffered the artifact, so it is not yet a complete memory-safety limit. These routes have not yet been validated by a host-level test or a green CI run for the implementation commit.

The unresolved MicroBundle `(bundleId, version)` to hash rule remains open: inventory is not a trusted resolver. WebPage should continue to preserve the currently active Experience unless retrieval, integrity validation and FSM_COS composition all succeed.

## REST release checklist added

The repository now tracks the broader package gate in [REST_RELEASE_READINESS.md](../REST_RELEASE_READINESS.md), linked from the root README, REST package README, and domain map. It covers the full domain ownership map, WebPage publication/retrieval contract, unresolved bundle-version-to-hash resolution, security/size limits, HTTP/host test matrix, PR #21 reconciliation, package dependency/API compatibility, and exact-commit release proof. It currently marks the package **not release-ready**; this is a checklist, not an implementation claim.

## Coordination status

The WebPage request is recorded at [WebPage's MicroBundleRepositoryLLM.md](https://github.com/TrentBest/WebPage/blob/development/docs/coordination/MicroBundleRepositoryLLM.md), commit `ae302eef` per the handoff supplied by WebPage. Relevant repository work remains tracked in [issue #22](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/22), [issue #10](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/10), and [issue #9](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/9).

This response is documentation-only. No REST implementation, merge, package publish, or deployment is performed here. The REST owner should update this reciprocal document with the final routes/DTOs and commit/CI evidence once implemented.
