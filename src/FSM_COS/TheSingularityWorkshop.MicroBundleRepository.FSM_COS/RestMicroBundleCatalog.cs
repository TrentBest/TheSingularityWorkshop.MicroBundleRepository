using System.Collections.ObjectModel;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using MicroBundle = TheSingularityWorkshop.MicroBundleDomain.IMicroBundle;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

/// <summary>
/// Preloads verified MicroBundle artifacts over REST and exposes the resident
/// result through the synchronous FSM_COS catalog contract.
/// </summary>
/// <remarks>
/// The input map is keyed by domain/runtime MicroBundle ID. Its values are
/// repository-local artifact addresses; their numeric storage IDs are not
/// required to equal the runtime keys.
/// </remarks>
public sealed class RestMicroBundleCatalog : IMicroBundleCatalog
{
    private readonly RestMicroBundleRepository _repository;
    private readonly IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> _runtimeAddresses;
    private readonly IMicroBundleArtifactMaterializer _materializer;
    private readonly Dictionary<ulong, MicroBundle> _loaded = new();
    private readonly Dictionary<ulong, MicroBundleArtifactAddress> _loadedAddresses = new();
    private readonly object _loadedGate = new();

    public RestMicroBundleCatalog(
        RestMicroBundleRepository repository,
        IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> runtimeAddresses,
        IMicroBundleArtifactMaterializer materializer)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ArgumentNullException.ThrowIfNull(runtimeAddresses);
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));

        var snapshot = runtimeAddresses.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        if (snapshot.Keys.Any(runtimeId => runtimeId == 0))
            throw new ArgumentException("Runtime MicroBundle IDs must be non-zero.", nameof(runtimeAddresses));
        if (snapshot.Values.Any(address => address.BundleId == 0))
            throw new ArgumentException("Repository-local storage IDs must be non-zero.", nameof(runtimeAddresses));
        if (snapshot.Values.Distinct().Count() != snapshot.Count)
            throw new ArgumentException("A repository artifact address cannot be mapped to more than one runtime MicroBundle ID.", nameof(runtimeAddresses));

        _runtimeAddresses = new ReadOnlyDictionary<ulong, MicroBundleArtifactAddress>(snapshot);
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
            if (!_runtimeAddresses.TryGetValue(rootBundleId, out var rootAddress))
                throw new InvalidOperationException(
                    $"No repository address is registered for runtime MicroBundle {rootBundleId}.");

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
                        $"The dependency closure requests conflicting versions '{visitedVersion}' and '{request.Version}' for runtime MicroBundle {request.BundleId}.");

                continue;
            }

            visited.Add(request.BundleId, request.Version);

            if (!_runtimeAddresses.TryGetValue(request.BundleId, out var address))
                throw new InvalidOperationException(
                    $"No repository address is registered for runtime MicroBundle {request.BundleId} version '{request.Version}'.");

            if (!string.Equals(address.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The repository address for runtime MicroBundle {request.BundleId} pins version '{address.Version}', " +
                    $"but the dependency closure requires exact version '{request.Version}'.");

            lock (_loadedGate)
            {
                if (_loaded.TryGetValue(request.BundleId, out var loaded))
                {
                    if (_loadedAddresses.TryGetValue(request.BundleId, out var loadedAddress) &&
                        loadedAddress == address)
                        continue;

                    throw new InvalidOperationException(
                        $"Runtime MicroBundle {request.BundleId} is already loaded from a different immutable artifact. " +
                        "Use an isolated candidate catalog when staging another Experience.");
                }
            }

            var artifact = await _repository.GetAsync(address, cancellationToken);
            if (artifact is null)
                throw new InvalidOperationException(
                    $"MicroBundle artifact {address} was not found in the repository.");

            var bundle = _materializer.Materialize(artifact, request.BundleId);
            ArgumentNullException.ThrowIfNull(bundle);

            if (bundle.Id != request.BundleId || bundle.Descriptor.Id != request.BundleId)
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced runtime MicroBundle ID {bundle.Id} " +
                    $"and descriptor ID {bundle.Descriptor.Id}; expected {request.BundleId}.");

            if (!string.Equals(bundle.Descriptor.Version, request.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle version {bundle.Descriptor.Version}.");

            staged.Add(bundle.Id, (bundle, address));

            foreach (var dependency in bundle.Dependencies)
            {
#if WORKSHOP_USE_REPOSITORY_SOURCES
                if (!dependency.IsPinned)
                    throw new InvalidOperationException(
                        $"MicroBundle {bundle.Id} declares unpinned dependency {dependency.BundleId}. " +
                        "Repository-backed published closures require exact dependency versions.");

                pending.Enqueue(new MicroBundleVersionRequest(
                    dependency.BundleId,
                    dependency.Version!));
#else
                // The currently released Domain package predates exact-version dependency
                // requests. Preserve legacy package compatibility; source-integration builds
                // compile against the development Domain contract and enforce the exact pin.
                if (!_runtimeAddresses.TryGetValue(dependency.BundleId, out var dependencyAddress))
                    throw new InvalidOperationException(
                        $"No repository address is registered for runtime MicroBundle {dependency.BundleId}.");

                pending.Enqueue(new MicroBundleVersionRequest(
                    dependency.BundleId,
                    dependencyAddress.Version));
#endif
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

    /// <summary>Resolves a resident bundle by its complete repository-local address.</summary>
    public bool TryResolve(
        MicroBundleArtifactAddress address,
        out MicroBundle? bundle)
    {
        lock (_loadedGate)
        {
            foreach (var entry in _loadedAddresses)
            {
                if (entry.Value == address && _loaded.TryGetValue(entry.Key, out bundle))
                    return true;
            }

            bundle = null;
            return false;
        }
    }

    /// <summary>Resolves a resident bundle by its domain/runtime identity and exact version.</summary>
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

    /// <summary>Resolves a resident bundle by its domain/runtime identity.</summary>
    public bool TryResolve(ulong bundleId, out MicroBundle? bundle)
    {
        lock (_loadedGate)
            return _loaded.TryGetValue(bundleId, out bundle);
    }
}
