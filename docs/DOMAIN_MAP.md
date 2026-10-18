# MicroBundle domain map

> Evidence-backed lifecycle map for MicroBundleDomain, MicroBundleRepository, and the FSM_COS composition boundary. This map describes the artifact-discovery candidate branch; it is not a claim that every lifecycle stage is production-proven.

**Status vocabulary:** **Implemented + unit-tested** means checked-in source and a corresponding test exist. **Implemented, proof incomplete** means code exists but the end-to-end or production path is not proven. **Gap / decision required** means it must not be presented as working behavior.

## Ownership map

| Concern | Owner | Evidence | Candidate status |
|---|---|---|---|
| Capability identity, dependencies, providers, load/arbitration contract | [MicroBundleDomain](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/IMicroBundle.cs) | [Domain architecture](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/ARCHITECTURE.md) | Domain contract exists; schema/default and Experience override policy remain tracked in [Domain #11](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/11) |
| Immutable MicroBundle address and SHA-256 verification | Repository Core | [Address](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/MicroBundleArtifactAddress.cs) · [Artifact](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/MicroBundleArtifact.cs) · [Core tests](../tests/Core) | Implemented; unit-tested |
| Durable MicroBundle bytes | Repository Core + Azure adapter | [Azure adapter](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureMicroBundleRepository.cs) | Implemented; Azure live-storage proof not claimed |
| Paginated artifact identity inventory | Repository Core + Azure + REST | [List request](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/MicroBundleArtifactListRequest.cs) · [Azure adapter](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureMicroBundleRepository.cs) · [REST host](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) | Implemented in source; adapter-level paging/filter/cancellation tests and host-level contract tests remain gaps |
| Ontology semantic address metadata | Domain semantic address + Azure blob metadata | [Azure tests](../tests/Azure) | Scalar and multidimensional metadata round-trip tests exist; metadata is not artifact identity or authorization |
| Immutable Experience bytes | Repository Core + Azure adapter | [Experience repository](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceRepository.cs) · [Azure implementation](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureExperienceRepository.cs) | Implemented in source; REST route and end-to-end proof incomplete |
| Mutable current-publication pointer | Repository Core + Azure catalog | [Catalog contract](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceCatalog.cs) · [Azure catalog](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureExperienceCatalog.cs) | Implemented in source; REST listing/lookup routes added in candidate; focused host tests remain a gap |
| REST transport and deployment composition | Repository REST host | [Program.cs](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) | Candidate maps MicroBundle inventory/get/put and Experience list/current-pointer/artifact routes. Azure configuration is required at startup; previous Local mode is removed in this candidate and needs an explicit compatibility decision |
| Composition-side materialization and dependency closure | FSM_COS / integration boundary | [FSM_COS](https://github.com/TrentBest/FSM_COS) | Must remain outside storage Core; review placement of REST integration types and prove consumer flow |
| Host activation and visual manifestation | WebPage / AnyApp / other hosts | [WebPage](https://github.com/TrentBest/WebPage) · [AnyApp](https://github.com/TrentBest/AnyApp) | Consumer responsibility; not artifact authority |

## Lifecycle: implemented versus unproven

1. **Author capability** — MicroBundleDomain owns capability identity, providers, dependencies, loading and arbitration contracts. See the [first MicroBundle guide](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/adventures/01-your-first-microbundle.md).
2. **Describe configuration** — capability-owned schema/defaults and the rule that Experiences carry overrides rather than duplicate the schema remain tracked in [Domain #11](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/11).
3. **Identify artifact** — MicroBundle bytes are addressed by bundle ID, explicit version and SHA-256. The hash proves byte identity, not publisher authenticity or authorization.
4. **Store and retrieve bytes** — Core defines exact-address access; Azure maps the address to a deterministic blob path. HTTP 412 handling now reads the existing artifact through the hash-verifying retrieval path and rejects differing bytes or semantic metadata; focused conflict/corruption tests are still required.
5. **Discover artifacts** — `ListAsync` returns paginated identities, optionally filtered by bundle ID/version, without downloading payloads. Because Azure's version prefix is not an exact match, the adapter now applies an exact version comparison to parsed identities; focused unit tests cover version-prefix collisions and bundle-ID filtering. Adapter-level pagination/continuation-token/cancellation tests remain outstanding, and the new tests have not yet been run on this head.
6. **Discover an Experience** — the candidate adds REST routes for published Experience identities, current publication lookup and exact immutable Experience retrieval. Those routes lack focused host-level tests in the candidate and have not been proven end-to-end.
7. **Materialize and compose** — repository delivery is not dependency closure, arbitration or activation. Those responsibilities belong at the FSM_COS/composition boundary; live WebPage activation remains unproven.
8. **Operate safely** — authentication/authorization, payload limits, observability, retry policy, retention and live Azure validation need explicit proof before production claims.

## Immutable artifact versus mutable publication

```text
Immutable MicroBundle / Experience artifact     Mutable Experience publication
(id, version, SHA-256)                           (experience ID -> artifact address)
             |                                                  |
             v                                                  v
      artifacts/{id}/{version}/{hash}                  publications/{experienceId}
             |                                                  |
             +-- exact bytes, hash-verified                      +-- pointer may move
             +-- identity is immutable                           +-- unpublish removes pointer
             +-- storage is not publication                      +-- must not delete artifact bytes
```

Keep these queries separate:

- **Artifact identity:** which exact bytes?
- **Inventory:** which immutable identities are stored?
- **Publication:** which artifact is currently selected for an Experience?
- **Ontology coverage:** which semantic ranges can a node serve?
- **Placement:** where is a node or storage endpoint?
- **Composition:** which capabilities are loaded and how do they interact?

Inventory, ontology coverage and placement are different contracts; see [Repository #16](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/16) and [#15](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/15).

## Coverage and release gates

| Area | Candidate evidence | Remaining decision or proof |
|---|---|---|
| Content integrity | Artifact constructor verifies SHA-256; unit tests exist | On HTTP 412, existing bytes are read through hash verification and semantic metadata is compared; add focused conflict/corruption tests |
| Pagination | Request validation and Azure listing source exist | Test next-page token, pagination, cancellation and empty pages; pure filter tests now cover exact version and bundle-ID matching |
| REST compatibility | Candidate adds pagination and Experience routes | `GET /api/microbundles` changes its response contract versus development's observer inventory; preserve it or deliberately version/document the break |
| Storage modes | Candidate host wires Azure implementations | Decide whether Local mode remains supported; development supports local filesystem mode, candidate requires Azure configuration |
| Experience routes | List, current publication and exact artifact routes are mapped | Add host-level tests, including missing pointer/artifact, invalid addresses, and IDs beyond signed 64-bit range; route constraints were removed so route binding can accept full-width `ulong` identifiers; host-level tests for values above `long.MaxValue` are still required |
| Access control | Host routes exist | Define authentication/authorization and permitted write/publish actors; CORS is not access control |
| Payload bounds | REST transport exists | Bound encoded/decoded payload sizes, memory use and request duration |
| Semantic metadata | Azure metadata round-trip tests exist | Decide malformed/unknown metadata policy; metadata must not change artifact identity |
| Azure proof | Adapter source and unit tests exist | Run against an explicitly provisioned development account before claiming live-cloud validation |
| Moniker end-to-end | [Repository #20](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/20) tracks delivery | Reconcile WebPage bundle 2110 vs Experience 3101 / wave-motion 3102; prove download → verify → materialize → compose → activate |
| CI and branch integration | Candidate source had earlier reported validation | Obtain fresh PR-triggered CI after branch reconciliation; current head has no confirmed PR-triggered run in the recorded check |

## Recommended next vertical slice

1. Test a known artifact's create, exact-address read and hash-mismatch behavior.
2. Test Azure list paging and filters without downloading payloads.
3. Preserve or intentionally version the existing REST inventory contract; test both MicroBundle and Experience routes at host level.
4. Reconcile the candidate with `development`, keeping this map and the lifecycle SVG aligned to the resulting code.
5. Prove Experience pointer lookup → immutable artifact retrieval → materialization → FSM_COS composition → host activation.
6. Repeat against a configured Azure development account and record the run. Do not infer live-cloud proof from unit tests.

The lifecycle visual is an index to the evidence, not a substitute for tests. Solid paths should mean the corresponding checked-in behavior exists; dashed paths should remain visible wherever validation or a design decision is outstanding.
