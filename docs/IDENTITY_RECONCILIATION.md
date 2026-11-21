# Portable MicroBundle Artifact Identity

This document separates the portable identity of an immutable artifact revision from the numeric address used by the current repository storage contract.

## Portable revision identity

The portable revision key is the complete tuple:

```text
(canonical ontology, exact version, SHA-256)
```

- **Ontology** identifies the capability independently of a repository. It is a canonical hierarchical identifier, not a display label, assembly name, URL, or filesystem path.
- **Version** selects the exact declared artifact version.
- **SHA-256** verifies the exact artifact bytes.

The digest is an integrity value. It does not authenticate a publisher or authorize publication.

## Repository-local binding

The repository may allocate a non-zero numeric handle for its own storage/index implementation:

```text
portable revision identity
          |
          | durable, validated mapping
          v
repository-local numeric address
          |
          v
immutable verified artifact bytes
```

A numeric handle allocated by Repository A is not assumed to be meaningful in Repository B. It must not be advertised as a portable semantic identity.

The binding is a repository concern. MicroBundleDomain owns the capability/runtime contract; the repository owns durable storage and its local mapping; FSM_COS owns composition and dependency closure.

## Validation invariants

A valid binding must preserve exact version and content-hash agreement between portable identity and local address. The artifact bytes must hash to the declared SHA-256 before the binding can be trusted.

The durable mapping must be one-to-one for the complete portable revision key and local address. Duplicate, conflicting, corrupt, or partially committed index state must fail closed. An artifact that exists without a committed binding is an unbound artifact, not a resolvable identity.

Concurrent writers require storage-native coordination: filesystem writers need cross-process exclusion/atomic index replacement; Azure writers need conditional updates/reservations. A process that does not participate in the coordination protocol must not be mixed with writers that assume it.

## Current development versus proposed integration

The current `development` branch still exposes numeric `MicroBundleArtifactAddress` values to the repository contract. The portable identity work is staged in a draft PR chain:

1. [PR #39 — durable portable revision identity and local bindings](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/39)
2. [PR #40 — REST identity endpoints and Workshop publisher authorization](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/40), stacked on #39
3. [PR #41 — authenticated identity-aware publisher CLI](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/pull/41), stacked on #40

The PR descriptions report successful .NET 10 build/test evidence and package verification, while package publication was skipped. These PRs are still draft and unmerged. Their behavior must not be described as present on canonical `development` until the stack is reconciled and accepted.

The cross-repository domain/runtime mapping remains tracked in [MicroBundleDomain issue #28](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/28). The repository's local binding is necessary but does not alone prove that FSM_COS manifests and numeric dependency declarations resolve the same portable identity.

## Explicitly out of scope

This repository does not decide:

- what an ontology identifier means;
- which capability dependencies are semantically required;
- whether an artifact is trusted or authorized for execution;
- which revision an Experience should publish;
- how FSM_COS composes, arbitrates, or activates a capability;
- whether an unreferenced artifact may be deleted.

Those require their owning contracts and explicit policy.

## Acceptance path

Before treating the portable identity system as complete:

1. Reconcile #39, #40, and #41 as one dependency-ordered change set.
2. Verify exact-head CI after the final integration commit.
3. Prove repeated ingestion is idempotent and conflicting bindings fail closed.
4. Prove version/hash mismatches and corrupted artifacts are rejected.
5. Prove mapping survives process restart and concurrent writers.
6. Integrate portable identity resolution with the Domain and FSM_COS runtime identity contract.
7. Test dependency closure from portable references through materialized runtime bundles.
8. Preserve compatibility for existing numeric manifests or provide an explicit migration.
9. Keep package publication disabled until the owner separately authorizes release.

## Related

- [Repository architecture](ARCHITECTURE.md)
- [Evidence-backed domain map](DOMAIN_MAP.md)
- [MicroBundleDomain identity model](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/IDENTITY_MODEL.md)
