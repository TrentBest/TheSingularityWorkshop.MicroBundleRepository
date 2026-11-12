# Durable revision identity bindings

## Contract

The portable identity of an immutable artifact revision is the complete tuple:

`(ontology, version, SHA-256)`

The existing `MicroBundleArtifactAddress(ulong bundleId, version, contentHash)` remains the numeric address used by runtime-facing storage APIs. The repository owns the durable translation between the two forms.

A binding is one-to-one:

- One complete portable revision identity resolves to exactly one non-zero local `ulong`.
- One local `ulong` resolves to exactly one complete portable revision identity.
- Different versions or hashes of the same ontology are different identities and must receive distinct local IDs.
- Re-ingesting identical verified bytes under the same complete identity returns the existing binding.
- An identity conflict, reverse-ID conflict, invalid ID, or mismatched version/hash fails closed.

The mapping is scoped to a repository namespace. It is not a globally meaningful ID and must not be copied as an identity between repositories.

## Persistence invariant

Artifact bytes and their identity binding form one logical commit:

1. Validate ontology and version.
2. Compute SHA-256 from the supplied bytes; never trust a caller-supplied digest.
3. Enter the repository's concurrency-safe allocation/commit boundary.
4. If the complete identity is already bound, verify the stored artifact and return that binding.
5. Otherwise reserve a non-zero local ID while enforcing uniqueness in both directions.
6. Persist the verified artifact bytes at the numeric address.
7. Durably commit both forward and reverse binding indexes.
8. Expose the binding only after the durable commit succeeds.

A failed integrity check or artifact write must not create a usable binding. A crash between artifact persistence and index commit may leave an unreferenced immutable artifact, but must not leave a binding to absent or unverified bytes. Recovery must validate/reconcile such orphaned artifacts without guessing identity.

Implementations must use a transactional store where available, or a recoverable write protocol with an explicit commit marker. A process-local lock is insufficient when multiple processes can write the same repository. Allocation must enforce uniqueness at the persistence layer; random-number collision probability is not a uniqueness guarantee.

## API boundary

`IMicroBundleIdentityRepository` is a repository capability layered on the existing `IMicroBundleRepository` contract. It provides:

- ingestion from ontology + version + bytes, with SHA-256 computed by the repository;
- forward resolution from the complete portable identity;
- reverse resolution from the numeric local ID.

The interface does not move identity allocation into Experiences, MicroBundleDomain, or FSM_COS. Consumers may read persisted bindings; they may not allocate or mutate them independently.

## Explicit non-goals

- No change to `MicroBundleArtifactAddress` or `MicroBundleReference(ulong, version, hash)`.
- No global numeric identity service.
- No dependency resolution, composition, materialization, or runtime policy in this repository.
- No package publication or cross-repository integration in this change.
