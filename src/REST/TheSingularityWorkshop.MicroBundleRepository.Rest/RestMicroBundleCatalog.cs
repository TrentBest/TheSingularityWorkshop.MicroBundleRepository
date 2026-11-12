using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using MicroBundle = TheSingularityWorkshop.MicroBundleDomain.IMicroBundle;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>
/// Preloads ontology-addressed artifacts and exposes materialized bundles through
/// the numeric runtime catalog contract. Runtime IDs remain domain-local; they are
/// not portable repository artifact identities.
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
    /// Fetches the requested exact-version dependency closure. Runtime bundle IDs
    /// are mapped to portable ontology addresses before repository access.
    /// </summary>
    public async Task PreloadClosureAsync(
        IEnumerable<ulong> rootBundleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootBundleIds);

        var pending = new Queue<(ulong RuntimeBundleId, MicroBundleVersionRequest Request)>();
        foreach (var runtimeBundleId in rootBundleIds)
        {
            if (!_addresses.TryGetValue(runtimeBundleId, out var rootAddress))
                throw new InvalidOperationException(
                    $"No repository address is registered for runtime MicroBundle {runtimeBundleId}.");

            pending.Enqueue((
                runtimeBundleId,
                new MicroBundleVersionRequest(rootAddress.Ontology, rootAddress.Version)));
        }

        var visited = new Dictionary<ulong, string>();
        var staged = new Dictionary<ulong, (MicroBundle Bundle, MicroBundleArtifactAddress Address)>();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (runtimeBundleId, request) = pending.Dequeue();
            if (visited.TryGetValue(runtimeBundleId, out var visitedVersion))
            {
                if (!string.Equals(visitedVersion, request.Version, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"The dependency closure requests conflicting versions '{visitedVersion}' and '{request.Version}' for runtime MicroBundle {runtimeBundleId}.");
                continue;
            }

            visited.Add(runtimeBundleId, request.Version);

            if (!_addresses.TryGetValue(runtimeBundleId, out var address))
                throw new InvalidOperationException(
                    $"No repository address is registered for runtime MicroBundle {runtimeBundleId}.");

            if (!string.Equals(address.Ontology, request.Ontology, StringComparison.Ordinal) ||
                !string.Equals(address.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The runtime MicroBundle {runtimeBundleId} maps to {address}, but the dependency closure requires ontology '{request.Ontology}' version '{request.Version}'.");

            lock (_loadedGate)
            {
                if (_loaded.TryGetValue(runtimeBundleId, out _))
                {
                    if (_loadedAddresses.TryGetValue(runtimeBundleId, out var loadedAddress) &&
                        loadedAddress == address)
                        continue;

                    throw new InvalidOperationException(
                        $"Runtime MicroBundle {runtimeBundleId} is already loaded from a different immutable artifact. " +
                        "Use an isolated candidate catalog when staging another Experience.");
                }
            }

            var artifact = await _repository.GetAsync(address, cancellationToken);
            if (artifact is null)
                throw new InvalidOperationException($"MicroBundle artifact {address} was not found in the repository.");

            var bundle = _materializer.Materialize(artifact);
            ArgumentNullException.ThrowIfNull(bundle);

            if (bundle.Id != runtimeBundleId)
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced runtime MicroBundle {bundle.Id}, expected {runtimeBundleId}.");

            if (!string.Equals(bundle.Descriptor.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle version {bundle.Descriptor.Version}.");

            staged.Add(runtimeBundleId, (bundle, address));

            foreach (var dependency in bundle.Dependencies)
            {
                if (!_addresses.TryGetValue(dependency.BundleId, out var dependencyAddress))
                    throw new InvalidOperationException(
                        $"No repository address is registered for runtime dependency {dependency.BundleId}.");

#if WORKSHOP_USE_REPOSITORY_SOURCES
                if (!dependency.IsPinned)
                    throw new InvalidOperationException(
                        $"MicroBundle {bundle.Id} declares unpinned dependency {dependency.BundleId}. " +
                        "Repository-backed published closures require exact dependency versions.");

                var dependencyVersion = dependency.Version!;
#else
                // The released Domain package predates exact-version dependency requests.
                // Preserve compatibility by using the version pinned in the host's address map.
                var dependencyVersion = dependencyAddress.Version;
#endif

                pending.Enqueue((
                    dependency.BundleId,
                    new MicroBundleVersionRequest(dependencyAddress.Ontology, dependencyVersion)));
            }
        }

        lock (_loadedGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in staged)
            {
                if (_loaded.ContainsKey(entry.Key))
                    throw new InvalidOperationException(
                        $"Runtime MicroBundle {entry.Key} was loaded concurrently while its dependency closure was being preloaded.");
            }

            foreach (var entry in staged)
            {
                _loaded.Add(entry.Key, entry.Value.Bundle);
                _loadedAddresses.Add(entry.Key, entry.Value.Address);
            }
        }
    }

    /// <summary>Resolves a resident bundle only when its complete portable address matches.</summary>
    public bool TryResolve(MicroBundleArtifactAddress address, out MicroBundle? bundle)
    {
        lock (_loadedGate)
        {
            var match = _loadedAddresses.FirstOrDefault(entry => entry.Value == address);
            if (match.Key != 0 && _loaded.TryGetValue(match.Key, out bundle))
                return true;

            bundle = null;
            return false;
        }
    }

    public bool TryResolve(ulong bundleId, string version, out MicroBundle? bundle)
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
