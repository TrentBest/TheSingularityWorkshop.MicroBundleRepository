# MicroBundle Repository Core — Contract Guide

## Purpose

TheSingularityWorkshop.MicroBundleRepository.Core is the platform-neutral contract layer for a MicroBundle repository.

It answers one deliberately narrow question:

> How do we identify, verify, and exchange an immutable MicroBundle artifact without coupling the composition system to a storage provider?

The package deliberately does not own Azure Blob Storage, REST transport, ASP.NET Core, authentication, caching, dependency arbitration, or Experience hosting. Those concerns belong outside this package.

## Why a repository contract exists

A MicroBundle is executable capability. Once a MicroBundle has been built and published, the composition system needs a durable way to request the exact artifact it was configured to use.

There are three related identities:

~~~text
semantic identity
      |
      +-- MicroBundle ID
      +-- explicit version
      |
content identity
      |
      +-- SHA-256 of the actual bytes
      |
physical location
      |
      +-- storage-specific path generated from the above
~~~

The Core package defines the complete artifact identity while allowing each storage implementation to choose its physical substrate.

## The repository boundary

The central contract is intentionally small:

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

GetAsync is an exact lookup. It does not perform semantic discovery, dependency resolution, arbitration, or composition.

PutAsync stores an already-identified immutable artifact. It does not publish an Experience or decide whether a MicroBundle is trusted.

~~~text
Manifest
   |
   v
FSM_COS determines requested bundle identities
   |
   v
IMicroBundleRepository
   |
   v
verified artifact bytes
   |
   v
composition-side materializer
   |
   v
IMicroBundle
~~~

The repository is therefore a delivery boundary, not a composition engine.

## Artifact identity

MicroBundleArtifactAddress contains:

- BundleId — the stable MicroBundle identity.
- Version — the explicit version selected by the publisher or manifest.
- ContentHash — the lowercase hexadecimal SHA-256 digest of the complete artifact bytes.

The constructor rejects zero IDs, missing versions, path separators in versions, and content hashes that are not exactly 64 hexadecimal characters.

The path-segment rule matters because the address is later projected into a storage path. The Core contract prevents a version from becoming an accidental path expression.

The hash is normalized to lowercase so equivalent hexadecimal spellings have one canonical representation.

## Constructing verified artifacts

The normal creation path is:

~~~csharp
var artifact = MicroBundleArtifact.Create(
    bundleId: 7001,
    version: "1.0.0",
    content: assemblyBytes);
~~~

Create calculates SHA-256 from the supplied bytes and constructs the complete address.

When reconstructing an artifact from storage, construct MicroBundleArtifact from the stored address and retrieved bytes. Construction recalculates SHA-256 and rejects mismatched bytes.

This means a storage adapter cannot silently return different bytes for an address and still produce a valid Core artifact.

## Immutability

The artifact copies its input bytes.

~~~text
caller buffer
     |
     +--> copied into artifact
             |
             +--> SHA-256 verified
             |
             +--> immutable artifact state
~~~

Content is exposed as ReadOnlyMemory<byte>, while the backing array remains private.

The same ownership rule applies to Experience artifacts.

## MicroBundle assembly payload

The repository stores opaque artifact bytes. The first defined MicroBundle representation is a compact binary envelope around a compiled assembly.

The envelope carries:

- magic: FSMB;
- format version: 1;
- MicroBundle ID;
- assembly byte length;
- assembly bytes.

The payload does not contain the repository SHA-256 address. Hashing belongs to the artifact boundary; the payload describes the representation being transported.

~~~text
MicroBundleAssemblyPayload
        |
        v
opaque payload bytes
        |
        v
MicroBundleArtifact
        |
        +-- BundleId
        +-- Version
        +-- SHA-256
~~~

FromBytes validates the complete stream. It rejects empty input, invalid magic, unsupported versions, zero IDs, empty assembly payloads, oversized payloads, truncated data, and trailing bytes.

The strict complete-stream rule keeps the binary representation deterministic.

## Experience artifacts

Experiences use the same durable artifact pattern through ExperienceArtifactAddress.

It contains ExperienceId, explicit Version, and SHA-256 ContentHash.

The deterministic representation is:

~~~text
artifacts/{experienceId}/{version}/{sha256}.experience
~~~

Experience artifacts are immutable bytes. Publication is separate.

~~~text
immutable artifacts
        |
        +--> v1
        +--> v2
        +--> v3

publication pointer
        |
        +--> currently selected artifact
~~~

ExperiencePublication is the pointer. Publishing a new version changes the pointer; it does not rewrite immutable artifact identity. Unpublishing removes the pointer without deleting the artifact.

The Core package therefore separates IExperienceRepository from IExperienceCatalog.

## What Core deliberately does not know

The package does not define:

- Azure Blob clients;
- storage account credentials;
- REST endpoints;
- ASP.NET Core;
- authentication or authorization;
- repository federation;
- human admission policy;
- licensing;
- MicroBundle dependency closure;
- arbitration;
- RuntimeAssembly construction;
- Experience rendering;
- GUI behavior;
- local cache policy.

Those are infrastructure, governance, composition, or presentation concerns.

## Building another repository implementation

A new repository implementation needs only to honor the Core invariants.

For reads:

1. Accept a complete MicroBundleArtifactAddress.
2. Locate the corresponding bytes in the implementation storage.
3. Construct a MicroBundleArtifact.
4. Let Core verify SHA-256.
5. Return the verified artifact.

For writes:

1. Accept a MicroBundleArtifact.
2. Use its complete address as the storage key.
3. Store the bytes without changing them.
4. Preserve immutability for the complete address.

This makes the contract usable by Azure Blob Storage, a filesystem repository, another object store, or a private vendor repository.

## Relationship to MicroBundleDomain

MicroBundleDomain describes what a MicroBundle is at the domain and vocabulary boundary.

MicroBundleRepository.Core describes how a concrete MicroBundle artifact is identified and delivered.

~~~text
MicroBundleDomain
    |
    | semantic definition
    | identity/version/dependencies/providers
    v
MicroBundle artifact
    |
    | compiled representation
    v
MicroBundleRepository.Core
    |
    | durable artifact address
    v
Repository implementation
~~~

The repository must not absorb the semantic responsibilities of MicroBundleDomain, and MicroBundleDomain should not become responsible for physical storage.

## Relationship to FSM_COS

FSM_COS is the composition boundary. The repository supplies verified bytes. FSM_COS determines what to load, resolves dependency closure, performs arbitration, and produces the runtime assembly.

~~~text
Repository
   | verified bytes
   v
materializer
   | IMicroBundle
   v
FSM_COS
   |
   +--> dependency closure
   +--> configuration
   +--> arbitration
   +--> convergence
   v
RuntimeAssembly
~~~

Keeping this distinction prevents the repository from becoming a second composition engine.

## Third-party repositories and federation

The contract is suitable for repositories owned by other organizations.

A vendor can operate its own artifact repository and retain control over its artifacts, storage, licensing, and release process. A host ecosystem can decide whether that repository is admitted for use.

That admission decision belongs above this package.

~~~text
vendor repository
       |
       v
admission / trust decision
       |
       v
available repository source
       |
       v
MicroBundle composition
~~~

The Core contract provides interoperability without automatically granting trust.

## Testing philosophy

Core tests focus on invariants rather than implementation details.

Important cases include:

- deterministic content hashes;
- canonical artifact identity;
- path-safe versions;
- valid SHA-256 syntax;
- defensive byte ownership;
- content/hash mismatch rejection;
- Experience identity validation;
- publication pointer validation;
- deterministic payload round trips;
- malformed payload rejection;
- truncated payload rejection;
- trailing-byte rejection.

The goal is not merely high line coverage. The goal is to make invalid states difficult to represent.

## Release checklist

Before this package is actually published as 1.0.0, verify:

- [ ] API surface has received human review.
- [ ] XML documentation is complete for public types and members.
- [ ] Package README explains why and intended use, not just installation.
- [ ] Artifact identity invariants are covered by tests.
- [ ] Payload parser failure modes are covered by tests.
- [ ] Experience artifact identity is covered by tests.
- [ ] Repository boundaries are documented.
- [ ] Dependency on FSM_Serialization is intentionally pinned.
- [ ] Release package contents are inspected with dotnet pack.
- [ ] Release build and all tests pass in GitHub Actions.
- [ ] No workflow path can publish without explicit release action.
- [ ] NuGet publication has not occurred.
- [ ] Human release approval is still pending.

The final item is intentional. Reaching this checklist is the precipice, not publication.

## Summary

TheSingularityWorkshop.MicroBundleRepository.Core is the small, portable contract underneath the physical repository implementations.

Its job is to make artifact identity and verification unambiguous.

> Domain vocabulary identifies the capability. Core identifies the artifact. Infrastructure stores it. FSM_COS composes it. Human governance decides which ecosystems participate.