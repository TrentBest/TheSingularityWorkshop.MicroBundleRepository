# TheSingularityWorkshop.MicroBundleRepository.Core

Platform-neutral repository contracts and immutable MicroBundle artifact identity.

This package defines the boundary between MicroBundle composition and physical storage. It does not reference Azure, ASP.NET Core, databases, or any storage SDK.

## Contracts

- `IMicroBundleRepository` stores and retrieves artifacts by complete content address.
- `MicroBundleArtifactAddress` identifies a bundle ID, version, and SHA-256.
- `MicroBundleArtifactListRequest` defines a bounded discovery query.
- `MicroBundleArtifactListPage` returns artifact identities and an opaque continuation token.

## Discovery is not retrieval

`ListAsync` returns identities only. It does not download payload bytes, infer dependencies, materialize a bundle, or declare that a bundle is compatible with a host.

The default page size is 100 and the maximum is 500. A caller passes the returned continuation token unchanged to request the next page. A caller can optionally filter by bundle ID and, when a bundle ID is supplied, by exact version.

This contract keeps storage discovery separate from MicroBundle meaning and runtime composition.
