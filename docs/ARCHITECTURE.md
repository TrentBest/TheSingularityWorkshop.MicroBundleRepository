# MicroBundle Repository Architecture

The repository is the durable supply boundary for MicroBundle artifacts.

It does **not** compose bundles, arbitrate dependencies, host Experiences, or provide query/database semantics.

## Boundary

```text
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
```

## Artifact identity

The portable identity of an immutable artifact is the tuple:

- canonical ontology string
- explicit version
- SHA-256 content identity

The Core contract exposes this tuple as `MicroBundleArtifactIdentity`. Ontology identifiers are ordinal and case-sensitive, must already be Unicode Form C normalized, and reject whitespace and route/path delimiters. The SHA-256 is not merely metadata: artifact bytes must match the declared digest before they are accepted.

This portable identity is deliberately separate from the current runtime-facing `MicroBundleArtifactAddress`, which still carries the numeric Domain `BundleId`. The numeric ID is not silently reinterpreted as an ontology string, and no repository-local storage handle is exposed as portable identity. The adapter and FSM_COS binding migration remains a separate integration step.

A repository-local storage key is an implementation detail. It must never be used as the portable identity or persisted into a published Experience manifest.

## Azure layout

The first storage implementation uses one private container:

```text
microbundles/
└── artifacts/
    └── {repository-local-key}/
        └── {version}/
            └── {sha256}.bundle
```

This is intentionally boring. A complete address maps directly to one blob. No database lookup is required.

## What Azure does not do

Azure Blob Storage does not:

- resolve MicroBundle dependencies
- arbitrate bundles
- decide Experience composition
- maintain RuntimeAssembly state
- become the MicroBundle catalog
- become a general-purpose application database

Those concerns belong elsewhere in the architecture.


## Simulation-scale artifact delivery

See [Artifact Delivery at Simulation Scale](SIMULATION_SCALE.md) for the distinction between immutable capability artifacts and high-frequency simulation state, including integrity, materialization, caching, and checkpoint boundaries.

## Experience artifacts

Experiences use the same durable delivery pattern without coupling storage to the WebForge Experience model.

```text
Published Experience definition
          |
          v
IExperienceRepository
          |
          v
Azure Blob Storage
          |
          v
verified Experience bytes
          |
          v
FSM_COS
   dependency closure
   load / arbitration
   RuntimeAssembly
```

Experience artifact identity is:

- Experience ID
- explicit version
- SHA-256 content identity

The Azure layout is:

```text
experiences/
└── artifacts/
    └── {experienceId}/
        └── {version}/
            └── {sha256}.experience
```

The repository stores opaque serialized bytes. It does not own IExperience, GUI behavior, MicroBundle execution, or composition policy. This keeps published Experience storage usable by WebForge and other hosts.
