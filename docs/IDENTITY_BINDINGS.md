# Durable MicroBundle revision identity bindings

## Portable identity and local runtime address

An immutable artifact revision has the portable identity:

`(ontology, exact version, SHA-256)`

The runtime-facing repository address remains:

`MicroBundleArtifactAddress(ulong bundleId, version, contentHash)`

The repository owns the durable translation between them. The numeric ID is a non-zero, repository-local storage handle; it is not a portable identity and must never be written into an authored Experience manifest or treated as globally stable.

A binding is one-to-one:

- A complete portable revision resolves to exactly one local numeric address.
- A local numeric ID resolves to at most one complete portable revision.
- Different versions or hashes of one ontology are distinct revisions.
- Re-ingesting identical bytes under the same identity returns the existing binding.
- Missing, aliased, conflicting, or corrupt bindings fail closed.

## API

`IMicroBundleIdentityRepository` extends `IMicroBundleRepository` with:

- ingestion from ontology + version + bytes; SHA-256 is computed by the repository;
- forward resolution from the complete `MicroBundleArtifactIdentity`;
- reverse resolution from the repository-local numeric ID.

The public filesystem and Azure adapters implement this capability additively. They continue to implement `IMicroBundleRepositoryObserver` and `IMicroBundleArtifactCatalog`; bounded `ListPageAsync` discovery and existing observation APIs remain available. Identity allocation and persistence do not move into Experiences, MicroBundleDomain, or FSM_COS.

## Persistence invariants

1. Validate the ontology and portable storage-safe version segment.
2. Compute SHA-256 from the supplied bytes; never trust a caller-provided digest.
3. Serialize allocation and commit through the provider's concurrency boundary.
4. If the complete identity already exists, verify the stored artifact bytes before returning it.
5. Reserve a non-zero ID while enforcing uniqueness in both directions.
6. Persist immutable artifact bytes before publishing the identity index.
7. Expose a binding only after the index commit succeeds.

A failed write may leave an unbound artifact or reserved ID. Such orphans are not guessed into bindings or reused. Index corruption, missing bytes, and content-hash mismatch fail closed.

## Filesystem provider

The filesystem identity adapter uses an exclusive lock file to serialize writers across processes. Artifact and index files are flushed before same-directory atomic replacement. Identity writes and legacy numeric writes pass through the same lock and check existing numeric artifact directories before allocating an ID.

The implementation does not explicitly fsync the parent directory after rename. It therefore does not claim immunity to every sudden-power-loss/filesystem-metadata failure.

## Azure Blob provider

The Azure identity adapter stores a forward/reverse index in one JSON blob. ETag conditional writes serialize concurrent index updates across processes and hosts. Per-ID reservation blobs created with `If-None-Match: *` arbitrate identity allocations against numeric writes. Artifact bytes are persisted and verified before the index is published.

**Mixed-version boundary:** older Azure writer processes that do not participate in reservation blobs cannot coordinate with the new protocol. Quiesce or migrate those writers before relying on concurrent mixed-version writes. Azurite integration tests exercise the reservation and index races; no live production-account migration is claimed.

## Ownership boundaries

- **Experience manifest:** portable ontology string, exact version, SHA-256; no local numeric ID.
- **MicroBundleRepository:** durable mapping, storage, byte verification, and local address resolution.
- **FSM_COS:** dependency closure, pinned runtime references, and composition.
- **Host:** materialization and activation only after the complete candidate is verified and composed.

This repository does not resolve semantic dependencies, construct runtime manifests, materialize bundles into a host, or claim an Experience is playable.
