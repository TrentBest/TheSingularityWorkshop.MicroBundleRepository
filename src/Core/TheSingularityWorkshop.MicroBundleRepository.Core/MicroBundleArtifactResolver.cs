namespace TheSingularityWorkshop.MicroBundleRepository.Core;

/// <summary>
/// Resolves exact version requests against observed immutable artifact addresses.
/// This is a publication-time operation; runtime composition consumes the resulting pins.
/// </summary>
public static class MicroBundleArtifactResolver
{
    /// <summary>
    /// Resolves every requested (bundle ID, exact version) to exactly one immutable address.
    /// Missing and ambiguous requests fail closed. Duplicate observations of the same address
    /// are harmless; distinct content hashes for the same ID and version are ambiguous.
    /// </summary>
    public static IReadOnlyDictionary<MicroBundleVersionRequest, MicroBundleArtifactAddress> ResolveExact(
        IEnumerable<MicroBundleVersionRequest> requests,
        IEnumerable<MicroBundleArtifactObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(observations);

        var requested = requests.Distinct().ToArray();
        var inventory = observations.ToArray();
        var resolved = new Dictionary<MicroBundleVersionRequest, MicroBundleArtifactAddress>();

        foreach (var request in requested)
        {
            var matches = inventory
                .Where(observation =>
                    observation.Address.BundleId == request.BundleId &&
                    string.Equals(observation.Address.Version, request.Version, StringComparison.Ordinal))
                .Select(observation => observation.Address)
                .Distinct()
                .ToArray();

            if (matches.Length == 0)
                throw new InvalidOperationException(
                    $"No immutable artifact exists for MicroBundle {request.BundleId} version '{request.Version}'.");

            if (matches.Length > 1)
                throw new InvalidOperationException(
                    $"MicroBundle {request.BundleId} version '{request.Version}' is ambiguous: " +
                    $"{matches.Length} distinct content hashes are present.");

            resolved.Add(request, matches[0]);
        }

        return resolved;
    }
}
