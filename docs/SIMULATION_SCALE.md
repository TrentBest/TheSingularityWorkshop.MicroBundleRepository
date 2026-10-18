# Artifact Delivery at Simulation Scale

## The important distinction

MicroBundleRepository is the **immutable capability-artifact delivery boundary**. It is not the live state database for a large simulation.

A domino-and-marble experience might use repository artifacts to distribute collision rules, material models, layout interpreters, motion solvers, observers, and replay tools. Once those capabilities are materialized and composed, the running simulation may contain millions of entities whose positions and velocities change many times per second. Those state updates do not belong in the artifact repository.

```text
MicroBundle artifacts                  Simulation runtime state
---------------------                  ------------------------
versioned capability code/rules        entity positions and velocities
content-addressed immutable bytes      contacts and active constraints
retrieved on demand                    per-step changes and events
cacheable by full artifact address     checkpointed / streamed separately
```

## What the current artifact contract actually guarantees

`MicroBundleArtifactAddress` identifies an artifact by:
- nonzero MicroBundle ID;
- explicit version;
- 64-character hexadecimal SHA-256 content hash.

`MicroBundleArtifact` copies the supplied bytes and validates that their SHA-256 matches the address before it can be constructed. The Core repository contract retrieves by a complete address and stores an artifact. This is integrity checking and deterministic addressing; it is not a digital signature, author identity, trust decision, executable safety sandbox, or compatibility proof.

A valid hash proves that bytes match the stated hash. It does not prove that those bytes are trustworthy or safe to execute.

## Retrieval path

```text
composition request / artifact address
                |
                v
      local or resident cache
                |
         cache miss
                v
      IMicroBundleRepository
                |
                v
       storage / transport
                |
                v
        immutable byte payload
                |
                v
     address/hash verification
                |
                v
     host-specific materializer
                |
                v
        composition host
```

The repository owns lookup, storage, transport, and artifact integrity. Materialization converts the verified byte representation into a runtime capability and belongs to an explicit host/repository integration. Composition resolves dependencies, loads capabilities, arbitrates, and creates the runtime assembly. Keep those responsibilities distinct.

## Assembly payloads are a separate format decision

The Core artifact object treats content as opaque bytes. It does not promise that every artifact is a .NET assembly, nor does it define an envelope containing a bundle ID and assembly bytes. A host that stores raw assembly bytes must load that documented format directly; a host that wants a structured envelope must define, version, validate, and test that envelope as a separate contract.

Do not assume a type such as `MicroBundleAssemblyPayload` exists unless it is actually defined by a dependency or by the host. Do not silently reinterpret raw bytes as a different payload format.

Before executing retrieved code, a production host should also define its trust policy, allowed origins/signatures, compatibility checks, failure handling, and isolation boundary. Hash verification alone is not a security boundary.

## Large simulations: what to store where

Use the artifact repository for relatively immutable, reusable capabilities:
- simulation algorithms and material rules;
- authored layout definitions when they are packaged as versioned assets;
- schema and tool versions;
- reusable initial-condition packages or test fixtures when appropriate.

Use a separate runtime-state mechanism for high-churn data:
- entity transforms, velocities, and sleep states;
- contact manifolds and temporary constraints;
- per-step events and telemetry;
- periodic snapshots/checkpoints;
- replay input logs and timeline branches.

A million marbles should not require a million MicroBundle artifacts. Nor should every simulation tick create a new artifact address. A capability can operate over large state arrays; the repository provides the capability bytes, not one storage object per simulated entity.

## Cache and content-addressing implications

The complete address (ID, version, hash) is a natural cache key. Since artifacts are immutable, a cache entry can remain valid for that exact address. A new build or changed artifact gets a different content hash, even if its ID and version were mistakenly reused.

For larger installations, measure:
- cache hit rate and cold-start latency;
- artifact sizes and transfer throughput;
- hash verification cost;
- duplicate downloads across processes;
- eviction and disk-space policy;
- concurrent fetch deduplication;
- cancellation and retry behavior;
- storage request costs.

The current contract does not itself provide a resident cache, distributed cache, background prefetch, streaming partial reads, or simulation-state replication. Those require separate implementations and tests.

## Suggested validation path

1. Verify that malformed hashes and mismatched bytes are rejected.
2. Store and retrieve an artifact using its exact address.
3. Verify a cache miss and a cache hit return the same immutable bytes.
4. Verify cancellation and transient storage failures are observable.
5. Verify host materialization rejects invalid, incompatible, or unexpected content.
6. Load a small set of simulation capabilities and run a tiny domino/marble scenario.
7. Benchmark artifact delivery separately from simulation step throughput.
8. Increase simulation entity count without increasing the number of capability artifacts proportionally.
9. Add checkpoint/replay storage only under a separately specified state format.

## Ownership map

| Concern | Owner |
|---|---|
| Capability identity, dependency declarations, load/arbitration contracts | MicroBundleDomain |
| Immutable artifact address, bytes, storage, retrieval, hash verification | MicroBundleRepository |
| Dependency closure, materialization integration, load order, arbitration convergence | Composition host / explicit adapter |
| Millions of entity states, collision processing, stepping, partitioning, checkpoints | Simulation / physics runtime |
| Observer-relative output and rendering | Observer / renderer / presentation layers |

This division lets the same immutable capability artifacts be reused by desktop, browser, batch, or distributed hosts without pretending that the repository is the physics engine.

## Related reading

- [Repository architecture](ARCHITECTURE.md)
- [Azure setup](AZURE_SETUP.md)
- [MicroBundleDomain architecture](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/ARCHITECTURE.md)
- [MicroBundleDomain massive simulations guide](https://github.com/TrentBest/TheSingularityWorkshop.MicroBundleDomain/blob/development/docs/MASSIVE_SIMULATIONS.md)
