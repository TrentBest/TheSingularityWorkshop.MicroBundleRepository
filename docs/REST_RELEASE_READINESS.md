# REST package — domain map and release readiness

> This is the release checklist for `TheSingularityWorkshop.MicroBundleRepository.Rest` and the consumer-facing REST contract it implements. It records the `development` branch as inspected on 2026-10-19. A source-declared version is not evidence that a package was published, and a route recommendation is not evidence that the route exists.

**Estimated release readiness: 60% — approaching, not ready.** MicroBundle PUT fails closed when `Repository:WriteApiKey` is unset, requires `X-Repository-Write-Key` when configured, and applies `Repository:MaxMicroBundleRequestBytes` (default 16 MiB). The REST client accepts optional `writeApiKey` and sends that header on PUT. CORS is opt-in: `Repository:AllowedCorsOrigins` is read from final host configuration, and an empty allow-list grants no cross-origin access. CORS behavior and the declared-oversized authorized PUT test have passed host CI. The declared body-size test returns `413` before JSON binding; the server request-body feature also limits streamed requests. Azure Experience writes now have a unit test proving an artifact over the configured cap is rejected before storage access. A live Azure oversized-read smoke test was added, but only executes against Azure when `EXPERIENCE_STORAGE_ACCOUNT_URI` is configured; passing CI does not establish that the live path ran. `RestMicroBundleCatalog.PreloadClosureAsync` now stages a candidate dependency closure and adds its bundles to the resident catalog only after every required artifact is retrieved, verified, and materialized. A regression test proves a missing dependency leaves neither the root nor dependency resolvable, and that a corrected address map can be retried. This implementation and test passed Build and Test and Package verification on commit `e0c88cc4`; publishing was skipped. This is a judgment-based progress estimate, not a test-coverage percentage. **Current disposition: NOT RELEASE-READY.** Remaining blockers include deterministic MicroBundle hash resolution, deployment origin configuration, actual live Azure oversized-read verification, end-to-end composition/activation proof, and final dependency/package compatibility evidence.

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
| Experience publication lookup | Development host maps `GET /api/experiences/{experienceId}/publication`; Azure catalog is registered in Azure mode; new host tests cover published/unpublished outcomes, invalid ID and missing-storage 503 | Current test suite passes CI on `7d3ef8a`; end-to-end evidence missing; local mode returns 503 |
| Immutable Experience GET | Development host route plus `RestExperienceClient`; new host tests cover exact byte retrieval, missing artifact and malformed-hash rejection | Existing host tests pass CI on `7d3ef8a`; additional host tests for 413 and integrity-failure mapping now pass current-head CI; Azure adapter oversized-read/write behavior and end-to-end evidence remain open |
| MicroBundle `(bundleId, version)` → trusted hash | Inventory can show stored addresses; no canonical selection rule is documented | Unresolved domain contract |
| Error behavior | Basic invalid address and not-found handling exist on MicroBundle GET | Incomplete; storage failures, integrity failures, invalid payloads and service unavailability need stable mappings |
| Security/limits | PUT fails closed without `Repository:WriteApiKey`; configured key is compared in fixed time; declared oversized authorized request test returns `413`; opt-in CORS is covered by passing host tests | CORS and declared-length request rejection are verified in CI; chunked request enforcement relies on server feature and needs deployment-level verification; deployment must configure trusted origins explicitly |
| Release validation | Host tests on `7d3ef8a` passed both workflows; the follow-up error-mapping tests on `798cc795c383246fb5a398f6c138e3a74ddbb4db` are pending | Evidence: [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39179690828) and [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39179690830). End-to-end proof remains open |

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
- [ ] Keep publication pointer changes separate from immutable artifact storage. Unpublishing must remove the pointer, not silently delete immutable bytes.
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
- [ ] Publication tests: published pointer, no publication, missing immutable artifact and pointer to corrupt/missing bytes.
- [ ] Integrity tests: mismatched address/hash must never produce a successful response.
- [ ] Error tests: storage unavailable, timeout/cancellation, malformed DTO, bad base64, oversized body and permission denial.
- [ ] Inventory tests: filtering, ordering guarantees if any, page size, opaque continuation token round-trip, invalid token, end-of-list and cancellation.
- [ ] Azure tests: create-only/idempotent write behavior, collision with differing bytes/metadata, listing pagination and cancellation. Keep cloud tests explicitly opt-in; report their status honestly.
- [x] Add REST client tests for publication lookup, unpublished outcome, immutable retrieval, and corrupt-byte rejection (execution still needs CI confirmation).
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

The REST package is releasable only when all required items above are either completed with linked evidence or explicitly declared out of scope with a documented consumer-compatible decision. At present, Experience publication/retrieval, deterministic dependency hash resolution, host-level tests, security limits and exact-release CI are **open**.

Related tracking: [issue #22 — immutable Experience retrieval through REST](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/22), [issue #10 — published Experience browsing](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/10), [issue #9 — first vertical slice](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/9), [issue #16 — inventory/ontology coverage/placement](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/16), and [issue #20 — zeroth Moniker](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/20).

This document is a working checklist, not a release approval and not evidence that any unchecked item has been implemented.


## Verified preload hardening update (2026-10-19)

- Commit [82271089](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/commit/822710892e8506d9b98b5ba5fd3355ca253d6b11) rejects a caller-supplied address map entry when its key bundle ID differs from the immutable address's `BundleId`, before any repository request.
- Commit [7b162797](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/commit/7b162797fc8e9b644867ca3985e9c2466618184d) adds a regression test asserting rejection and that neither identity becomes resident.
- Both commits passed Build and Test and Package verification: [Build/Test, 82271089](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203052887), [Package, 82271089](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203052856), [Build/Test, 7b162797](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203067246), [Package, 7b162797](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203067232). Publishing was skipped.
- This is fail-fast validation, not canonical dependency resolution: dependency declarations still identify bundle IDs without pinning immutable hashes, so the caller-provided address map remains a trust boundary.

## Current checkpoint (2026-10-19, 14:55Z)

The address-map identity guard and regression test are verified on `development`. The two code commits `82271089` and `7b162797` each passed Build and Test and Package verification; publishing was skipped. The follow-up documentation commits also passed all four workflows: [REST checklist Build/Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203241355), [REST checklist Package](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203241336), [WebPage handoff Build/Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203248450), and [WebPage handoff Package](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39203248609).

The next contract blocker is tracked in [issue #24 — deterministic immutable MicroBundle dependency resolution](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/24). No contract choice has been assumed: domain dependency declarations currently identify dependency bundle IDs, while exact retrieval requires a full immutable address. Coordinate manifest pinning versus a trusted, ambiguity-rejecting resolver with MicroBundleDomain, FSM_COS, and WebPage before making a cross-repository API change.

**Readiness is 60% — not release-ready.** The green documentation workflows verify documentation edits, not the still-open resolver contract, live Azure proof, deployment configuration, end-to-end composition/activation, or final package compatibility. The publish gate remains unchanged.

## Cancellation atomicity checkpoint (2026-10-19, 15:03Z)

- Commit [ea3dfb80](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/commit/ea3dfb80287f9b1dd94ac6faed534b534eadf6b2) adds `RestMicroBundleCatalogCancellationTests`, simulating cancellation after the final artifact response and before the catalog commit point.
- The test asserts that cancellation throws and the staged bundle remains unavailable through both catalog resolution overloads.
- Current-head [Build and Test](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39274395256) and [Package verification](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/runs/39274395250) both completed successfully; publishing was skipped.
- This adds regression evidence for cancellation atomicity. It does not resolve the immutable dependency address contract or replace the end-to-end composition/activation proof.

**Readiness: 60% — not release-ready.** The publish gate remains unchanged.
