# TheSingularityWorkshop.MicroBundleRepository.FSM_COS

Composition adapters that connect the MicroBundle Repository to **FSM_COS**.

This package deliberately sits above the repository transport:

`MicroBundleRepository.Core` → repository/storage contracts  
`MicroBundleRepository.Rest` → REST delivery  
`MicroBundleRepository.FSM_COS` → FSM_COS composition integration

The repository itself does not depend on FSM_COS. Hosts that want repository-backed
composition opt into this adapter package.

## Included adapters

- `RestMicroBundleCatalog` preloads a dependency closure from a REST repository and exposes it through `IMicroBundleCatalog`.
- `AssemblyMicroBundleArtifactMaterializer` turns a verified assembly artifact into an executable `IMicroBundle`.

The package is intentionally an integration boundary, not part of repository Core or REST transport.
