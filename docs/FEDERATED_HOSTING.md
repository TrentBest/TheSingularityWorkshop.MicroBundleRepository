# Federated MicroBundle Hosting

## Purpose

The Singularity Workshop should be able to consume MicroBundles whose physical repository is **outside Workshop infrastructure** while still allowing those artifacts to participate in Workshop compositions.

This is not a special MicroBundle type. It is a repository topology.

## The model

```text
                    Ecosystem governance
                            |
                     admission decision
                            |
             +--------------+--------------+
             |              |              |
             v              v              v
       Workshop Repo    Publisher A    Publisher B
             |          Repository     Repository
             |              |              |
             +--------------+--------------+
                            |
                   repository adapter
                            |
                            v
                       FSM_COS
```

The external repository remains externally operated. The ecosystem retains control over whether and how that repository is admitted.

## What “entangled” means

An external repository can be entangled with the ecosystem through explicit, inspectable relationships:

- an admitted repository identity;
- artifact addresses referenced by manifests;
- publisher or organization metadata;
- capability/ontology metadata;
- licensing information;
- trust or admission records;
- Experience composition references.

None of those require the Workshop to become the owner of the external bytes.

## Recommended boundary

A federated repository implementation can implement the existing Core contract:

```csharp
IMicroBundleRepository
```

It can internally route an exact MicroBundleArtifactAddress to an admitted source.

```text
request
  |
  v
(bundleId, version, sha256)
  |
  v
admission / source routing
  |
  +--> local repository
  +--> publisher repository
  +--> private repository
  |
  v
bytes
  |
  v
Core SHA-256 verification
  |
  v
MicroBundleArtifact
```

This is preferable to teaching FSM_COS about every storage provider or publisher.

## Authority model

Federation should preserve separate authorities.

| Authority | Owns |
|---|---|
| Publisher | its repository, release process, and hosted artifacts |
| Storage provider | physical storage |
| Workshop / ecosystem governance | repository admission |
| FSM_COS | composition and arbitration |
| Experience | presentation and use |

These authorities can cooperate without becoming the same authority.

## Failure semantics

A federated repository should distinguish at least:

- source not admitted;
- artifact not found;
- artifact found but hash verification failed;
- source unavailable;
- artifact version unavailable;
- authorization failure.

A failed external source must never silently become a different artifact.

The complete requested address remains stable throughout resolution.

## Security and licensing boundary

Authentication, authorization, licensing, signatures, and admission policy are intentionally outside Repository.Core.

Core supplies deterministic artifact identity and verification.

A higher-level federation layer may add:

- signed repository manifests;
- publisher identity;
- allow/deny policy;
- license constraints;
- source health;
- credential handling;
- audit records.

Those features can evolve without changing the fundamental artifact contract.

## Example

A publisher could host a licensed digital toy repository:

```text
Publisher Repository
    |
    +-- Toy Character A
    +-- Toy Vehicle B
    +-- Licensed World C
```

An Experience manifest can reference exact artifact identities from that repository.

The Workshop can admit the repository for that Experience.

FSM_COS then composes the resulting capabilities normally.

The publisher never needs to move its artifact bytes into Workshop storage merely to participate.

## Non-goals

Federation does not mean:

- automatic trust;
- automatic licensing;
- automatic discovery of every Internet repository;
- bypassing artifact verification;
- copying all external artifacts locally;
- making external repositories part of the Core package.

The objective is **interoperability with controlled participation**.