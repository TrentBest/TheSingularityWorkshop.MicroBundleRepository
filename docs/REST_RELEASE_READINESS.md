# REST package release readiness

> **Current candidate checkpoint: 2026-11-20.** The implementation is the three-PR stack [#39](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/39) → [#40](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/40) → [#41](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/41). PR #39 is based on `development` at `7dbd0ed2e03b817d46d76ba8334e34a2cb43195b`; PR #40 is stacked on #39; PR #41 is stacked on #40. The latest validated code head is `e0acab0e96464143c47ee991eea2e1225e6d15ce`. Subsequent commits on the PR #41 branch refresh the README, readiness, and branch audit; those documentation-only follow-ons have separate workflow runs. None of the stack is integrated into `development`.

**Current assessment: approximately 75% implementation/release readiness — not release-ready.** This is a reasoned engineering estimate, not test coverage. The candidate has durable portable-revision bindings, Workshop-authenticated REST mutations, and an identity-aware publisher CLI. It now also requires HTTPS for remote publishing and disables redirects on the credential-bearing transport. The remaining work is dominated by integration, operational, and release-policy proof rather than missing basic repository features.

## Current candidate evidence

| Area | Candidate evidence | Status |
|---|---|---|
| Durable portable identity | PR #39 maps `(ontology, exact version, SHA-256)` to a non-zero repository-local numeric ID in both directions. Existing numeric `MicroBundleArtifactAddress`, observer, and paginated catalog surfaces are retained. | Implemented in candidate; review/integration pending |
| Domain/runtime identity seam | The repository binding resolves a portable revision to a repository-local storage address. It does **not** yet define how that address relates to `MicroBundleDescriptor.Id` / `MicroBundleDependencyRequest.BundleId` in MicroBundleDomain and FSM_COS. [MicroBundleDomain issue #28](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/28) remains open. | Cross-repository contract blocker; do not conflate storage IDs with runtime IDs |
| Filesystem persistence | Exclusive cross-process lock, immutable-byte verification, and fail-closed corrupt-index behavior. Parent-directory fsync is not implemented; the candidate does not claim immunity to all sudden-power-loss metadata failures. | Tested; documented limitation |
| Azure persistence | ETag-conditional index writes and per-ID conditional reservation blobs coordinate identity allocation. | Azurite-tested; live account migration not proven |
| REST identity API and authorization | PR #40 adds ingest, forward resolve, and reverse resolve routes. All mutations require `Repository:WorkshopPublisherKey` / `X-Workshop-Publisher-Key`; the old generic write-key header no longer authorizes writes. Missing server configuration fails closed. | Implemented and host-tested |
| Publisher CLI | PR #41 adds `publish <ontology> <version> <input-file>`; the repository allocates the local ID. The Workshop credential comes from `MICRO_BUNDLE_REPOSITORY_WORKSHOP_PUBLISHER_KEY`, is not a command-line argument, and is sent only on mutation. The client verifies the returned identity and reads the artifact back. | Implemented and process-tested |
| Standard build/test | [Build and Test #44874329653](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/44874329653): **150/150 standard and 152/152 source-integrated tests passed** on exact code head `e0acab0e96464143c47ee991eea2e1225e6d15ce`. | Passed on exact code head; later docs-only commits are validated separately |
| Source-integrated build/test | Same workflow: **152/152 source-integrated tests passed**, including the source-integrated dependency path. | Passed on code head; docs-only successor needs fresh CI |
| Package verification | [Package #44874329661](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/44874329661): verification, build, tests, and Core/Azure/REST package packing passed. Publish job was skipped; **no NuGet package was published**. | Passed on code head; docs-only successor needs fresh CI |
| Branch policy | The live repository currently has 15 branches, not exactly `master` + `development`. | Blocking Sith policy violation |

The standard test matrix at the latest validated code head reports Core 43, Local 26, Azure 29, REST client 20, REST Host 26, and CLI 6: **150/150 passed**. The source-integrated matrix reports Core 43, Local 26, Azure 29, REST client 22, REST Host 26, and CLI 6: **152/152 passed**. The two additional CLI process tests cover rejection of remote HTTP publishing and refusal to follow a redirect while carrying the Workshop credential. The Azure identity integration tests use Azurite; these results do not establish a live production-account migration or mixed-version writer safety.

## Package publication posture

This is currently a **first-publication package line**, not an update to an already-published MicroBundleRepository package:

| Package | Source version | Target | NuGet status |
|---|---|---|---|
| `TheSingularityWorkshop.MicroBundleRepository.Core` | `0.1.0-alpha.1` | .NET 10 | Not listed in the publisher's NuGet profile |
| `TheSingularityWorkshop.MicroBundleRepository.Azure` | `0.1.0-alpha.1` | .NET 10 | Not listed in the publisher's NuGet profile |
| `TheSingularityWorkshop.MicroBundleRepository.Rest` | `0.1.0-alpha.2` | .NET 10 | Not listed in the publisher's NuGet profile |

The [publisher's NuGet profile](https://www.nuget.org/profiles/TheSingularityWorkshop) lists the published MicroBundleDomain, FSM_REST, and FSM_COS dependencies, but does not list these MicroBundleRepository package IDs. Treat them as unpublished until a direct package/version lookup proves otherwise. The successful package workflow proves packing only, not publication.

MicroBundleRepository consumes the already-published `MicroBundleDomain 1.0.1`, `FSM_REST 0.1.0-alpha.4`, and `FSM_COS 0.1.0-alpha.6` package versions in its package-based build; its source-integration lane separately validates the current source checkouts. Before the first repository package publication, finalize the intended alpha versions, verify clean external consumer restore/build, align the release branch according to the repository's package-aware release policy, and satisfy Sith. No publication is authorized by this readiness record.

## Remaining release blockers

1. **Integrate the reviewed stack into `development`.** The PRs are intentionally stacked. Retarget and validate each dependent PR only after its prerequisite is integrated; require fresh exact-head checks after any base/head change. Do not merge a later stack layer independently of its prerequisites.
2. **Prove clean consumer/package compatibility.** Run the package workflow against the exact candidate and test consumption from a clean external host using the published dependency versions. The REST project currently references published `MicroBundleDomain 1.0.1`, `FSM_REST 0.1.0-alpha.4`, and `FSM_COS 0.1.0-alpha.6`. Domain `1.0.1` is published for .NET 8; current Domain `development` source targets .NET 10. Standard package-based validation and source-integrated validation are distinct evidence and both must remain green.
3. **Resolve the Domain/runtime identity seam.** PR #39 maps `(ontology, exact version, SHA-256)` to a repository-local storage address; it does not, by itself, map that address to `MicroBundleDescriptor.Id` or `MicroBundleDependencyRequest.BundleId`. MicroBundleDomain issue [#28](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/28) must define whether the descriptor ID is a stable runtime identity distinct from the repository-local storage key, how authored portable references reach the descriptor, and how dependency closure crosses that boundary. Keep these identities explicitly distinct until the contract and tests prove the mapping. Experiences/FSM_COS must then demonstrate portable revision retrieval while retaining valid numeric runtime references. Do not put repository-local numeric IDs into authored Experience manifests.
4. **Complete deployment security/operations proof.** Verify trusted origin configuration, request-size enforcement (including chunked requests), secret provisioning/rotation, and operational failure behavior in the actual deployment configuration. CORS is not authorization.
5. **Complete live Azure operational validation.** Azurite covers deterministic integration behavior, not production storage account migration. Before enabling the new concurrent identity writer protocol, quiesce or migrate older Azure writers that do not participate in per-ID reservations.
6. **Satisfy Sith branch policy.** Reconcile all 15 current repository branches, preserve unique work, and have the owner remove surplus branches in one audited batch. No branch deletion is performed by this readiness work.
7. **Keep final release authority separate.** Build, test, pack, branch equality, and green CI do not authorize NuGet publication, release, or deployment. The package publish job remains disabled/skipped until the owner explicitly authorizes publication.

## Current disposition

**Candidate status: technically promising, integration pending, not release-ready.** The highest-value next engineering action is to review and integrate the #39 → #40 → #41 stack in dependency order, then refresh all exact-head validation. The paginated discovery work in [PR #21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) and older publisher work in [PR #17](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/17) remain separate reconciliation surfaces; do not merge either wholesale without checking for overlap with the newer stack.

**Release controls:** no NuGet publication, release, deployment, force-push, `master` rewrite, or branch deletion is part of this checkpoint.

---

> **Historical checkpoint: 2026-11-10.** Inspected `development` at `8ca9c887497fc542ca9595d664da967eaadd546b`. The authenticated Experience artifact upload, publication-pointer update, unpublish routes, request limits, host tests, and API documentation are present. Exact-head [Build and Test passed](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/42769838572); [Package verification passed with publication skipped](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/42769838647). [Sith Policy failed](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/42769838579): the repository still has branches beyond `master` and `development`, and the workflow's generated evidence update could not fast-forward `master`. [CiAi Repository Observation failed](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/42769838738) because its embedded CiAi build cannot resolve `CommandInstruction` in `AiCiRepositoryBuilder.cs:37`; that is separate from the REST host build. No merge, force-push, branch deletion, or package publication was performed. These runs verify the REST code/tests/package build, not deployment, live Azure, or end-to-end consumer activation.

**Estimated release readiness: 60% — not release-ready.** This is a judgment-based assessment of the remaining production and consumer proof, not a test-coverage percentage. The current development solution builds, tests, and packs successfully, and the local filesystem adapter and persistent cache are present with deterministic-address, integrity, missing-artifact, cancellation, and cache-recovery tests. That is meaningful progress, but green CI does not close the contract and deployment gates. Remaining blockers are deterministic immutable dependency resolution across MicroBundleDomain/Repository/FSM_COS/WebPage; deployment origin/authentication and request-limit verification; live Azure evidence where credential-gated tests have not been shown to execute; end-to-end retrieval → verification → composition → activation proof; and exact package/dependency compatibility from a clean consumer. The paginated artifact-discovery implementation remains in draft [PR #21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) and is not treated as integrated merely because the branch contains it. Package source versions are not proof of publication. NuGet publication remains unauthorized and the package workflow's publish condition remains hard-disabled.

The 2026-10-19 checklist checkpoint and earlier workflow links below are retained as historical evidence. They must not be read as the current branch state.
## 1. Domain map: ownership and boundaries

| Domain concern | Owner | REST package responsibility | Do not move into REST |
|---|---|---|---|
| Capability identity, schema, defaults, providers and dependency declarations | [MicroBundleDomain](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain) | Transport the declared identity/manifest without redefining its semantics | Authoritative schema/default policy |
| Immutable MicroBundle bytes | MicroBundleRepository Core + storage adapters | Expose exact-address retrieval and authorized write operations; preserve identity and integrity errors | Guessing a hash or silently selecting a different version |
| Artifact inventory / pagination | Repository observer/catalog contract | Expose inventory as inventory, with explicit page/filter semantics | Treating inventory as a semantic dependency resolver |
| Immutable Experience bytes | Core `IExperienceRepository` + storage adapter | Expose retrieval by complete `(experienceId, version, contentHash)` after host wiring | Owning Experience behavior or composition |
| Current Experience publication | Core `IExperienceCatalog` + storage adapter | Expose a read-only publication lookup for consumers | Selecting “latest” by listing blobs or mutating publication from public read routes |
| Dependency closure, materialization policy, arbitration and RuntimeAssembly | FSM_COS / composition integration | Keep transport DTOs and errors suitable for the composition-side caller; coordinate seam placement | Making REST the composition engine |
| Candidate activation, active Experience retention, user-facing recovery | WebPage / consumer host | Return explicit outcomes so consumers can preserve the current active Experience when a candidate fails | Host UI state and activation policy |
| Physical persistence | Local/Azure repository adapters | Delegate through Core contracts; preserve cancellation and failure semantics | Azure-specific paths in consumer code |
| Authentication, authorization, limits and operational policy | REST host/deployment boundary | Enforce and document the chosen policy consistently | Assuming CORS is authorization |

Architecture invariant: **MicroBundleDomain = What; MicroBundleRepository = Where; FSM_COS = How; WebPage = consumer/experience.** REST is a transport boundary, not a second domain or composition layer.

## 2. Current implementation inventory

Evidence inspected:
- [REST host Program.cs on development](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs)
- [REST client package README](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/README.md)
- [REST client tests](../../tests/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Tests/RestMicroBundleRepositoryTests.cs)
- [Core domain map](DOMAIN_MAP.md)
- [REST package project/version/dependencies](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/TheSingularityWorkshop.MicroBundleRepository.Rest.csproj)

| Capability | Current evidence on `development` | Release assessment |
|---|---|---|
| Exact MicroBundle GET by `(bundleId, version, contentHash)` | Host route and REST client exist; client tests cover successful materialization, 404, PUT and identity mismatch | Partial; add host-level HTTP contract and corruption/error mapping tests |
| MicroBundle PUT | Host route and client method exist; base64 DTO and Core artifact verify hash | Not production-ready until authentication/authorization, size bounds, collision behavior and failure mappings are proven |
| MicroBundle inventory | Host `GET /api/microbundles` returns observer inventory | This development route is an unpaginated observation list; the separate draft PR #21 changes the contract to paginated identity discovery. Do not describe that candidate as integrated/released |
| Experience publication lookup and mutation | Development host exposes GET/PUT/DELETE `/api/experiences/{experienceId}/publication`; PUT requires an existing integrity-verified immutable artifact; DELETE removes only the pointer. Host tests cover read, publication ordering, and unpublish retention. | Exact-head host tests pass in Build and Test; concurrent authorized publishers are last-write-wins (no compare-and-swap); end-to-end evidence missing; local mode returns 503 |
| Immutable Experience GET/PUT | Development host exposes exact-address GET and authenticated PUT; PUT verifies SHA-256, persists, then reads back and compares bytes. Host tests cover exact GET, missing artifact, malformed hash, integrity failure, upload read-back and hash mismatch. | Exact-head Build and Test passes upload/read-back and hash-mismatch coverage; chunked request-size enforcement and live Azure behavior remain open |
| MicroBundle `(bundleId, version)` → trusted hash | Inventory can show stored addresses; no canonical selection rule is documented | Unresolved domain contract |
| Error behavior | Basic invalid address and not-found handling exist on MicroBundle GET | Incomplete; storage failures, integrity failures, invalid payloads and service unavailability need stable mappings |
| Security/limits | PUT fails closed without `Repository:WriteApiKey`; configured key is compared in fixed time; declared oversized authorized request test returns `413`; opt-in CORS is covered by passing host tests | CORS and declared-length request rejection are verified in CI; chunked request enforcement relies on server feature and needs deployment-level verification; deployment must configure trusted origins explicitly |
| Release validation | Code checkpoint `6358720` passed all 52 tests across four test assemblies and source integration. Current documentation head `d735fa7` also passed both required workflows. | Evidence: [current Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321974469) and [current Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321974465). End-to-end proof and final package compatibility remain open |

The REST package project currently declares version `0.1.0-alpha.2`, references `FSM_REST 0.1.0-alpha.4`, `FSM_COS 0.1.0-alpha.6`, and `MicroBundleDomain 1.0.1`. These are source declarations only. Before release, verify each dependency exists on NuGet and is API-compatible with the exact commit being packed.

**Boundary warning:** the REST client project currently references FSM_COS and contains `IMicroBundleArtifactMaterializer` / `RestMicroBundleCatalog` composition-facing types. Review whether these belong in a separate composition integration assembly so the transport adapter does not pull composition policy into the REST package. Do not break consumers blindly; document the dependency graph and stage any split with compatibility tests.

## 3. WebPage consumer contract required

WebPage's request is recorded in [WebPage's MicroBundleRepositoryLLM.md](https://github.com/TrentBest/WebPage/blob/development/docs/coordination/MicroBundleRepositoryLLM.md); the reciprocal response is [WebPageLLM.md](coordination/WebPageLLM.md). These are requirements and coordination records, not proof of implemented routes.

### 3.1 Resolve the current publication pointer

Proposed route: `GET /api/experiences/{experienceId}/publication`

- `200 OK`: return the exact published address: `experienceId`, `version`, `contentHash` (and optionally a canonical immutable artifact URI).
- `404 Not Found`: the Experience has no current publication.
- `400 Bad Request`: malformed identifier/address.
- `5xx`: storage/service failure; never disguise it as “not published”.
- The answer must come from `IExperienceCatalog.GetPublishedAsync`, not “largest version”, blob enumeration, or a mutable source-control URL.

### 3.2 Retrieve immutable Experience bytes

Proposed route: `GET /api/experiences/{experienceId}/{version}/{contentHash}`

- Validate the full address before storage access.
- Retrieve only the exact immutable artifact through `IExperienceRepository.GetAsync`.
- Verify the returned identity and SHA-256 before returning success.
- Return `404` for an absent exact artifact; use a client error for malformed address and a service/integrity error for corruption or storage failure.
- Keep response DTOs/versioning documented. Bound decoded bytes, encoded payloads, request body and memory consumption. Azure Experience artifact reads now use a configurable pre-buffer/streaming byte cap; MicroBundle HTTP payloads and total request-body limits remain open.

### 3.3 Resolve every selected MicroBundle deterministically

**Cross-repository contract finding (2026-10-29):** the current public shapes do not yet express a complete dependency pin. `MicroBundleDomain.MicroBundleDependency` carries only `BundleId`; `FSM_COS.RuntimeManifestDependency` carries only `(BundleId, DependencyId)`; `MicroBundleManifestEntry` adds a requested version for a root entry, while `MicroBundleReference` can carry `(BundleId, Version, ContentHash)`. Repository retrieval requires the full `(BundleId, Version, SHA-256)` address. Therefore a dependency edge alone cannot state its required version or hash, and the repository cannot infer a canonical address from that edge without a separately defined trusted resolution policy. This is a real contract gap, not merely missing REST plumbing. Keep Domain/Repository ontology-agnostic and do not let the REST package own composition policy.

The existing exact retrieval route requires `(bundleId, version, contentHash)`, while a selection may identify only `(bundleId, version)`. The current `RestMicroBundleCatalog` receives a caller-supplied `IReadOnlyDictionary<ulong, MicroBundleArtifactAddress>` and recursively follows dependency bundle IDs through that map; it does not resolve addresses from inventory or prove that each dependency's requested version/hash matches a manifest pin. The preload operation now stages newly materialized bundles and commits the closure only after all dependencies succeed, preventing a failed preload from leaving a partially resolvable candidate in the resident catalog. Thus deterministic behavior currently depends on the caller providing a complete, correct address map for the whole dependency closure. Resolve and test this before WebPage integration is called complete. Choose and test one of:

1. **Manifest pinning:** every Experience pins each dependency's complete immutable address, including SHA-256; or
2. **Trusted catalog resolution:** repository catalog returns one canonical address for a bundle/version and explicitly rejects ambiguous versions with multiple hashes.

Do not let WebPage guess hashes, select an arbitrary inventory match, or follow mutable branch URLs. If both models are needed, specify precedence and conflict behavior.

### 3.4 Preserve the active Experience on failure

REST must expose distinguishable outcomes for unpublished/not found, malformed address, integrity mismatch, unavailable storage/service, and authorization failure. The consumer must be able to retrieve and verify a candidate first, compose it through FSM_COS, and activate only after successful composition. A failed candidate must not force the host to discard its current active Experience.

## 4. Release checklist — implementation, proof, documentation

### A. Freeze the public contract
- [ ] Decide the stable route names, HTTP verbs, DTOs, content types and API compatibility/versioning policy.
- [ ] Specify all status codes and error response shape for malformed IDs, missing pointer/artifact, hash mismatch/corrupt bytes, ambiguous dependency address, unauthorized writes, storage outage, cancellation and oversized requests.
- [ ] Decide whether inventory is observer-only or a supported discovery API; keep inventory distinct from semantic dependency resolution.
- [ ] Decide the canonical `(bundleId, version)` to hash rule (manifest pinning or trusted resolver).
- [ ] Define publication update concurrency semantics and cache freshness/ETag or equivalent policy if consumers need it.

### B. Implement the missing Experience endpoints
- [x] Register `IExperienceCatalog` and `IExperienceRepository` in Azure mode; local mode explicitly reports 503 because no local Experience adapter exists.
- [x] Add publication lookup and immutable Experience GET routes on `development`.
- [x] Validate address shape and verify bytes through `ExperienceArtifact` before success.
- [x] Add initial host-level contract tests for publication lookup, unpublished response, immutable artifact bytes, malformed hash, missing artifact, and invalid Experience ID.
- [ ] Confirm any subsequent test extension passes CI and expand coverage for storage failures, corruption, cancellation and limits.
- [x] Enforce a configurable Azure Experience read/write byte limit before and during buffering (`Repository:MaxExperienceArtifactBytes`, default 10 MiB); host-level mapping and tests still need verification. The host's response check remains as a secondary guard.
- [x] Keep publication pointer changes separate from immutable artifact storage. Authenticated DELETE removes only the pointer; immutable bytes remain retrievable.
- [x] Add authenticated Experience artifact PUT with route/body identity matching, Base64 decoding, SHA-256 validation, storage write, and read-back verification.
- [x] Add authenticated publication PUT that refuses to move the pointer until the immutable artifact exists and is integrity-verified; read the pointer back before success.
- [x] Apply a separate configurable JSON/Base64 request-body limit (`Repository:MaxExperienceArtifactRequestBytes`, default 14 MiB) and decoded artifact limit (`Repository:MaxExperienceArtifactBytes`, default 10 MiB). Deployment verification for chunked bodies remains open.
- [ ] Add deterministic publication concurrency semantics (current adapter contract is last-write-wins; no compare-and-swap).
- [ ] Add deterministic error translation; unexpected storage errors must not leak internals or be mapped to false 404 responses.
- [ ] Keep composition/materialization in the appropriate FSM_COS integration boundary, not in a REST controller.

### C. Secure the host before exposing writes
- [ ] Decide deployment/network exposure and authentication policy.
- [ ] Authorize PUT and any publication mutation explicitly; read-only public retrieval, if chosen, must not imply anonymous write access.
- [x] Replace unrestricted CORS with an explicit, opt-in origin/method/header policy where browser access requires CORS. Empty `Repository:AllowedCorsOrigins` grants no cross-origin browser access; the allow-list is read from final host configuration. Verified by host tests on commit `0c73f9dd` and successful [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39185338890) and [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39185338921). The checklist update itself also passed on `bb2a1a09`: [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39185560729), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39185560503). CORS is not authorization.
- [ ] Complete request-body, decoded artifact, response-size, concurrency and request-duration limit coverage. The declared-oversize authorized PUT test passed CI and verifies `413` before JSON binding; `IHttpMaxRequestBodySizeFeature` is set for streaming/chunked bodies, but that server-level path still needs deployment-level verification.
- [ ] Ensure cancellation reaches storage operations and large payloads do not require avoidable duplicate buffers.
- [ ] Test hostile inputs, malformed base64, path/route edge cases, oversized payloads, unauthorized writes and storage failures.
- [ ] Decide whether SHA-256 alone is sufficient for the threat model; if publisher authenticity matters, document signature/provenance and trust-root policy separately.

### D. Test the real HTTP boundary
- [x] Initial HTTP host tests: publication success/not-published, immutable artifact response bytes, malformed hash rejected before storage access, missing artifact, invalid Experience ID.
- [ ] Expand host tests to DI/startup failure, storage failure, corruption, cancellation and limits.
- [x] Publication tests: published pointer, no publication, missing immutable artifact, successful pointer movement, and unpublish retaining immutable bytes.
- [ ] Add tests for unauthorized/wrong-key responses on every Experience mutation, corrupt/missing artifact pointer and concurrent publishers.
- [ ] Integrity tests: mismatched address/hash must never produce a successful response.
- [ ] Error tests: storage unavailable, timeout/cancellation, malformed DTO, bad base64, oversized body and permission denial.
- [ ] Inventory tests: filtering, ordering guarantees if any, page size, opaque continuation token round-trip, invalid token, end-of-list and cancellation.
- [ ] Azure tests: create-only/idempotent write behavior, collision with differing bytes/metadata, listing pagination and cancellation. Keep cloud tests explicitly opt-in; report their status honestly.
- [x] Add REST client tests for publication lookup, unpublished outcome, immutable retrieval, and corrupt-byte rejection; current-head CI passed these tests on 2026-10-19 (see the current checkpoint above).
- [ ] Extend REST client tests for reserved-character escaping, response DTO compatibility, identity mismatch, non-success status mapping, and cancellation propagation.
- [ ] End-to-end test: publication lookup → immutable Experience retrieval and hash verification → exact MicroBundle address resolution/retrieval → FSM_COS materialization/composition → host activation only on success.

### E. Reconcile the candidate discovery branch
- [ ] Review [PR #21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) separately from `development`; it is draft and diverged, not an integrated release.
- [ ] Reconcile the paginated `GET /api/microbundles` proposal with the existing unpaginated observer DTO contract. Decide whether this is a breaking change, a versioned route, or a documented compatible migration.
- [ ] Verify exact bundle/version filters, pagination token semantics, cancellation and Azure paging with tests executed on the final candidate commit.
- [ ] Review optional semantic-address metadata and prove it does not silently change immutable identity or hash behavior.
- [ ] Reconcile candidate Experience routes and dependencies with the WebPage contract above.
- [ ] Do not merge/rebase wholesale; the candidate was recorded as substantially diverged. Preserve unique tests/docs while selecting a reviewable implementation.
- [ ] Keep the NuGet publish gate disabled until explicit release approval.

### F. Package, compatibility and release proof
- [ ] Confirm intended package version and release notes; do not infer publication from the `.csproj` version.
- [ ] Verify dependency package versions exist and compile against the intended public APIs; run both package-based and source-integrated solutions.
- [ ] Build the explicit solution in Release and run all tests from a clean checkout.
- [ ] Run package verification/pack and inspect the produced `.nupkg`: package ID/version, README, XML docs, target framework, dependency graph and included files.
- [ ] Check API compatibility for any changed route, DTO, interface or package dependency.
- [x] Record CI evidence: `efcd2cbad43061e2c8841d46f7e9e898be714192` — [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39179142427) and [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39179142357) succeeded. [ ] Verify current-head workflows for any further test changes and repeat for the release candidate after end-to-end tests land.
- [ ] Review license, security posture, README, XML documentation, changelog/release notes and package metadata.
- [ ] Publish only after explicit approval; verify the resulting NuGet package page and perform a clean consumer restore/build/smoke test.
- [ ] Update this checklist and the domain map with evidence, not intention, after each gate passes.

## 5. Suggested implementation order

1. **Contract first:** settle publication DTO/status semantics and dependency hash resolution with WebPage, MicroBundleDomain and FSM_COS owners.
2. **Small vertical slice:** add Experience publication GET + immutable Experience GET with host DI and focused HTTP tests.
3. **Integrity and failure behavior:** corrupt bytes, absent pointer/artifact, storage failure, cancellation and oversized requests.
4. **Security:** define read/write policy and limits before public exposure.
5. **Discovery branch reconciliation:** decide whether paginated inventory is compatible and bring its tests onto the chosen integration branch.
6. **End-to-end consumer proof:** retrieval → verification → composition → activation while preserving current active Experience on failure.
7. **Package validation:** exact-commit CI, package inspection, dependency/API compatibility, clean consumer test.
8. **Release decision:** update the checklist to show evidence; obtain explicit approval before publication.

## 6. Release gate

The REST package is releasable only when all required items above are either completed with linked evidence or explicitly declared out of scope with a documented consumer-compatible decision. At present, Experience publication/retrieval endpoints and initial host tests exist, and current-head CI is green; expanded failure-path coverage, deterministic dependency hash resolution, deployment-level security/limit verification, live Azure proof, end-to-end composition/activation, and exact release-candidate compatibility are **open**.

Related tracking: [issue #22 — immutable Experience retrieval through REST](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/22), [issue #10 — published Experience browsing](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/10), [issue #9 — first vertical slice](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/9), [issue #16 — inventory/ontology coverage/placement](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/16), and [issue #20 — zeroth Moniker](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/20).

This document is a working checklist, not a release approval and not evidence that any unchecked item has been implemented.



## Verification history and current disposition

The dated checkpoints below are condensed audit history; the summary and implementation inventory above are authoritative for current status.

| Milestone | Evidence | What it establishes (and does not establish) |
|---|---|---|
| Address-map identity guard (`82271089`, regression test `7b162797`) | [Build/Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203052887), [Package](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203052856), [regression Build/Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203067246), [regression Package](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203067232) | Rejects disagreement between map key and immutable address identity; does not choose the correct dependency hash. |
| Concurrent preload atomicity (`d056631a`, `ecbffebb`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39275320275), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39275320137) | Conflicting concurrent closures fail closed; no retry/merge policy is defined. |
| Cancellation atomicity (`ea3dfb80`, refined by `bf47e44b`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39278539302), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39278539175) | Cancellation is checked before commit and failed preload stays unresolved. The lock-contention test still uses broad `ThreadState.WaitSleepJoin` evidence, which does not prove the worker is waiting on this specific monitor. |
| Coverage collector correction (`529a7c12`, `6358720b`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39320677860), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39320677873) | Four test assemblies passed 52 tests with zero failures; logs showed no missing-collector warnings. Package artifacts were built for verification only. |
| Previous documentation checkpoint (`cab389d0`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321546705), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321546688) | Both required workflows passed at that checkpoint; `test` and `source-integration` passed; `verify` passed; `publish` was skipped. Superseded by the current documentation checkpoint below. |
| Prior documentation checkpoint (`d735fa75`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321974469), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39321974465) | Both required workflows passed at that checkpoint; superseded by the later documentation reconciliation. |

| Current documentation checkpoint (`23e43b0c`) | [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39325912503), [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39325912531) | Both required workflows passed; `test` and `source-integration` passed; package `verify` passed and `publish` was skipped. This verifies the documentation checkpoint only, not the open integration/security/release gates. |

**Current disposition: NOT RELEASE-READY. Readiness remains 60%.** The authenticated Experience write routes and initial host tests have been added on development and pass exact-head Build and Test; Package verification also passes with publish skipped. The Sith Policy evidence refresh could not fast-forward master, and no merge/force-push was attempted. These results do not change the release disposition. The immutable dependency-address contract (tracked in [issue #24](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/24) and [MicroBundleDomain issue #17](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/17)), deployment-level security/limit verification, live Azure evidence, end-to-end composition/activation, and clean package-consumer compatibility remain open. No package was published; the publish gate remains unchanged.