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

## Provider implementation status

### Filesystem

The filesystem identity adapter persists the forward/reverse mapping in one atomically replaced index. Identity ingestion and legacy numeric writes share an exclusive cross-process lock, so ID allocation, checks against legacy numeric artifact directories, and numeric writes are serialized within this adapter. Artifact and index file contents are flushed to disk before same-directory replacement, and the adapter verifies artifact bytes when resolving a binding. The implementation does not explicitly fsync the parent directory after rename, so it does not claim a guarantee against every sudden-power-loss/filesystem-metadata failure.

### Azure Blob Storage

The Azure adapter persists the mapping in one JSON index blob and uses ETag conditional writes (`If-Match` / `If-None-Match`) to serialize competing identity-index updates across processes and hosts. It also creates a per-ID reservation blob with `If-None-Match: *` before an identity ID is committed or a new numeric write proceeds. The conditional reservation is the shared allocation boundary: if a numeric writer wins, identity allocation rejects that ID; if identity allocation wins, the numeric API rejects it. Legacy numeric artifact prefixes are checked before identity reservations are created. Immutable artifact blobs are written before the mapping index. A failed conditional index update can leave an unreferenced artifact and a safely unavailable reservation; the retry re-reads the winning index before selecting a binding.

**Important compatibility boundary:** this reservation protocol coordinates writers using the updated Azure adapter. Older processes that write numeric artifacts without creating reservation blobs cannot participate in that protocol; deployments must quiesce those writers or migrate them before relying on concurrent mixed-version writes. The current Azurite acceptance suite covers reservation races, ETag retries, and crash-boundary recovery. A live production-account migration has not been performed.

### Acceptance coverage and remaining work

Implemented on the current feature branch:
- Azure identity-ingestion concurrency and conditional-write coverage against Azurite.
- A deterministic Azure reservation race test that synchronizes an identity allocator and numeric writer after both have observed the same candidate ID but before either conditional reservation.
- Failure injection after artifact persistence and before identity-index commit for both Azure and filesystem adapters. The tests verify that the artifact remains unbound, the orphan reservation/path is not reused, and a reopened repository can commit a fresh binding.
- Corrupt-index rejection and byte-integrity checks on resolution.

### Exact-head validation

Validated feature head: `987d6662c54d56cf2696d22eee1724a82118851c`.

- Build and Test run #478: regular build/test **passed** (103/103 tests: Core 18, Local 23, Azure 26, REST host 21, REST 15).
- The same run's source-integration job **passed**, including the source-integrated build and all 105 tests (Core 18, Local 23, Azure 26, REST host 21, REST 17).
- Source integration checked out moving `FSM_API/development` at `960ad5a97096586166a89b8df65ac393f60bd150`. This resolves the immediate downstream compiler failure seen at prior FSM_API commit `46eab8253ad5a3ab2301e24262290b1e6bf3aed3`. The earlier failure and requested upstream validation are recorded in [FSM_API issue #36](https://github.com/TrentBest/FSM_API/issues/36); a downstream success update has been posted there, and the issue remains open for FSM_API's own validation.
- Package run #466: verification **passed**; publish job **skipped**. Package publication remains disabled.
- Azure integration tests use Azurite. No live production-account migration is claimed.

The filesystem parent-directory metadata durability gap is tracked separately in [issue #34](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/34). The Sith branch-policy gate remains blocked because the repository currently has 18 branches rather than exactly `master` and `development`. No branch has been certified ready for deletion by this audit; individual branch content and PR history still require review, followed by the user's explicit authorization for a single deletion batch.

This PR remains draft and unmerged. No package was published. No merge, release, deployment, `master` change, or branch deletion was performed.
