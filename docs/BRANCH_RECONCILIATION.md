# Branch reconciliation — current audit

**Purpose:** preserve every unique implementation, test, document, visual, and release artifact; then reduce this repository to exactly `master` and `development`.

**Audit date:** 2026-11-12  
**Inventory:** 17 branches on the live remote at the time of this update. The former `feat/local-microbundle-roundtrip` ref is no longer present; its commits remain reachable from the active publisher branch. This is a content/PR audit, not authorization to delete branches.

## Active work and dependency boundaries

| Branch | PR | Disposition |
|---|---:|---|
| `feature/revision-identity-bindings` | [#33](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/33) | Active; retain. Revision-keyed durable identity bindings. Draft, unmerged. |
| `feature/ontology-address-mapping` | [#31](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/31) | Active; retain. Its public ontology-string address replacement conflicts with the numeric-address compatibility requirement. Salvage compatible CLI/REST/resolver work separately; do not merge wholesale. |
| `feature/microbundle-publisher` | [#17](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/17) | Active; retain. Publisher and local round-trip/materializer work. |
| `work/microbundle-artifact-discovery` | [#21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) | Active; retain. Paginated discovery, semantic metadata, tests, and materializer integration require selective reconciliation. |

PR #33 currently preserves the existing numeric `MicroBundleArtifactAddress(ulong, version, SHA-256)` contract and adds a separate repository-owned mapping from portable `(ontology, version, SHA-256)` revisions to nonzero repository-local numeric IDs. Its current head is `64829170c946b5862458920ab38cb4e4c3370be4`. Build/Test run #479 passed (103 regular tests and 105 source-integration tests); package verification run #467 passed with publication skipped. The PR remains draft and unmerged. The filesystem parent-directory durability limitation and Azure mixed-version writer limitation remain documented; see [PR #33](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/33).

The repository identity work is consumed downstream by Experiences, but the repository remains the persistence/allocation owner. FSM_COS retains its existing numeric pinned-reference contract.

## Branch inventory

### Permanent branches — 2

- `master` — release-aligned baseline; retain.
- `development` — active integration source; retain.

### Active PR branches — 4

- `feature/revision-identity-bindings` — PR #33; retain.
- `feature/ontology-address-mapping` — PR #31; retain pending selective salvage/review.
- `feature/microbundle-publisher` — PR #17; retain.
- `work/microbundle-artifact-discovery` — PR #21; retain.

### Ready for the single deletion batch — 7

These are candidates whose useful work has a surviving home or whose PR is merged/superseded. They have **not** been deleted.

1. `architecture/decouple-rest-from-cos` — PR #14 merged; the branch's remaining history is represented by the merged integration and subsequent work.
2. `fix/repository-build-errors` — PR #19 merged; source/test changes are represented in `development`; remaining workflow is obsolete.
3. `forge/experience-publication-catalog` — PR #5 merged; implementation/tests are represented in `development`; README content is superseded.
4. `forge/experience-repository-azure` — PR #4 merged; current bounded Azure implementation/tests supersede the older adapter; Experience storage boundary documentation is represented in current docs.
5. `implementation-azure-solidification` — PR #2 merged; Azure setup/live-test work is represented in `development`; remaining workflow is obsolete.
6. `implementation-core-azure-blob` — PR #1 merged; historical implementation baseline is superseded by the current Core/Azure implementation and tests.
7. `docs/readme-standard` — PR #18 closed as superseded; useful Visual Studio-first onboarding and package-boundary guidance is present in the current README; old workflow changes are intentionally not carried forward.
The former `feat/local-microbundle-roundtrip` branch is absent from the current remote inventory. Its complete history had been contained in `feature/microbundle-publisher` (PR #17), so its commits remain reachable there; no additional cleanup action is needed for that ref.

### Retain pending reconciliation — 4

- `docs/visual-readme` — retain pending reconciliation; it is not documentation-only. Its long-form README contains useful distinct explanations of optional semantic metadata, REST discovery as a catalog boundary, explicit non-goals, and a documentation map. The current `development` README follows the Workshop 5-section standard and already covers much of the architecture, but not all of those topics. Its compiled-assembly payload (`MicroBundleAssemblyPayload.cs`, blob `c827692...`), FSM_COS materializer/catalog adapter and tests are also absent from `development`; the newer payload implementation and matching adapter/tests are present on active PR #21 (`work/microbundle-artifact-discovery`). The branch's `AzureExperienceCatalog.cs` is older than both PR #21 and `development`. Its four architecture SVGs were previously verified present in `development`. Before retiring this branch, reconcile the unique README explanations into the current structure and verify PR #21's current-head implementation/test coverage; do not copy its stale source wholesale.
- `fix/repository-boundary` — its README and architecture document sharpen the opaque-artifact/composition-host boundary. Its diff removes payload/materializer/catalog code from the REST package and differs from the integration-package direction in PR #14 and PR #21. Preserve until the intended boundary is resolved; do not merge it wholesale.
- `release-prep/core-1.0.0` — contains four substantial documents absent from `development`: `CORE_CONTRACT.md`, `REPOSITORY_THEORY.md`, `FEDERATED_HOSTING.md`, and `PACKAGE_BUILD_GRAPH.md`. Preserve and reconcile their useful contract, federation, and release-governance theory. Update old payload/identity assumptions against PR #33 before publishing these as current guidance.
- `release-prep/rest-1.0.0` — contains `REST_RELEASE_1_0.md` and release work absent from `development`. Its stated dependency closure includes FSM_COS, which conflicts with the later REST/composition transport-boundary separation. Preserve the useful transport/release checklist, but rewrite the dependency closure and recheck implementation/tests before retiring the branch.

## Reconciliation rules

1. The final branch set is exactly `master` and `development`.
2. Preserve unique source, tests, docs, SVGs, workflow safeguards, release notes, and cross-repository dependencies before retiring a branch.
3. Do not merge highly diverged branches wholesale when selective file/commit reconciliation is safer.
4. A PR passing tests or package verification is not permission to merge, publish, release, or deploy.
5. Keep package publication disabled unless explicitly authorized.
6. Branch deletion is a separate user-authorized operation. Present the confirmed candidates together for one batch; do not delete branches as part of this audit.


### `fix/repository-boundary` — selective salvage only

The branch is highly divergent from current integration: at the 2026-11-12 audit it is 36 commits ahead and 251 behind `development` (merge base `a7b3632b33215fbdceeca1b7e056142793e9a7cb`). Its remaining distinct contributions are primarily boundary documentation and a proposed published-Experience listing surface.

- **Documentation worth preserving:** the branch states explicitly that the repository returns verified opaque bytes and does not define or interpret the executable representation. The current README already expresses this boundary, but the branch's architecture explanation can inform future refinements.
- **Potential API contribution, not accepted:** the branch adds `IExperienceCatalog.ListPublishedAsync`, an Azure Blob enumeration implementation, and `GET /api/experiences`. This is a catalog/discovery capability, not part of immutable artifact retrieval. Before adopting it, define pagination, ordering guarantees, listing cost, and whether a mutable publication catalog is the right source for discovery.
- **Do not transplant its REST host or Azure catalog wholesale:** the branch's host uses permissive `AllowAnyOrigin` CORS and exposes write routes without the current development branch's fail-closed write-key middleware and request-body limits. Current development has since added local MicroBundle storage, explicit CORS allowlisting, write authorization, bounded request bodies, and stronger Experience artifact verification.
- **Do not revive removed REST materializer/catalog adapter types from this branch:** the current architecture keeps representation/materialization outside repository Core and keeps repository responsibilities narrow.

Disposition: retain pending a deliberate decision on whether published-Experience listing belongs in this repository and, if so, a paginated, secured design with tests against current development. No branch-wide merge or cherry-pick is recommended.

## Current result

- **17 branches total**
- **7 ready-to-delete candidates**
- **4 retained for reconciliation**
- **4 active PR branches**
- **2 permanent branches**
- **0 branches deleted by this audit**

The candidate list is a recommendation, not a deletion action. Recheck the branch/PR state immediately before any future deletion batch.


### `release-prep/core-1.0.0` — preserve theory, do not revive the release snapshot

The branch is 59 commits ahead and 252 behind `development` (merge base `a7b3632b33215fbdceeca1b7e056142793e9a7cb`). It contains substantial unique documentation, but its implementation and release assumptions predate current integration and PR #33.

- **Documentation worth reconciling:** `docs/CORE_CONTRACT.md` and `docs/REPOSITORY_THEORY.md` explain the immutable artifact/delivery boundary, separation of artifact bytes from publication pointers, and the boundary between MicroBundleDomain, repository storage, and FSM_COS composition. These are useful source material, not ready-to-publish current contracts: their identity examples still use numeric BundleId as the portable semantic identity and need updating to the current dual-identity model (portable `(ontology, version, SHA-256)`; repository-local numeric address/binding retained for existing runtime-facing APIs).
- **Federation theory:** `docs/FEDERATED_HOSTING.md` adds useful third-party repository custody/admission concepts. Reconcile against current security/admission ownership before presenting it as implemented behavior; keep the distinction between documented architecture and shipped capability explicit.
- **Release graph:** `docs/PACKAGE_BUILD_GRAPH.md` contains a useful rule that stable release eligibility depends on the complete first-party dependency closure and that publication remains a separate human-authorized action. Its listed 1.0.0 package frontier and cross-repository versions are historical and do not describe current development, where repository Core and REST project files declare alpha versions and current upstream dependencies. Rewrite the graph from live project/package metadata before using it as a release gate.
- **Implementation not adopted:** the branch adds `MicroBundleAssemblyPayload` and serialization tests, but the payload implements `IBinarySerializable` from `FSM_Serialization`; current Core has no such project/package dependency. Its envelope also embeds the numeric BundleId, which needs an explicit compatibility decision against the portable revision identity and repository-owned mapping in PR #33. Do not copy this implementation into Core without deciding representation ownership, dependency direction, and identity semantics.
- **Identity/API changes:** the branch's altered artifact addresses and identity tests are older than the current revision-binding design in PR #33. Use PR #33 as the current implementation candidate; preserve the older branch as historical design evidence only.
- **REST materializer:** the branch adds an assembly materializer in the REST package. Current architecture keeps representation/materialization outside repository Core and avoids making repository storage responsible for composition. Verify the active PR #21 integration boundary before considering any adapter; do not transplant the old type simply because it exists in the release branch.

Disposition: retain until the unique theory/federation/release-governance material is selectively reconciled into current documentation and the payload question is resolved. The branch is not a valid 1.0.0 release snapshot and should not be merged or cherry-picked wholesale.

### `release-prep/rest-1.0.0` — salvage checklist, refresh dependency frontier

The branch is 28 commits ahead and 252 behind `development` (same merge base `a7b3632b33215fbdceeca1b7e056142793e9a7cb`). It contains a unique `docs/REST_RELEASE_1_0.md`, a release-frontier SVG, package/workflow changes, and an older REST host revision.

- **Documentation worth preserving:** the release contract correctly distinguishes REST transport, repository delivery, and FSM_COS composition, and it explicitly keeps package publication manual. The checklist is useful as a starting point for a future release-readiness document.
- **Refresh dependency claims:** the document names MicroBundleRepository.Core 1.0.0, FSM_REST 1.0.0, and FSM_COS 1.0.0 as the stable dependency closure. That is a historical release plan, not the current state: development's REST project references FSM_REST 0.1.0-alpha.4, FSM_COS 0.1.0-alpha.6, MicroBundleDomain 1.0.1, and the repository Core project. Recalculate the complete graph from current project metadata and the actual intended package boundaries before any release work.
- **Current host is materially safer:** the old branch's `Program.cs` predates current development's opt-in CORS allowlist, fail-closed write-key authorization, constant-time key comparison, and bounded request-body handling. Do not transplant the old host.
- **Identity and REST contract are stale:** the branch predates current revision identity work and the active PR #31/#33 design discussion. Any route or DTO release checklist must be reconciled with the selected public portable identity and the preserved numeric runtime/storage compatibility contract.
- **Workflow changes are not release authority:** the branch's package workflow changes do not authorize publication. Current policy remains that package verification and packaging may run, but NuGet publication requires explicit user authorization.

Disposition: retain until its checklist and visual are either updated into current docs/assets or deliberately rejected. No release snapshot, package version, dependency closure, or workflow from this branch is accepted as current.


## Release-prep audit result

- `release-prep/core-1.0.0`: 59 ahead / 252 behind; retain for selective documentation reconciliation and the unresolved assembly-payload boundary.
- `release-prep/rest-1.0.0`: 28 ahead / 252 behind; retain for selective release-checklist/visual reconciliation only.
- Neither branch is a current release candidate. Both contain historical package versions and host/identity assumptions; neither was merged or cherry-picked.
