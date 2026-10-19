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
    /// Fetches requested bundles and recursively follows their declared dependencies.
    /// Composition remains synchronous after preload.
    /// </summary>
    public async Task PreloadClosureAsync(
        IEnumerable<ulong> rootBundleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootBundleIds);

        var pending = new Queue<ulong>(rootBundleIds);
        var visited = new HashSet<ulong>();
        // Keep a candidate closure isolated until every artifact has been fetched,
        // hash-verified, and materialized. A failed preload must not leave roots
        // resolvable while one or more dependencies are missing.
        var staged = new Dictionary<ulong, MicroBundle>();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bundleId = pending.Dequeue();
            if (!visited.Add(bundleId) || staged.ContainsKey(bundleId))
                continue;

            lock (_loadedGate)
            {
                if (_loaded.ContainsKey(bundleId))
                    continue;
            }

            if (!_addresses.TryGetValue(bundleId, out var address))
                throw new InvalidOperationException(
                    $"No repository address is registered for MicroBundle {bundleId}.");

            // The dictionary key is the dependency lookup key; the address is the
            // immutable remote identity. Reject disagreement before making a request.
            if (address.BundleId != bundleId)
                throw new InvalidOperationException(
                    $"The repository address registered for MicroBundle {bundleId} identifies MicroBundle {address.BundleId}.");

            var artifact = await _repository.GetAsync(address, cancellationToken);
            if (artifact is null)
                throw new InvalidOperationException(
                    $"MicroBundle artifact {address} was not found in the repository.");

            var bundle = _materializer.Materialize(artifact);
            ArgumentNullException.ThrowIfNull(bundle);

            if (bundle.Id != bundleId)
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle {bundle.Id}.");

            if (!string.Equals(bundle.Descriptor.Version, address.Version, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle version {bundle.Descriptor.Version}.");

            staged.Add(bundle.Id, bundle);

            foreach (var dependency in bundle.Dependencies)
                pending.Enqueue(dependency.BundleId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (_loadedGate)
        {
            // Validate the whole commit before mutating shared state. This keeps
            // concurrent preload operations from exposing a partially committed closure.
            foreach (var entry in staged)
            {
                if (_loaded.ContainsKey(entry.Key))
                    throw new InvalidOperationException(
                        $"MicroBundle {entry.Key} was loaded concurrently while its dependency closure was being preloaded.");
            }

            foreach (var entry in staged)
                _loaded.Add(entry.Key, entry.Value);
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
