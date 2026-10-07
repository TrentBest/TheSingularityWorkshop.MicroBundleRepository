using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.FSM_COS;

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

var containerName = args[1];
if (!ulong.TryParse(args[2], out var bundleId) || bundleId == 0)
{
    Console.Error.WriteLine("Bundle ID must be a non-zero unsigned integer.");
    return 2;
}

var version = args[3];
var bundlePath = args[4];

if (!File.Exists(bundlePath))
{
    Console.Error.WriteLine($"Bundle artifact was not found: {bundlePath}");
    return 2;
}

var content = await File.ReadAllBytesAsync(bundlePath);
var artifact = MicroBundleArtifact.Create(bundleId, version, content);

var repository = new AzureMicroBundleRepository(new AzureMicroBundleRepositoryOptions
{
    StorageAccountUri = storageAccountUri,
    ContainerName = containerName
});

await repository.InitializeAsync();
await repository.PutAsync(artifact);

var retrieved = await repository.GetAsync(artifact.Address)
    ?? throw new InvalidOperationException($"Repository did not return {artifact.Address} after publication.");

if (!retrieved.Content.Span.SequenceEqual(artifact.Content.Span))
    throw new InvalidOperationException("Repository returned different artifact bytes.");

Console.WriteLine($"Repository round trip verified: {artifact.Address}");

var bundle = new AssemblyMicroBundleArtifactMaterializer().Materialize(retrieved);

if (bundle.Id != bundleId)
{
    throw new InvalidOperationException(
        $"Materialized MicroBundle ID {bundle.Id} does not match requested ID {bundleId}.");
}

Console.WriteLine($"FSM_COS materialization verified: MicroBundle {bundle.Id} ({bundle.GetType().FullName})");
return 0;
