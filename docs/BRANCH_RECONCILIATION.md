# Branch reconciliation — MicroBundle Repository

This document is a working inventory, not permission to delete branches. The goal is to preserve useful work, converge on one reviewable implementation, and only then remove superseded branches.

## Canonical branches

- `master` — protected release/integration baseline; do not change as part of this reconciliation.
- `development` — working integration baseline.
- `work/microbundle-artifact-discovery` — active candidate for the paginated artifact-discovery work and documentation alignment.

## Current pull requests

| PR | Branch | Current disposition |
|---|---|---|
| [#21](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/21) | `work/microbundle-artifact-discovery` | Keep draft while API, Azure metadata behavior, docs, and final CI are reviewed. |
| [#19](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/19) | `fix/repository-build-errors` | Keep until the four-commit fix is deliberately integrated or superseded. |
| [#18](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/18) | `docs/readme-standard` | Keep until useful onboarding guidance is reconciled. Its workflow change adds the required `&& false` publish gate, already present on the active branch; do not merge its older workflow wholesale. |
| [#17](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/17) | `feature/microbundle-publisher` | Keep draft. The publisher and round-trip/materialization work must be reviewed against the repository's release scope before disposition. |

Closed PRs do not prove that their source branches contain no unique work.

## Branch inventory

The ahead/behind figures below compare each branch with `development` at the time of this audit. A diverged branch can contain both superseded and still-useful work; these figures alone are not a deletion criterion.

| Branch | Ahead / behind `development` | Recommended disposition |
|---|---:|---|
| `architecture/decouple-rest-from-cos` | 63 / 25 | **Preserve and reconcile.** Contains substantial REST/composition boundary and materializer work. |
| `docs/readme-standard` | 2 / 4 | **Keep for PR #18.** Merge useful onboarding guidance into the active README before closing or deleting. |
| `docs/visual-readme` | 36 / 25 | **Preserve and consolidate.** Contains valuable SVGs and teaching material, but also implementation changes; not a documentation-only branch. |
| `feat/local-microbundle-roundtrip` | 33 / 25 | **Preserve and reconcile.** Includes artifact round-trip and publisher-related work that needs an explicit scope decision. |
| `feature/microbundle-publisher` | 47 / 25 | **Keep for PR #17.** Publisher executable and tests are substantive work, not disposable branch noise. |
| `fix/repository-boundary` | 36 / 25 | **Inspect before disposition.** Contains boundary documentation/assets and composition-adapter changes; determine which are superseded by the selected implementation. |
| `fix/repository-build-errors` | 4 / 0 | **Keep for PR #19.** Small, directly based on `development`; integrate or explicitly supersede before deletion. |
| `forge/experience-publication-catalog` | 8 / 42 | **Audit against current source.** Publication-catalog work may overlap later integration; do not infer it is obsolete from age. |
| `forge/experience-repository-azure` | 17 / 43 | **Audit against current source.** Contains Experience artifact storage work; determine what is already integrated and what remains unique. |
| `implementation-azure-solidification` | 3 / 44 | **Inspect its live-Azure tests and setup changes.** Small branch, but unique test/operational work may still be useful. |
| `implementation-core-azure-blob` | 32 / 45 | **Audit carefully.** An older implementation baseline with many files; compare contracts and tests against the current structure before classifying as superseded. |
| `release-prep/core-1.0.0` | 59 / 25 | **Preserve pending content audit.** Contains extensive contract/theory/test material; closed release-prep PR status alone is insufficient evidence for deletion. |
| `release-prep/rest-1.0.0` | 28 / 25 | **Preserve pending content audit.** Contains REST release notes/assets and implementation/test changes; determine what remains valuable. |
| `work/microbundle-artifact-discovery` | Compared separately to `master`: ahead, 0 behind (the count changes as this PR receives commits) | **Active branch.** Keep until PR #21 is reviewed and its remaining correctness questions are resolved. |

## Deletion rule

A branch becomes a deletion candidate only after all of these are true:

1. Its open PR is merged or deliberately closed with an explicit replacement.
2. Its commits and file changes have been compared with the chosen integration branch—not merely counted as ahead/behind.
3. Unique tests, docs, SVGs, release notes, and implementation changes have either been integrated or explicitly rejected.
4. Any follow-on branch/PR dependency has been checked.
5. The final report names the exact branch and the reason it is safe to remove.

**Current conclusion: no extra branch is yet certified safe to delete.** The next practical step is to consolidate the visual README assets and useful onboarding material into the active branch, then reconcile the publisher/materializer and older Experience-storage branches. This keeps the branch cleanup evidence-based rather than destructive.
