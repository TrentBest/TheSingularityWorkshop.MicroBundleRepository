using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

var local = args.Length > 0 && string.Equals(args[0], "--local", StringComparison.OrdinalIgnoreCase);

if (local)
{
    if (args.Length != 4)
    {
        Console.Error.WriteLine(
            "Usage: MicroBundlePublisher --local <bundle-id> <version> <bundle-path>");
        return 2;
    }

    return await RunRoundTripAsync(
        new InMemoryMicroBundleRepository(),
        args[1],
        args[2],
        args[3]);
}

if (args.Length != 5)
{
    Console.Error.WriteLine(
        "Usage: MicroBundlePublisher <storage-account-uri> <container-name> <bundle-id> <version> <bundle-path>");
    return 2;
}

if (!Uri.TryCreate(args[0], UriKind.Absolute, out var storageAccountUri))
{
    Console.Error.WriteLine("Storage account URI must be absolute.");
    return 2;
}

return await RunRoundTripAsync(
    new AzureMicroBundleRepository(new AzureMicroBundleRepositoryOptions
    {
        StorageAccountUri = storageAccountUri,
        ContainerName = args[1]
    }),
    args[2],
    args[3],
    args[4]);

static async Task<int> RunRoundTripAsync(
    IMicroBundleRepository repository,
    string bundleIdText,
    string version,
    string bundlePath)
{
    if (!ulong.TryParse(bundleIdText, out var bundleId) || bundleId == 0)
    {
        Console.Error.WriteLine("Bundle ID must be a non-zero unsigned integer.");
        return 2;
    }

    if (!File.Exists(bundlePath))
    {
        Console.Error.WriteLine($"Bundle artifact was not found: {bundlePath}");
        return 2;
    }

    var content = await File.ReadAllBytesAsync(bundlePath);
    var artifact = MicroBundleArtifact.Create(bundleId, version, content);

    if (repository is AzureMicroBundleRepository azureRepository)
        await azureRepository.InitializeAsync();

    await repository.PutAsync(artifact);

    var retrieved = await repository.GetAsync(artifact.Address)
        ?? throw new InvalidOperationException(
            $"Repository did not return {artifact.Address} after publication.");

    if (!retrieved.Content.Span.SequenceEqual(artifact.Content.Span))
        throw new InvalidOperationException("Repository returned different artifact bytes.");

    Console.WriteLine($"Repository round trip verified: {artifact.Address}");

    var bundle = new AssemblyMicroBundleArtifactMaterializer().Materialize(retrieved);

    if (bundle.Id != bundleId)
    {
        throw new InvalidOperationException(
            $"Materialized MicroBundle ID {bundle.Id} does not match requested ID {bundleId}.");
    }

    Console.WriteLine(
        $"FSM_COS materialization verified: MicroBundle {bundle.Id} ({bundle.GetType().FullName})");

    return 0;
}

sealed class InMemoryMicroBundleRepository : IMicroBundleRepository
{
    private readonly Dictionary<MicroBundleArtifactAddress, MicroBundleArtifact> _artifacts = new();

    public ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _artifacts.TryGetValue(address, out var artifact);
        return ValueTask.FromResult(artifact);
    }

    public ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();
        _artifacts.Add(artifact.Address, artifact);
        return ValueTask.CompletedTask;
    }
}
