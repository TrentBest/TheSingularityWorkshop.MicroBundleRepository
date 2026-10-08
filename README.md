# The Singularity Workshop — MicroBundle Repository

**The durable artifact boundary for MicroBundles.**

This repository stores and delivers versioned MicroBundle artifacts. It deliberately does not become a database, composition engine, arbitration engine, Experience host, or GUI.

## What and Why

MicroBundleRepository owns the durable artifact-delivery boundary: locate and retrieve immutable, versioned MicroBundle bytes, verify their content identity, and materialize them for a consumer. It does not interpret a bundle's domain, resolve composition dependencies, or execute a runtime.

The repository project files currently declare alpha package versions. Confirm each exact package/version on NuGet before relying on it; a source `<Version>` field is not proof of publication.

## 60-Second Quick Start

This is a multi-project infrastructure repository, so the most reliable first step is to open and verify the source.

### 1. Open the solution in Visual Studio

Clone the repository and open `TheSingularityWorkshop.MicroBundleRepository.slnx` in Visual Studio.

### 2. Open the Developer Terminal

Choose **View → Terminal** in Visual Studio and ensure the terminal is at the repository root.

### 3. Restore, build, and test

```powershell
dotnet restore TheSingularityWorkshop.MicroBundleRepository.slnx
dotnet build TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release --no-restore
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

These commands validate the current source and its test projects. They do not require you to publish an artifact or configure a production storage account.

## Add It to an Existing Project

Already have an application? Depend on the smallest responsibility you need.

- **Need the storage/retrieval contract?** Reference `TheSingularityWorkshop.MicroBundleRepository.Core` only.
- **Need Azure Blob Storage?** Add the Azure implementation and configure `DefaultAzureCredential`; keep credentials out of source control.
- **Need HTTP delivery?** Use the REST adapter/host only when your deployment requires that transport.
- **Need runtime composition?** Keep the adapter that materializes verified artifact bytes into runtime MicroBundles outside the repository package. The repository must not depend upward on FSM_COS.

Until the exact Core package version is verified as published, add a local project reference from your application project (adjust the relative path to your clone):

```powershell
dotnet add reference ..\\TheSingularityWorkshop.MicroBundleRepository\\src\\Core\\TheSingularityWorkshop.MicroBundleRepository.Core\\TheSingularityWorkshop.MicroBundleRepository.Core.csproj
```

The current REST adapter project declares FSM_COS alpha.3 and FSM_REST alpha.4 dependencies. Treat that adapter as a separate compatibility check against the current alpha.6 line; do not assume the source project graph is already aligned.

## The boundary

~~~text
Experience / published manifest
          |
          v
   local bundle cache
          |
          | missing artifact
          v
IMicroBundleRepository
          |
          v
Azure Blob Storage
          |
          v
 verified artifact bytes
          |
          v
       FSM_COS
   dependency closure
   load once
   arbitration
   convergence
          |
          v
   RuntimeAssembly
~~~

The architectural invariant is simple:

> **Blob Storage is the substrate. The repository is the delivery boundary. FSM_COS is the composition boundary.**

## Artifact identity

An artifact is addressed by three values:

~~~text
MicroBundle ID
      +
explicit version
      +
SHA-256 content identity
~~~

The physical Azure location is deterministic:

~~~text
microbundles/
└── artifacts/
    └── {bundleId}/
        └── {version}/
            └── {sha256}.bundle
~~~

No database lookup is required to locate a complete artifact address.

The repository verifies the SHA-256 before materializing the immutable artifact object. Storage metadata repeats the identity for inspection, but the bytes remain authoritative.

## Projects

~~~text
src/
├── Core/
│   └── TheSingularityWorkshop.MicroBundleRepository.Core
│       └── platform-neutral contracts and artifact identity
├── Azure/
│   └── TheSingularityWorkshop.MicroBundleRepository.Azure
│       └── Azure Blob Storage implementation
├── REST/
│   ├── TheSingularityWorkshop.MicroBundleRepository.Rest
│   │   └── REST delivery adapter
│   └── TheSingularityWorkshop.MicroBundleRepository.Rest.Host
│       └── REST API host
└── CLI/
    └── TheSingularityWorkshop.MicroBundleRepository.Cli
        └── platform-neutral human operational client

tests/
├── Core/
│   └── deterministic artifact identity tests
├── Azure/
│   └── deterministic Azure path/configuration tests
└── REST/
    └── REST boundary tests
~~~

### Core

`TheSingularityWorkshop.MicroBundleRepository.Core` has no Azure, GUI, or host dependency.

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

## Human operation without GUI

The repository now has a deliberately small console client:

~~~text
CLI
 |
 | HTTP
 v
Repository REST API
 |
 v
IMicroBundleRepository
 |
 v
Azure Blob Storage
~~~


Set `MICRO_BUNDLE_REPOSITORY_URL` or pass `--url`:

~~~powershell
$env:MICRO_BUNDLE_REPOSITORY_URL = "http://localhost:5000/"
dotnet run --project src/CLI/TheSingularityWorkshop.MicroBundleRepository.Cli -- health
~~~

Inspect the repository:

~~~powershell
dotnet run --project src/CLI/TheSingularityWorkshop.MicroBundleRepository.Cli -- list
~~~

Publish a local artifact:

~~~powershell
dotnet run --project src/CLI/TheSingularityWorkshop.MicroBundleRepository.Cli -- put 2110 1.0.0 .undle.bin
~~~

Retrieve a known artifact:

~~~powershell
dotnet run --project src/CLI/TheSingularityWorkshop.MicroBundleRepository.Cli -- get 2110 1.0.0 <sha256> .undle.bin
~~~

The CLI computes the SHA-256 from the supplied bytes and the existing Core artifact contract verifies the identity before the request is sent. That gives us a simple human-operated path for publishing and inspecting the small MicroBundles that will eventually compose the zeroth ontology:

**The Singularity Workshop.**

A future GUI, Workshop mailbox, or other client can use the same REST boundary without changing the repository contract.

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
- provide general-purpose CRUD/query persistence

Those boundaries matter.

The repository answers:

> **Given this complete MicroBundle artifact address, where are its bytes and can I verify them?**

For operational observation, the repository host also exposes an artifact inventory. This is deliberately narrower than semantic discovery: it reports stored artifact identity, size, and storage metadata without resolving dependencies or assigning meaning.

The Workshop can manifest repository delivery through explicit **mailboxes**. A mailbox is an intake/delivery surface over a declared destination; the underlying repository still performs deterministic, content-addressed storage.

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

## Experience publication state

The Experience artifact repository is immutable. Publication is a separate pointer that identifies which immutable artifact is currently live.

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
