# TheSingularityWorkshop.MicroBundleRepository.FSM_COS

**Optional composition-side adapters for repository-backed MicroBundles.**

## ✳️ 00 — Identity

This package connects repository-delivered artifacts to FSM_COS composition. It is deliberately above repository Core and REST transport: consumers that only store, list, or retrieve artifacts do not need this integration package.

## 🟦 01 — The problem and short answer

Verified bytes are not yet a composed runtime capability. A composition host may opt into adapters that expose repository-backed artifacts through FSM_COS contracts. Keeping that step here prevents the repository itself from acquiring an upward dependency on FSM_COS.

## 🟣 02 — Boundary and dependencies

- `MicroBundleRepository.Core` owns artifact and storage contracts.
- `MicroBundleRepository.Rest` owns HTTP delivery.
- `MicroBundleRepository.FSM_COS` owns the optional composition integration.

This package is not the repository, not the MicroBundle domain definition, and not a general execution loop. It should be selected only by a host that needs this particular integration.

## 🩵 03 — Included adapters

- `RestMicroBundleCatalog` preloads a dependency closure from a REST repository and exposes it through `IMicroBundleCatalog`.
- `AssemblyMicroBundleArtifactMaterializer` turns a verified assembly artifact into an executable `IMicroBundle`.

Materialization is the point where a consumer-specific payload representation becomes a runtime object. Storage identity and optional semantic metadata remain distinct from FSM_COS's composition decisions.

## 🟢 04 — See it in a minute

Start with the [root repository walkthrough](../../../README.md) to see the artifact inventory and exact-address delivery boundary. Then use this package only in a consumer that already references compatible FSM_COS contracts.

There is no honest standalone runtime result to promise without a host, a valid assembly artifact, compatible dependencies, and a composition request. The automated materializer tests are the appropriate source-level proof for the adapter; the repository tests alone do not prove successful end-to-end runtime composition.

From the repository root, run:

```powershell
dotnet test TheSingularityWorkshop.MicroBundleRepository.slnx --configuration Release
```

## 🟪 05 — Further reading

- [Root README](../../../README.md) — ecosystem roles and artifact flow.
- [Repository architecture](../../../docs/ARCHITECTURE.md) — why materialization is outside storage Core.
- [REST adapter README](../TheSingularityWorkshop.MicroBundleRepository.Rest/README.md) — HTTP transport contract.
- [FSM_COS documentation standard](https://github.com/TrentBest/TheSingularityWorkshop.FSM_COS/blob/development/DOCUMENTATION_STANDARD.md).

Before release, verify that this project's declared FSM_COS/FSM_REST dependencies match the intended release line. Do not publish it simply because the source solution builds.
