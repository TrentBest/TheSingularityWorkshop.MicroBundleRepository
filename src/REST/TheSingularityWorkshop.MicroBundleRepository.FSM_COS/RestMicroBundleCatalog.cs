using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;

namespace TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

/// <summary>
/// Preloads verified MicroBundle artifacts over REST and exposes the resident
/// result through the FSM_COS catalog boundary.
/// </summary>
public sealed class RestMicroBundleCatalog : IMicroBundleCatalog
{
    private readonly RestMicroBundleRepository _repository;
    private readonly IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> _addresses;
    private readonly IMicroBundleArtifactMaterializer _materializer;
    private readonly Dictionary<ulong, TheSingularityWorkshop.MicroBundleDomain.IMicroBundle> _loaded = new();

    public RestMicroBundleCatalog(
        RestMicroBundleRepository repository,
        IReadOnlyDictionary<ulong, MicroBundleArtifactAddress> addresses,
        IMicroBundleArtifactMaterializer materializer)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
    }

    public async Task PreloadClosureAsync(
        IEnumerable<ulong> rootBundleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootBundleIds);

        var pending = new Queue<ulong>(rootBundleIds);
        var visited = new HashSet<ulong>();

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bundleId = pending.Dequeue();
            if (!visited.Add(bundleId) || _loaded.ContainsKey(bundleId))
                continue;

            if (!_addresses.TryGetValue(bundleId, out var address))
                throw new InvalidOperationException(
                    $"No repository address is registered for MicroBundle {bundleId}.");

            var artifact = await _repository.GetAsync(address, cancellationToken);
            if (artifact is null)
                throw new InvalidOperationException(
                    $"MicroBundle artifact {address} was not found in the repository.");

            var bundle = _materializer.Materialize(artifact);
            ArgumentNullException.ThrowIfNull(bundle);

            if (bundle.Id != bundleId)
                throw new InvalidOperationException(
                    $"Materialized artifact {address} produced MicroBundle {bundle.Id}.");

            _loaded.Add(bundle.Id, bundle);

            foreach (var dependency in bundle.Dependencies)
                pending.Enqueue(dependency.BundleId);
        }
    }

    public bool TryResolve(
        ulong bundleId,
        out TheSingularityWorkshop.MicroBundleDomain.IMicroBundle? bundle) =>
        _loaded.TryGetValue(bundleId, out bundle);
}
