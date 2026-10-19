# MicroBundle domain map

> Evidence-backed map of the current MicroBundle lifecycle across MicroBundleDomain, MicroBundleRepository, and the composition boundary. This is an implementation inventory, not a claim that every proposed lifecycle stage exists.

**Status vocabulary:** **Implemented + unit-tested** means source and a corresponding test exist in this branch; **implemented, proof incomplete** means source exists but the checked-in tests do not prove the whole production path; **proposed / gap** means it must not be presented as working behavior.

## Ownership map

| Concern | Owner | Evidence | Current status |
|---|---|---|---|
| Capability identity, version, dependencies, providers, load/arbitration contract | [MicroBundleDomain](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/IMicroBundle.cs) | [Domain architecture](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/ARCHITECTURE.md) | Implemented; independent of repository/host |
| Authoring description and configurable fields | MicroBundleDomain | [MicroBundleDefinition.cs](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/MicroBundleDefinition.cs) | Implemented; schema/default ownership and Experience override-only rule still tracked in [Domain #11](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/11) |
| Immutable MicroBundle artifact address and SHA-256 verification | Repository Core | [Address](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/MicroBundleArtifactAddress.cs) · [Artifact](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/MicroBundleArtifact.cs) · [tests](../tests/Core/TheSingularityWorkshop.MicroBundleRepository.Core.Tests/MicroBundleArtifactTests.cs) | Implemented + unit-tested |
| Durable MicroBundle bytes | Repository Core + Local/Azure adapters | [contract](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IMicroBundleRepository.cs) · [local adapter](../src/Local/TheSingularityWorkshop.MicroBundleRepository.Local/FileSystemMicroBundleRepository.cs) · [Azure adapter](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureMicroBundleRepository.cs) | Implemented; local write semantics need immutability parity review |
| Artifact inventory | Observer contract + REST host | [REST host](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) · [observer contract](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IMicroBundleRepositoryObserver.cs) | MicroBundle inventory route exists; not a semantic dependency catalog |
| Immutable Experience bytes | Repository Core + Azure adapter | [contract](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceRepository.cs) · [Azure adapter](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureExperienceRepository.cs) | Implemented in source; production wiring and end-to-end proof incomplete |
| Mutable current-publication pointer | Repository Core + Azure catalog + REST host | [contract](../src/Core/TheSingularityWorkshop.MicroBundleRepository.Core/IExperienceCatalog.cs) · [Azure catalog](../src/Azure/TheSingularityWorkshop.MicroBundleRepository.Azure/AzureExperienceCatalog.cs) · [host routes](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) | GET publication route and host tests exist; current-head CI passes published/unpublished, invalid-ID and missing-storage outcomes; end-to-end consumer proof remains open |
| REST transport for MicroBundle bytes | REST host | [Program.cs](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) | GET/list/PUT routes exist; auth, payload bounds and hostile-client behavior require explicit review |
| Experience artifact retrieval/publication through REST | REST client + repository REST host | [REST client](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/RestExperienceClient.cs) · [host routes](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest.Host/Program.cs) · [tracked gap #22](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/22) | Publication lookup and immutable GET routes/client exist; current-head CI passes client and host contract tests for exact bytes, missing artifacts, malformed hashes, integrity failures, oversized requests, and publication outcomes. Live Azure execution and end-to-end composition/activation remain open |
| Artifact materialization into IMicroBundle | Composition integration | [materializer contract](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/IMicroBundleArtifactMaterializer.cs) · [catalog](../src/REST/TheSingularityWorkshop.MicroBundleRepository.Rest/RestMicroBundleCatalog.cs) | Integration exists; the REST project references composition concerns and needs a boundary-placement review |
| Dependency closure, arbitration, convergence, RuntimeAssembly | FSM_COS | [FSM_COS repository](https://github.com/TrentBest/FSM_COS) | Composition owner; not repository responsibility |
| Host activation and visual manifestation | WebPage / AnyApp / other hosts | [WebPage](https://github.com/TrentBest/WebPage) · [AnyApp](https://github.com/TrentBest/AnyApp) | Consumer layer; must not be artifact authority |

## Lifecycle: what exists versus what remains

1. **Author capability** — implement `IMicroBundle` and its descriptor/dependencies/load/arbitration contract. See [first MicroBundle guide](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/adventures/01-your-first-microbundle.md).
2. **Describe configuration** — `MicroBundleDefinition` exposes inspectable fields, defaults, bounds and optional stable parameter IDs. The rule that an Experience carries overrides rather than duplicating capability-owned schema is an explicit architectural requirement ([Domain #11](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/issues/11)).
3. **Package artifact** — bytes are associated with `(bundle ID, version, SHA-256)`. The repository verifies the hash when constructing `MicroBundleArtifact`; a hash proves byte identity, not publisher identity or authorization.
4. **Store/retrieve MicroBundle** — `IMicroBundleRepository` has deterministic Get/Put. Local filesystem and Azure implementations exist. The REST host exposes `GET /api/microbundles`, `GET /api/microbundles/{bundleId}/{version}/{contentHash}`, and `PUT` at the latter path.
5. **Resolve an Experience selection** — development maps `GET /api/experiences/{experienceId}/publication` and `GET /api/experiences/{experienceId}/{version}/{contentHash}` and wires Azure-backed Experience storage/catalog in Azure mode. Local mode returns explicit 503 because no local Experience adapter exists. Host contract tests pass in current-head CI; end-to-end proof remains open.
6. **Materialize and compose** — REST catalog/materializer integration can fetch declared artifact closures in tests, but this does not prove that a published Experience can be retrieved through REST and activated by WebPage. Track [#22](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/22) and [#20](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/20).
7. **Activate and operate** — host activation, update/unpublish behavior, retention, provenance/audit, retry policy and live Azure deployment require separate proof. Unpublishing an Experience pointer is designed not to delete immutable bytes.

## Immutable artifact versus mutable publication

```text
Immutable MicroBundle artifact                 Mutable Experience publication
(bundleId, version, SHA-256)                    (experienceId -> artifact address)
            |                                                |
            v                                                v
    artifacts/{id}/{version}/{hash}                publications/{experienceId}
            |                                                |
            +-- bytes do not change                           +-- pointer may move
            +-- hash identifies bytes                        +-- unpublish removes pointer
            +-- can be retained                               +-- does not imply artifact deletion
```

Do not conflate:
- **identity** (which exact bytes),
- **publication** (which artifact is currently selected),
- **inventory** (what bytes a repository reports storing),
- **ontology coverage** (which semantic ranges a node can serve),
- **placement** (where a node/storage endpoint lives),
- **composition** (what gets loaded and how it interacts).

Inventory, ontology coverage, and placement are different queries; see [#16](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/16) and [#15](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/15).

## Coverage matrix and open contracts

| Area | Evidence-backed status | Decision or proof still needed |
|---|---|---|
| Hash/integrity | Artifact constructors compute SHA-256 and reject mismatch | Add corruption-path tests for each adapter and REST mapping |
| Publisher authenticity | Not provided by SHA-256 alone | Define trusted publisher/signature or provenance policy; decide who may PUT/publish |
| REST access control | PUT fails closed when `Repository:WriteApiKey` is unset and requires `X-Repository-Write-Key` when configured; CORS is opt-in through `Repository:AllowedCorsOrigins` | Deployment must configure trusted origins and write credentials; verify the intended network exposure, secret handling and authorization policy. CORS is not authorization |
| Payload limits | MicroBundle PUT has a configurable `Repository:MaxMicroBundleRequestBytes` cap (default 16 MiB) and server request-body enforcement; Azure Experience reads/writes have configurable artifact caps (default 10 MiB) | Verify deployment-level streaming/chunked enforcement and live Azure read behavior; review buffering, concurrency and request-duration limits |
| Local immutability | Local Put uses file move with overwrite enabled | Align semantics with Azure's create-only content-addressed write; detect conflicting bytes at same address |
| Experience REST | Publication lookup and immutable GET routes, REST client, and host contract tests exist; current-head CI passes | Prove live Azure read-limit behavior and the complete publication → immutable bytes → verification → composition/activation path, including failed-candidate retention |
| Cache | Architecture describes a local/resident cache, but no cache contract is defined here | Define immutable cache key, publication-pointer freshness, concurrency, offline behavior, eviction/size and cancellation |
| Publication concurrency | Azure catalog writes the publication blob | Define atomic/concurrent update expectations and conditional writes/versioning |
| Artifact lifecycle | Unpublish leaves artifact bytes | Define retention, tombstones, garbage collection, legal/audit needs before deletion behavior |
| Observability | MicroBundle observer reports artifact address/size/modified time | Define stable operational event, retry/failure, audit and health semantics |
| Azure proof | Azure adapter and live-test source exist; no live run asserted here | Record a successful run against an explicitly provisioned development account before claiming cloud validation |
| End-to-end Moniker | [#20](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleRepository/issues/20) tracks ID reconciliation and delivery | Reconcile WebPage 2110 vs Experience 3101 / wave-motion 3102 before publishing; then prove download → verify → materialize → compose → activate |

## Recommended next vertical slice

Keep the first proven slice deliberately narrow:

1. Create a test artifact with a known ID/version/hash.
2. PUT it to local repository storage, then GET by the complete address.
3. Verify exact bytes and rejection of corrupted bytes.
4. Fetch it through the REST endpoint and materialize it using the composition-side materializer.
5. Extend and integrate the existing Experience publication-pointer lookup and immutable GET; do not treat route-level tests as end-to-end consumer proof.
6. Compose the retrieved MicroBundle through FSM_COS and prove the host consumes that result.
7. Repeat against Azure only after a configured integration environment is available.

The source of truth is the code and tests linked above. The diagram in [microbundle-lifecycle.svg](images/microbundle-lifecycle.svg) is a visual index, not a substitute for tests.
