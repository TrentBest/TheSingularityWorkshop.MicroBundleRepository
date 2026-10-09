# The Singularity Workshop — MicroBundle Repository

[![NuGet version](https://img.shields.io/nuget/v/TheSingularityWorkshop.MicroBundleRepository?style=flat-square&logo=nuget&logoColor=white)](https://www.nuget.org/packages/TheSingularityWorkshop.MicroBundleRepository)
[![NuGet downloads](https://img.shields.io/nuget/dt/TheSingularityWorkshop.MicroBundleRepository?style=flat-square&logo=nuget&logoColor=white)](https://www.nuget.org/packages/TheSingularityWorkshop.MicroBundleRepository)
[![Build](https://img.shields.io/github/actions/workflow/status/TrentBest/TheSingularityWorkshop.MicroBundleRepository/build.yml?branch=master&style=flat-square&logo=github)](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/TrentBest/TheSingularityWorkshop.MicroBundleRepository?style=flat-square)](LICENSE.txt)

<p align="center">
  <img src="docs/images/microbundle-repository-boundary.svg" alt="MicroBundle Repository is the durable artifact-delivery boundary between domain-owned MicroBundles and composition hosts." width="1100">
</p>

<p align="center">
  <strong>Where MicroBundles live before they become runtime.</strong><br>
  <em>Locate the artifact. Verify the bytes. Deliver the capability. Stop there.</em>
</p>

## ✳️ 00 — Identity

**MicroBundle Repository stores and delivers immutable, versioned MicroBundle artifacts.** It defines the “where” boundary in the Workshop's “what / where / how” architecture.

This repository is intentionally narrower than a database or runtime. It owns artifact identity, deterministic storage addresses, durable retrieval, integrity verification, and identity-only inventory. It does not decide what a bundle means or how capabilities are composed.

## 🟦 01 — The problem and the short answer

A host may know that it needs a MicroBundle without already possessing the exact bytes. It needs a stable way to identify, store, discover, and retrieve a particular artifact without coupling its runtime to a storage vendor.

The answer is a small platform-neutral repository contract, with storage adapters behind it. The artifact address names a bundle ID, explicit version, and SHA-256 content identity. Listing can discover stored identities; exact-address retrieval remains a separate operation.

## 🟣 02 — Where this fits in the Workshop

| Question | Responsibility |
|---|---|
| **What is this capability?** | [MicroBundleDomain](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain) |
| **Where are its immutable artifact bytes?** | **MicroBundle Repository** |
| **How are capabilities composed and arbitrated?** | [FSM_COS](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS) |

These are responsibility boundaries, not mandatory dependencies. A consumer can implement another repository, use the domain package without this storage adapter, or choose a different host. Core should not depend upward on FSM_COS; composition-side materialization belongs outside the storage contract.

The architectural invariant is:

> **Blob Storage is the substrate. The repository is the delivery boundary. FSM_COS is the composition boundary.**

## 🩵 03 — The mental model and contract

An artifact has a complete immutable address:

```text
MicroBundle ID + explicit version + SHA-256 content identity
```

For Azure Blob Storage, the deterministic artifact path is:

```text
microbundles/
└── artifacts/
    └── {bundleId}/
        └── {version}/
            └── {sha256}.bundle
```

The repository locates and returns bytes for a complete address, verifies content identity, and can list stored identities without downloading every payload. A list result is not a dependency graph, publication decision, compatibility verdict, or permission to execute the artifact.

An optional Ontology semantic address may accompany an artifact as metadata; it does not alter the content hash or deterministic storage path. The Azure adapter preserves that metadata separately, while identity-only listing remains deliberately unaware of semantic meaning.

## 🟢 04 — See it in a minute

The most direct first proof is the artifact-inventory contract exposed by the REST host. With the host running, request a page of stored artifact identities:

```http
GET /api/microbundles?pageSize=100
```

You can narrow the inventory or continue a page:

```http
GET /api/microbundles?bundleId=2110&version=1.0.0
GET /api/microbundles?pageSize=100&continuationToken={opaque-token}
```

The response contains artifact identity fields (`bundleId`, `version`, and `contentHash`) and may include a continuation token. An empty inventory is a valid result when no artifacts have been stored. Listing does not return payload bytes; retrieve a selected artifact by its complete address in a separate operation.

To build and run the repository's automated checks from source:

```powershell
dotnet restore TheSingularityWorkshop.MicroBundleRepository.slnx
dotnet build TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

These commands validate the source and tests, not a live Azure deployment. Live Azure-backed listing requires configured credentials and has not been claimed as part of the automated CI proof. See [Azure setup](docs/AZURE_SETUP.md) before attempting a real storage account.

## 🟪 05 — Documentation map

- [Architecture](docs/ARCHITECTURE.md) — why storage, artifact identity, and composition are separate boundaries.
- [Azure setup](docs/AZURE_SETUP.md) — configure the first durable storage adapter and credentials.
- [Core package README](src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/README.md) — platform-neutral artifact and repository contracts.
- [Azure package README](src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/README.md) — Blob Storage implementation and operational requirements.
- [REST package README](src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/README.md) — HTTP transport boundary.
- [FSM_COS integration README](src/REST/TheSingularityWorkshop.MicroBundleRepository.FSM_COS/README.md) — composition-side materialization, where that project is present in this branch.
- [FSM_COS documentation standard](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS/blob/development/DOCUMENTATION_STANDARD.md) — Workshop-wide documentation intent and presentation guidance.
- [Branch reconciliation](docs/BRANCH_RECONCILIATION.md) — current branch dispositions and evidence required before deletion.

Package versions in project files describe source state, not proof that a version is published on NuGet. Verify each package and its transitive dependency versions before treating it as installable. No package should be released until its own README and relevant usage/contract documentation meet the Workshop standard.


## Projects

~~~text
src/
├── Core/
│   └── TheSingularityWorkshop.MicroBundleRepository.Core
│       └── platform-neutral contracts and artifact identity
├── Azure/
│   └── TheSingularityWorkshop.MicroBundleRepository.Azure
│       └── Azure Blob Storage adapter
└── REST/
    ├── TheSingularityWorkshop.MicroBundleRepository.Rest
    │   └── HTTP client adapter
    ├── TheSingularityWorkshop.MicroBundleRepository.Rest.Host
    │   └── REST API host
    └── TheSingularityWorkshop.MicroBundleRepository.FSM_COS
        └── optional composition-side integration

tests/
├── Core/  └── artifact identity and contract tests
├── Azure/ └── blob address, metadata, and adapter tests
└── REST/  └── transport and API contract tests
~~~

### Core

`TheSingularityWorkshop.MicroBundleRepository.Core` has no Azure dependency.

Its central contract is:

~~~csharp
public interface IMicroBundleRepository
{
    ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default);

    ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default);
}
~~~

### Azure

`TheSingularityWorkshop.MicroBundleRepository.Azure` uses the Microsoft Azure SDK and `DefaultAzureCredential`.

That means the storage account key does not belong in application configuration or source control. Local development can authenticate through Visual Studio or Azure CLI; Azure-hosted applications can later use managed identity without changing the repository contract.

## Azure configuration

The first deployment target is intentionally minimal:

- StorageV2
- Standard performance
- LRS redundancy
- Hot access tier
- one private `microbundles` container
- public blob access disabled
- secure transfer enabled
- TLS 1.2 minimum
- no database
- no CDN
- no Front Door
- no geo-replication

See [Azure setup](docs/AZURE_SETUP.md) for the portal and CLI configuration.

See [architecture](docs/ARCHITECTURE.md) for the full repository boundary.

## What this repository does not do

It does not:

- discover semantic dependencies
- resolve MicroBundle dependency graphs
- arbitrate installed bundles
- construct RuntimeAssembly
- own Experience composition
- host WebForge, Unity, MyVR, or another presentation surface
- provide general-purpose CRUD/query persistence

Those boundaries matter.

The first concrete payload format is a compiled MicroBundle assembly wrapped in a small domain-owned binary envelope supplied through `TheSingularityWorkshop.FSM_Serialization`. The repository still stores and verifies opaque bytes; the composition-side materializer interprets the envelope and binds the requested `IMicroBundle`.

~~~text
MicroBundle assembly
      ↓
FSM_Serialization envelope
      ↓
SHA-256 artifact identity
      ↓
Azure Blob Storage
      ↓
verified bytes
      ↓
composition-side materializer
      ↓
IMicroBundle
~~~

The repository answers:

> **Given this complete MicroBundle artifact address, where are its bytes and can I verify them?**

FSM_COS answers:

> **Given the requested MicroBundles, how do I compose and arbitrate them into a RuntimeAssembly?**

## Development

Build and test the explicit solution rather than relying on folder inference:

~~~powershell
dotnet restore TheSingularityWorkshop.MicroBundleRepository.slnx
dotnet build TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
~~~

The Azure integration test path will be added once the real development storage account is provisioned; the current test suite intentionally has no hidden dependency on a live cloud resource.

## Why Blob Storage first?

MicroBundles are artifacts.

A repository should be able to move an artifact from durable storage to a resident/local cache without introducing a query engine between the request and the bytes.

That gives us the shape we want:

~~~text
artifact address
      ↓
deterministic blob location
      ↓
bytes
      ↓
SHA-256 verification
      ↓
local/resident cache
      ↓
FSM_COS
~~~

Storage can evolve later. The composition contract does not need to know that it was Azure today.


## Experience discovery and publication state

The Experience artifact repository is immutable. Publication is a separate pointer that identifies which immutable artifact is currently live.

The REST host exposes the first discovery path for hosts such as AnyApp:

```text
GET /api/experiences
GET /api/experiences/{experienceId}
GET /api/experiences/{experienceId}/{version}/{sha256}
```

The discovery endpoint returns only published artifact identity. It does not deserialize or execute the Experience. The selected artifact bytes remain the host/Experience boundary's responsibility.


~~~text
experiences/
├── artifacts/{experienceId}/{version}/{sha256}.experience
└── publications/{experienceId}.publication.json
~~~

The publication lifecycle is:

~~~text
publish v1
    ↓
publication → v1
    ↓ modify
publication → v2
    ↓ unpublish
no publication
~~~

`IExperienceCatalog` owns that pointer. Unpublishing removes the pointer; it does not delete the immutable artifact bytes. This keeps artifact identity, publication state, and FSM_COS composition as separate boundaries.

~~~csharp
public interface IExperienceCatalog
{
    ValueTask<ExperiencePublication?> GetPublishedAsync(...);
    ValueTask PublishAsync(ExperiencePublication publication, ...);
    ValueTask UnpublishAsync(ulong experienceId, ...);
}
~~~

That gives an Experience a durable path from definition to consumption:

~~~text
Experience definition
       ↓
serialize
       ↓
immutable artifact
       ↓
Azure Blob Storage
       ↓
publication pointer
       ↓
FSM_COS
       ↓
RuntimeAssembly
~~~


## Artifact discovery

A repository must let a host discover stored artifacts without already knowing every complete address. The MicroBundle repository therefore exposes a paginated identity-listing API alongside exact-address retrieval.

~~~http
GET /api/microbundles?pageSize=100
GET /api/microbundles?bundleId=2110
GET /api/microbundles?bundleId=2110&version=1.0.0
GET /api/microbundles?pageSize=100&continuationToken={opaque-token}
~~~

The response lists `bundleId`, `version`, and `contentHash` for each artifact, plus an optional continuation token. It does not download payloads or imply that an artifact is compatible, published, or ready for composition. Page sizes are bounded from 1 through 500.

This gives a host a first step to **see what is in the repository**. It can then retrieve a chosen immutable artifact by its complete address, validate/materialize it, and let FSM_COS handle composition. Discovery, artifact retrieval, and runtime composition remain separate operations.
