# Branch reconciliation — current audit

**Purpose:** preserve every unique implementation, test, document, visual, and release artifact; resolve PR dependencies; then reduce this repository to `master` and `development`.

**Audit baseline:** `development` at `963499d1e7b1d98cf9381119984a3dae757a4a83`. Ahead/behind counts below are comparisons against that baseline. They are inventory signals, not deletion criteria.

## Dependency order

```text
Experiences #54 (in-memory portable identity map)
        |
        v
Experiences #55
        |
        v
MicroBundleRepository #32
        |
        v
PR #33 — durable (ontology, version, SHA-256) <-> local ulong
        |
        v
Experiences repository adapter + verified-byte integration
        |
        v
existing FSM_COS numeric MicroBundleReference / RuntimeManifestEntry
```

- **PR #33 is the active repository dependency.** It preserves the existing numeric `MicroBundleArtifactAddress(ulong, version, hash)` and owns durable allocation/persistence.
- **PR #31 is not a prerequisite.** It changes the public artifact-address and REST/CLI identity to ontology strings. That conflicts with issue #32's explicit compatibility requirement. Preserve its useful ontology-oriented CLI/REST/resolver work for a separately reviewed API evolution; do not merge the branch wholesale.
- **PR #21** owns paginated artifact discovery, SemanticAddress metadata, discovery tests, and useful documentation/SVGs. It is compatible with the numeric-address contract and should be selectively reconciled after #33, not rebased or merged wholesale.
- **PR #17** owns the repository-native publisher and local round-trip/materializer smoke test. Its ancestor `feat/local-microbundle-roundtrip` is fully contained in the publisher branch; preserve the publisher as the current owner.
- **PR #14** established REST/FSM_COS separation and was merged to `master`. Its integration package/materializer work still needs selective reconciliation into the active development line; PR #21 contains overlapping but independently diverged work.
- **Experiences #55** has been updated with the concrete #33 contract and current validation state. Experiences consumes persisted bindings; it must not allocate IDs or own repository persistence. FSM_COS remains on its existing numeric pinned-reference contract.

## Branch inventory

| Branch | Ahead / behind `development` | PR / disposition |
|---|---:|---|
| `master` | canonical | Protected release-aligned baseline; keep |
| `development` | baseline | Active integration source; keep |
| `feature/revision-identity-bindings` | 44 / 0 | [#33](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/33) — active. Exact head `00cf677` passes Build/Test and Package verification. Remaining deterministic reservation-race and artifact/index failure-injection tests keep it draft. |
| `feature/ontology-address-mapping` | 35 / 0 | [#31](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/31) — preserve, but do not merge as-is. Public ontology-string address replacement conflicts with #32's numeric-address compatibility contract. Salvage unique CLI/REST/resolver work later. |
| `work/microbundle-artifact-discovery` | 104 / 231 | [#21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) — preserve. Unique paginated discovery contract/API, SemanticAddress metadata, list tests, lifecycle/boundary SVGs, docs, and FSM_COS materializer work. Selective reconciliation only. |
| `feature/microbundle-publisher` | 47 / 231 | [#17](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/17) — preserve. Unique publisher executable, docs, round-trip and materializer tests. Keep draft pending contract/CI review. |
| `feat/local-microbundle-roundtrip` | 33 / 231 | Ancestor of `feature/microbundle-publisher` (publisher is 14 commits ahead and 0 behind this branch). No separate unique commits remain outside the publisher branch. Candidate for the eventual deletion batch only after PR #17's work is integrated or explicitly superseded. |
| `architecture/decouple-rest-from-cos` | 63 / 231 | [#14](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/14) merged to `master`. Preserve until its REST/FSM_COS adapter, materializer, tests, and workflow changes are reconciled into `development`; it is not contained by PR #21. |
| `fix/repository-boundary` | 36 / 231 | [#11](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/11) closed unmerged. Preserve pending comparison with #14; it has boundary/materializer changes not proven to be fully superseded. |
| `docs/readme-standard` | 2 / 210 | [#18](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/18) closed as superseded after its useful onboarding content was reconciled into the current README. **Ready to delete:** its remaining README wording is outdated and its package workflow is obsolete. |
| `docs/visual-readme` | 36 / 231 | Preserve. Contains four distinct SVGs: Experience publication model, artifact identity, delivery boundary, and repository boundary, alongside implementation changes. Extract the visuals and review code separately. |
| `fix/repository-build-errors` | 4 / 206 | [#19](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/19) merged to `development`. **Ready to delete:** all three changed source/test files have identical blob SHAs to current `development`; the only remaining difference is an older package workflow using outdated action versions. No unique implementation remains. |
| `forge/experience-publication-catalog` | 8 / 248 | [#5](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/5) merged to `master`. **Ready to delete:** all six implementation/test files in its branch diff have identical blob SHAs to current `development`; its only remaining README difference is older onboarding material superseded by the current Workshop README. |
| `forge/experience-repository-azure` | 17 / 249 | [#4](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/4) merged to `master`. **Ready to delete:** Core artifact/address/interface and Core tests match `development`; Azure implementation and tests are superseded by the current bounded-read implementation and additional size-limit tests; its unique Experience storage layout/boundary explanation is now in the current README. |
| `implementation-azure-solidification` | 3 / 250 | [#2](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/2) merged to `master`. **Ready to delete:** Azure setup guide and live-Azure test match `development` exactly; its only remaining difference is an obsolete package workflow with old actions and a publish condition lacking the current `&& false` safeguard. |
| `implementation-core-azure-blob` | 32 / 251 | [#1](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/1) merged to `master`. Historical implementation baseline; preserve until root-template, Core/Azure source, and tests are checked against both canonical branches. |
| `release-prep/core-1.0.0` | 59 / 231 | [#12](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/12) closed unmerged. Preserve Core theory, contract, federation, package-graph documentation, diagrams, and tests until each is reconciled or explicitly rejected. |
| `release-prep/rest-1.0.0` | 28 / 231 | [#13](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/13) closed unmerged. Preserve REST release notes/assets and implementation/tests pending review. |

## Ready-to-delete batch

The following branches have passed a file-level preservation check and can be included in the next user-performed deletion batch:

1. `fix/repository-build-errors` — PR #19 merged; source/test files match `development`; remaining workflow is obsolete.
2. `forge/experience-publication-catalog` — PR #5 merged; all implementation/test files match `development`; README is superseded.
3. `forge/experience-repository-azure` — PR #4 merged; current bounded Azure implementation and tests supersede its older adapter; unique Experience storage documentation is preserved in `development`.
4. `implementation-azure-solidification` — PR #2 merged; Azure setup guide and live test match `development`; only obsolete workflow changes remain.
5. `docs/readme-standard` — PR #18 closed as superseded; all useful onboarding guidance is preserved in the current README; remaining branch files are outdated.

`feat/local-microbundle-roundtrip` is a **future candidate**, not yet ready: its work is carried by the still-open publisher PR #17.

## Reconciliation rules

1. Keep exactly `master` and `development` as the final branch set.
2. Preserve unique work before closing or deleting a branch: source, tests, docs, SVGs, workflow safeguards, release notes, and cross-repository dependencies all count.
3. Never merge/rebase a highly diverged branch wholesale when selective file/commit reconciliation is safer.
4. Keep package publication disabled unless explicitly authorized. Passing Package verification is not publication.
5. No merge, NuGet publication, release, deployment, or branch deletion is authorized by this inventory.
6. The user performs branch deletion; present all confirmed-ready branches together for one batch.

**Current result:** 5 branches are ready for the eventual deletion batch; 1 additional branch is a future candidate; all remaining unique work has an explicit preservation/reconciliation disposition.