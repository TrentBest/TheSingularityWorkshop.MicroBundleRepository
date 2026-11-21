# MicroBundleRepository.FSM_COS

FSM_COS integration adapters for artifacts delivered by MicroBundleRepository.

This package is deliberately separate from the Core, storage, and REST delivery packages. It owns the composition-facing bridge:

- maps an explicit **runtime MicroBundle ID** to a **repository-local artifact address**;
- retrieves and verifies artifact bytes through the REST repository client;
- materializes the expected runtime MicroBundle;
- preloads the exact-version dependency closure before synchronous FSM_COS catalog access.

## Identity boundary

The adapter accepts an explicit mapping:

```text
runtime/domain ID -> MicroBundleArtifactAddress(repository-local storage ID, version, SHA-256)
```

The key and the address's `BundleId` are intentionally independent. The adapter verifies the materialized bundle's `IMicroBundle.Id` and `MicroBundleDescriptor.Id` against the runtime key. It never assumes the repository's local storage handle is the runtime identity.

The source of the runtime-to-portable mapping remains owned by the Domain/host integration contract. See [MicroBundleDomain issue #28](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/28).

## Assembly materialization

`AssemblyMicroBundleArtifactMaterializer` is a starter implementation for artifacts whose content is a managed .NET assembly. Other artifact formats can provide another `IMicroBundleArtifactMaterializer`.

Assembly loading uses the default .NET load context in this initial adapter. Unloadable per-Experience isolation and dependency resolver policy remain separate lifecycle work.

## Usage

Construct a `RestMicroBundleCatalog` with a REST repository client, an explicit runtime-ID-to-repository-address map, and a materializer. Call `PreloadClosureAsync` before handing the catalog to synchronous FSM_COS composition.

This package does not own artifact storage, publisher authorization, semantic identity allocation, or Experience publication policy.
