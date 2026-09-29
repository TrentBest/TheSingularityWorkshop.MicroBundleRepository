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
