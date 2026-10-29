# Local MicroBundle Repository

A filesystem-backed development adapter for the same repository contracts used by durable storage.

It lets a developer exercise artifact storage and retrieval immediately, without Azure credentials or cloud provisioning. The artifact is still addressed by bundle ID, explicit version, and SHA-256; the local adapter does not invent a second MicroBundle format or perform dependency resolution/composition.

## Storage layout

```text
{root}/
└── {bundleId}/
    └── {version}/
        └── {sha256}.bundle
```

Choose the root directory explicitly. For example, this uses the current user's local application-data directory rather than a machine-specific absolute path:

```csharp
using System.Security.Cryptography;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;

var root = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "TheSingularityWorkshop",
    "MicroBundles");

var bundleBytes = await File.ReadAllBytesAsync("my-bundle.bin");
var hash = Convert.ToHexString(SHA256.HashData(bundleBytes)).ToLowerInvariant();

var address = new MicroBundleArtifactAddress(
    bundleId: 42,
    version: "1.0.0",
    contentHash: hash);

var artifact = new MicroBundleArtifact(address, bundleBytes);
IMicroBundleRepository repository = new FileSystemMicroBundleRepository(root);

await repository.PutAsync(artifact);

var retrieved = await repository.GetAsync(address)
    ?? throw new InvalidOperationException("The artifact was not found.");

Console.WriteLine($"Verified artifact: {retrieved.Address}");
```

The bytes are opaque to the repository. `MicroBundleArtifact` verifies that the bytes match the supplied SHA-256 address when the artifact is constructed and again when stored bytes are read back.

## Persistent read-through cache

When a remote repository is available, `CachingMicroBundleRepository` can put a local filesystem cache in front of it:

```csharp
IMicroBundleRepository repository =
    new CachingMicroBundleRepository(cacheRoot, remoteRepository);
```

Reads use a verified local cache hit when available and otherwise fetch from the origin, then persist the verified result. Writes go to the origin first and warm the cache only after the origin write succeeds. Both adapters use the same complete immutable artifact address.

## Boundaries

- No Azure account, credentials, or network connection is needed for filesystem-only development.
- The root directory is supplied by the host; this library does not choose a user's storage location.
- The local adapter implements storage and observation, not semantic dependency resolution, Experience publication, REST hosting, or FSM_COS composition.
- Azure remains an optional provider. The same `IMicroBundleRepository` contract can be used by code that selects the Azure adapter in a configured deployment.

For the repository contract and related adapters, see the [Core README](../../Core/TheSingularityWorkshop.MicroBundleRepository.Core/README.md) and the [repository architecture](../../../docs/ARCHITECTURE.md).
