# CiAiLLM — MicroBundleRepository Coordination

## Purpose

This file is the coordination contract for CiAi-assisted work in `TheSingularityWorkshop.MicroBundleRepository`. Keep changes evidence-driven, small enough to review, and aligned with the repository's role in the Singularity Workshop stack.

## Architectural boundaries

- **MicroBundleDomain = WHAT** a bundle is and how its identity, version, and content are described.
- **MicroBundleRepository = WHERE** immutable bundle artifacts are stored and retrieved.
- **FSM_COS = HOW** selected bundles are composed into a runtime assembly.
- The host application owns its scheduling/tick loop, concurrency policy, lifecycle, instrumentation, and presentation.
- The local filesystem provider must work without cloud credentials. Azure and REST providers are optional adapters, not prerequisites for local development or core use.

Preserve content-addressed identity and integrity checks. Avoid introducing unnecessary dependencies into the core repository abstraction.

## CiAi observer responsibilities

CiAi is a CI evidence observer and diagnostic aid here; it is not a deployment authority.

- Observe the actual repository and commit SHA associated with a completed workflow run.
- Produce a reviewable observation report as a retained workflow artifact.
- Use least-privilege permissions and pinned immutable GitHub Actions references.
- Treat missing credentials or unavailable evidence explicitly; never fabricate a passing result.
- Keep the observer read-only. It must not publish packages, deploy artifacts, alter repository settings, merge branches, or delete branches.

The current `.github/workflows/ciai-observe.yml` workflow is intended to observe package CI evidence and upload `commit-observation.json`. Its workflow-level failures must be resolved and a real artifact verified before calling the observer integration complete.

## Deployment is a separate, protected capability

Future deployment or artifact-delivery automation must be a distinct workflow with explicit user authorization and environment protections. Keep destination selection, endpoint/account/tenant configuration, and credentials conceptually separate. Prefer OIDC or managed identity with narrowly scoped, short-lived credentials. Never print secrets. Verify artifact hashes and provenance, run a destination health check, record the outcome, and define a rollback path before treating deployment as successful.

A documented deployment vision is not an implemented deployment feature. Do not imply that this repository currently deploys to a cloud destination.

## Sith branch and release policy

- Work on the existing `development` branch unless explicitly instructed otherwise.
- The intended final branch set is `master` and `development`.
- Reconcile and preserve unique work before any branch cleanup. The user performs surplus-branch deletion manually.
- Do not merge into `master`, delete branches, publish NuGet packages, deploy externally, or change protected settings without explicit user approval.
- Keep package publishing gated; successful pack/test validation is not authorization to publish.

## Current work snapshot

- Local filesystem repository and its focused tests are in place.
- Build/test and package-validation workflows have passed in the current work sequence; package publication was skipped intentionally.
- The CiAi observer workflow exists, but recent workflow runs failed before jobs were created. Diagnose workflow validation before debugging application behavior.
- No end-to-end external deployment has been implemented or proven.

Readiness estimates are working estimates, not measured quality scores: repository foundation approximately **75%**; future CiAi-assisted deployment approximately **30%**.

## Next steps

1. Diagnose and fix the observer workflow's workflow-level failure.
2. Trigger it against a known completed CI run and verify the uploaded report's repository, commit SHA, evidence, and artifact retention.
3. Demonstrate local artifact storage and retrieval end-to-end without cloud credentials.
4. Add and test optional destination adapters separately; retain explicit approval gates for any real deployment or publication.
5. Update this file and the repository's readiness notes when verified evidence changes the status.
