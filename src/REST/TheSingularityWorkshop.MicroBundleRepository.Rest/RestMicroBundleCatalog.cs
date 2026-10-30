using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using MicroBundle = TheSingularityWorkshop.MicroBundleDomain.IMicroBundle;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>
/// Preloads verified MicroBundle artifacts over REST and exposes the resident
/// result through the synchronous FSM_COS catalog contract.
/// </summary>
public sealed class RestMicroBundleCatalog : IMicroBundleCatalog
{
    private readonly RestMicroBundleRepository _repository;
    private readonly IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> _addresses;
    private readonly IMicroBundleArtifactMaterializer _materializer;
    private readonly Dictionary<ulong, MicroBundle> _loaded = new();
    private readonly Dictionary<ulong, MicroBundleArtifactAddress> _loadedAddresses = new();
    private readonly object _loadedGate = new();

    public RestMicroBundleCatalog(
        RestMicroBundleRepository repository,
        IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> addresses,
        IMicroBundleArtifactMaterializer materializer)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
    }

    /// <summary>
    /// Fetches the requested exact-version dependency closure. Every dependency
    /// declared by a repository-backed bundle must carry an exact version pin.
    /// Composition remains synchronous after preload.
    /// </summary>
    public async Task PreloadClosureAsync(
        IEnumerable<ulong> rootBundleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootBundleIds);

        var pending = new Queue<MicroBundleVersionRequest>();
        foreach (var rootBundleId in rootBundleIds)
        {
            if (!_addresses.TryGetValue(rootBundleId, out var rootAddress))
                throw new InvalidOperationException(
                    $"No repository address is registered for MicroBundle {rootBundleId}.");

            if (rootAddress.BundleId != rootBundleId)
                throw new InvalidOperationException(
                    $"The repository address registered for MicroBundle {rootBundleId} identifies MicroBundle {rootAddress.BundleId}.");

            pending.Enqueue(new MicroBundleVersionRequest(rootBundleId, rootAddress.Version));
        }

        var visited = new Dictionary<ulong, string>();
        var staged = new Dictionary<ulong, (MicroBundle Bundle, MicroBundleArtifactAddress Address)>();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = pending.Dequeue();
            if (visited.TryGetValue(request.BundleId, out var visitedVersion))
            {
                if (!string.Equals(visitedVersion, request.Version, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"The dependency closure requests conflicting versions '{visitedVersion}' and '{request.Version}' for MicroBundle {request.BundleId}.");

                continue;
            }

            visited.Add(request.BundleId, request.Version);

            if (!_addresses.TryGetValue(request.BundleId, out var address))
                throw new InvalidOperationException(
                    $"No repository address is registered for MicroBundle {request.BundleId} version '{request.Version}'.");

            if (address.BundleId != request.BundleId)
                throw new InvalidOperationException(
                    $"The repository address registered for MicroBundle {request.BundleId} identifies MicroBundle {address.BundleId}.");

            if (!string.Equals(address.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The repository address for MicroBundle {request.BundleId} pins version '{address.Version}', " +
                    $"but the dependency closure requires exact version '{request.Version}'.");

            lock (_loadedGate)
            {
                if (_loaded.TryGetValue(request.BundleId, out var loaded))
                {
                    if (_loadedAddresses.TryGetValue(request.BundleId, out var loadedAddress) &&
                        loadedAddress == address)
                        continue;

                    throw new InvalidOperationException(
                        $"MicroBundle {request.BundleId} is already loaded from a different immutable artifact. " +
                        "Use an isolated candidate catalog when staging another Experience.");
                }
            }

            var artifact = await _repository.GetAsync(address, cancellationToken);
            if (artifact is null)
                throw new InvalidOperationException(
                    $"MicroBundle artifact {address} was not found in the repository.");

            var bundle = _materializer.Materialize(artifact);
            ArgumentNullException.ThrowIfNull(bundle);

            if (bundle.Id != request.BundleId)
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle {bundle.Id}.");

            if (!string.Equals(bundle.Descriptor.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle version {bundle.Descriptor.Version}.");

            staged.Add(bundle.Id, (bundle, address));

            foreach (var dependency in bundle.Dependencies)
            {
                if (!dependency.IsPinned)
                    throw new InvalidOperationException(
                        $"MicroBundle {bundle.Id} declares unpinned dependency {dependency.BundleId}. " +
                        "Repository-backed published closures require exact dependency versions.");

                pending.Enqueue(new MicroBundleVersionRequest(
                    dependency.BundleId,
                    dependency.Version!));
            }
        }

        lock (_loadedGate)
        {
            // Cancellation may occur while this preload waits for the commit lock.
            // Observe it after acquiring the lock, before touching shared state.
            cancellationToken.ThrowIfCancellationRequested();

            // Validate the whole commit before mutating shared state. This keeps
            // concurrent preload operations from exposing a partially committed closure.
            foreach (var entry in staged)
            {
                if (_loaded.ContainsKey(entry.Key))
                    throw new InvalidOperationException(
                        $"MicroBundle {entry.Key} was loaded concurrently while its dependency closure was being preloaded.");
            }

            foreach (var entry in staged)
            {
                _loaded.Add(entry.Key, entry.Value.Bundle);
                _loadedAddresses.Add(entry.Key, entry.Value.Address);
            }
        }
    }

    /// <summary>Resolves a resident bundle only when its complete immutable address matches.</summary>
    public bool TryResolve(
        MicroBundleArtifactAddress address,
        out MicroBundle? bundle)
    {
        lock (_loadedGate)
        {
            if (_loaded.TryGetValue(address.BundleId, out bundle) &&
                _loadedAddresses.TryGetValue(address.BundleId, out var loadedAddress) &&
                loadedAddress == address)
                return true;

            bundle = null;
            return false;
        }
    }

    public bool TryResolve(
        ulong bundleId,
        string version,
        out MicroBundle? bundle)
    {
        lock (_loadedGate)
        {
            if (_loaded.TryGetValue(bundleId, out bundle) &&
                string.Equals(bundle.Descriptor.Version, version, StringComparison.Ordinal))
                return true;

            bundle = null;
            return false;
        }
    }

    public bool TryResolve(ulong bundleId, out IMicroBundle? bundle)
    {
        lock (_loadedGate)
            return _loaded.TryGetValue(bundleId, out bundle);
    }
}
