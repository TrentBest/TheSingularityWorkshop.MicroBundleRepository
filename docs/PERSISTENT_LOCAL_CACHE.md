![The Singularity Workshop moniker](https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop.Renderer/development/docs/assets/moniker.svg)

# Persistent local MicroBundle cache

The local cache adapter keeps verified MicroBundle artifact bytes on disk between application runs. It is a repository adapter, not an Experience manifest manager and not part of the FSM_COS composition kernel.

## What happens on an artifact request

1. Look for the exact artifact address on disk: bundle ID + explicit version + SHA-256.
2. If the file exists, reconstruct the immutable artifact; the Core artifact type verifies its content hash.
3. If the file is missing or fails hash verification, retrieve the exact address from the origin repository.
4. Verify identity, then persist the artifact for later runs.
5. A later process can use the disk hit without contacting the origin.

The cache stores immutable entries at:

    {cacheRoot}/{bundleId}/{version}/{sha256}.bundle

Different versions and different content hashes occupy different paths. Cache hits do not mean “latest”; they mean “these exact bytes.”

## Write behavior

CachingMicroBundleRepository.PutAsync writes to the origin first and then warms the local cache. If the origin write fails, the adapter does not report a successful write merely because local bytes could be saved.

## What this does not solve

The adapter deliberately does not own:

- the current published Experience manifest/version check;
- manifest-to-artifact consumer references;
- editor notifications or adoption of MicroBundle updates;
- active runtime leases;
- deferred garbage collection of artifacts no longer referenced by any Experience;
- publisher authenticity or authorization.

Those are separate lifecycle contracts. A matching SHA-256 establishes byte integrity, not publisher trust. A host should keep the manifest version record and reference/lease metadata durable, then reconcile exact artifact addresses after a manifest changes. Old files must not be deleted until no manifest references, runtime leases, or in-flight operations remain.

## Tests

The Local test project covers cold retrieval and persistence across adapter instances, missing artifacts, corrupted cache replacement, version isolation, and cancellation. Run the full solution tests from the repository root:

    dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release

---

<p align="center">
  <img src="https://raw.githubusercontent.com/TrentBest/TheSingularityWorkshop/development/Avatar.jpg" alt="The Singularity Workshop avatar" width="96" />
</p>
