# TheSingularityWorkshop.MicroBundleRepository.Core

**Platform-neutral contracts for immutable MicroBundle artifacts.**

## ✳️ 00 — Identity

Core defines the address and repository contract used to store, retrieve, and enumerate MicroBundle artifacts. It owns the vocabulary of artifact delivery—not Azure configuration, HTTP transport, ontology interpretation, or runtime composition.

## 🟦 01 — The problem and short answer

A consumer needs a stable way to identify a particular bundle artifact without coupling its code to a storage provider. Core models that identity and exposes `IMicroBundleRepository`; adapters implement the contract.

## 🟣 02 — Boundary and dependencies

Core is the lowest-level repository package. It must remain independent of Azure, ASP.NET Core, storage SDKs, and FSM_COS. Consumers may use Core with their own repository implementation or add an adapter when they need a concrete transport/storage mechanism.

## 🩵 03 — Contracts

- `MicroBundleArtifactAddress` identifies a bundle ID, explicit version, and SHA-256 content identity.
- `MicroBundleArtifact` holds an address, payload bytes, and optional semantic metadata.
- `IMicroBundleRepository` defines exact-address retrieval, immutable storage, and paginated identity listing.
- `MicroBundleArtifactListRequest` bounds a discovery query.
- `MicroBundleArtifactListPage` returns identities and an opaque continuation token.

**Discovery is not retrieval.** `ListAsync` returns artifact identities, not payload bytes. It does not infer dependencies, materialize bundles, or declare that a bundle is compatible with a host.

## 🟢 04 — See it in a minute

The list request can be used without knowing a complete artifact address in advance:

```csharp
var request = new MicroBundleArtifactListRequest(pageSize: 100);
var page = await repository.ListAsync(request, cancellationToken);

foreach (var address in page.Items)
{
    Console.WriteLine($"{address.BundleId} {address.Version} {address.ContentHash}");
}
```

This illustrates the contract shape; `repository` is an `IMicroBundleRepository` implementation supplied by the host. The result contains identities only. A returned continuation token is passed unchanged into the next request. The default page size is 100 and the maximum is 500.

Verify the current implementation from the repository root:

```powershell
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

## 🟪 05 — Further reading

- [Repository architecture](../../../docs/ARCHITECTURE.md) — storage, identity, and composition boundaries.
- [Root README](../../../README.md) — ecosystem context and the REST inventory example.
- [Workshop documentation standard](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS/blob/development/DOCUMENTATION_STANDARD.md) — documentation principles.

Package versions declared in source are not proof of publication. Confirm the exact package version and dependencies on NuGet before relying on a published install.
