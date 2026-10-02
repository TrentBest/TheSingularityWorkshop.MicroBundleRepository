# TheSingularityWorkshop.MicroBundleRepository.Core

**Platform-neutral contracts and immutable artifact identity for MicroBundle repositories.**

This package is the contract layer between MicroBundle composition and physical storage.

It deliberately does **not** know about Azure Blob Storage, ASP.NET Core, REST transport, databases, authentication, or GUI frameworks.

## Why this package exists

A MicroBundle is executable capability. Once an artifact is built, a composition system needs a precise way to request the exact bytes it was configured to use.

The Core package gives that artifact three pieces of identity:

~~~text
MicroBundle ID
      +
explicit version
      +
SHA-256 content identity
~~~

The bytes are authoritative. A MicroBundleArtifact verifies the SHA-256 of its content before it can exist as a valid artifact.

That gives storage implementations a strong boundary:

~~~text
FSM_COS
   |
   | requested artifact identity
   v
IMicroBundleRepository
   |
   | verified bytes
   v
MicroBundleArtifact
   |
   v
composition-side materializer
~~~

## Core responsibilities

The package provides:

- IMicroBundleRepository
- MicroBundleArtifactAddress
- MicroBundleArtifact
- MicroBundleAssemblyPayload
- IExperienceRepository
- ExperienceArtifactAddress
- ExperienceArtifact
- IExperienceCatalog
- ExperiencePublication

These types define identity, verification, immutable artifact exchange, and Experience publication pointers.

They do **not** decide how MicroBundles are composed.

## MicroBundle artifact identity

~~~csharp
var artifact = MicroBundleArtifact.Create(
    bundleId: 7001,
    version: "1.0.0",
    content: assemblyBytes);
~~~

The address is deterministic:

~~~text
artifacts/{bundleId}/{version}/{sha256}.bundle
~~~

The Core contract rejects invalid IDs, path-unsafe versions, malformed SHA-256 values, and content whose digest does not match the declared address.

The artifact also copies the supplied bytes, so later mutation of a caller-owned buffer cannot change the artifact.

## MicroBundle assembly payload

The first defined payload representation is a compact binary envelope:

~~~text
FSMB
format version
MicroBundle ID
assembly byte length
assembly bytes
~~~

It is intentionally separate from the repository address.

The payload describes the representation.

The artifact address describes the immutable bytes.

Malformed payloads are rejected, including invalid magic, unsupported format versions, zero IDs, empty assembly data, truncated payloads, trailing bytes, and oversized payloads.

## Experience artifacts

Experience artifacts use the same content-addressed model:

~~~text
artifacts/{experienceId}/{version}/{sha256}.experience
~~~

Publication is a separate pointer.

~~~text
immutable artifact
       |
       +--> version 1
       +--> version 2
       +--> version 3

publication
       |
       +--> currently selected artifact
~~~

IExperienceRepository owns immutable artifact delivery.

IExperienceCatalog owns the publication pointer.

Unpublishing removes the pointer; it does not delete immutable artifact bytes.

## Implementing your own repository

The contract is storage-provider neutral.

A repository implementation needs to:

1. accept a complete artifact address;
2. locate the bytes;
3. construct the Core artifact;
4. allow Core to verify the SHA-256;
5. return the verified artifact.

For writes, it should preserve the complete content-addressed identity and treat an existing address as immutable.

That makes the same contract usable for Azure Blob Storage, local development storage, another object store, a private vendor repository, or a repository operated by another organization.

The Core package does not grant trust or admission. Those are ecosystem governance decisions above the repository boundary.

## Relationship to the other layers

~~~text
MicroBundleDomain
    |
    | semantic vocabulary
    v
MicroBundleRepository.Core
    |
    | artifact identity + delivery contract
    v
storage implementation
    |
    v
verified bytes
    |
    v
FSM_COS
    |
    | dependency closure + arbitration + convergence
    v
RuntimeAssembly
~~~

The separation is intentional:

- MicroBundleDomain describes the MicroBundle.
- Repository.Core identifies and verifies the artifact.
- Repository implementations move durable bytes.
- FSM_COS composes capabilities.
- Experience systems decide how the resulting assembly is used.

## Documentation

The deeper contract, rationale, extension model, testing philosophy, and release checklist are documented in docs/CORE_CONTRACT.md.

The repository-level architecture and Azure setup guides provide the infrastructure context.

## Development

Build and test the explicit solution:

~~~powershell
dotnet restore TheSingularityWorkshop.MicroBundleRepository.slnx
dotnet build TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
~~~

The Core package itself has no live-cloud requirement.

## Release posture

This package is being prepared for human review as a potential 1.0.0.

**Preparation is not publication.**

No NuGet publication is performed by the normal repository workflow. Release approval remains a separate human decision.