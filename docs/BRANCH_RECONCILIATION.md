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

## Current result

- **17 branches total**
- **7 ready-to-delete candidates**
- **4 retained for reconciliation**
- **4 active PR branches**
- **2 permanent branches**
- **0 branches deleted by this audit**

The candidate list is a recommendation, not a deletion action. Recheck the branch/PR state immediately before any future deletion batch.
