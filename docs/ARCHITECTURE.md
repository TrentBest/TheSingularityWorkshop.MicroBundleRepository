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

Every artifact is addressed by:

- MicroBundle ID
- explicit version
- SHA-256 content identity

The SHA-256 is not merely metadata. The repository constructs the immutable artifact only after verifying that the bytes match the declared content identity.

## Azure layout

The first storage implementation uses one private container:

```text
microbundles/
└── artifacts/
    └── {bundleId}/
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
