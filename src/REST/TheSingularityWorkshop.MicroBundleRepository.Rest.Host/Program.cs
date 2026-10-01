using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Local;
using TheSingularityWorkshop.MicroBundleRepository.Rest;

var builder = WebApplication.CreateBuilder(args);

var mode = builder.Configuration["Repository:Mode"] ?? "Azure";

if (string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase))
{
    var root = builder.Configuration["Repository:LocalRoot"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "microbundles");

    var local = new FileSystemMicroBundleRepository(root);
    builder.Services.AddSingleton<IMicroBundleRepository>(local);
    builder.Services.AddSingleton<IMicroBundleRepositoryObserver>(local);
}
else
{
    var storageAccountUri = builder.Configuration["Repository:StorageAccountUri"];
    if (!Uri.TryCreate(storageAccountUri, UriKind.Absolute, out var storageUri))
        throw new InvalidOperationException(
            "Repository:StorageAccountUri must be configured with an absolute Azure Storage account URI.");

    var containerName = builder.Configuration["Repository:ContainerName"] ?? "microbundles";

    builder.Services.AddSingleton<AzureMicroBundleRepository>(_ =>
        new AzureMicroBundleRepository(new AzureMicroBundleRepositoryOptions
        {
            StorageAccountUri = storageUri,
            ContainerName = containerName
        }));

    builder.Services.AddSingleton<IMicroBundleRepository>(services =>
        services.GetRequiredService<AzureMicroBundleRepository>());

    builder.Services.AddSingleton<IMicroBundleRepositoryObserver>(services =>
        services.GetRequiredService<AzureMicroBundleRepository>());
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", (IConfiguration configuration) => new
{
    name = "TheSingularityWorkshop.MicroBundleRepository.Rest",
    role = "microbundle-repository",
    status = "alpha",
    storage = configuration["Repository:Mode"] ?? "Azure"
});

app.MapGet("/health", (IConfiguration configuration) =>
    Results.Ok(new
    {
        status = "healthy",
        storage = configuration["Repository:Mode"] ?? "Azure"
    }));

app.MapGet(
    "/api/microbundles",
    async (
        IMicroBundleRepositoryObserver observer,
        CancellationToken cancellationToken) =>
    {
        var observations = await observer.ListAsync(cancellationToken);

        return Results.Ok(observations.Select(observation =>
            new MicroBundleArtifactObservationDto(
                observation.Address.BundleId,
                observation.Address.Version,
                observation.Address.ContentHash,
                observation.ContentLength,
                observation.LastModified)));
    });

app.MapGet(
    "/api/microbundles/{bundleId}/{version}/{contentHash}",
    async (
        ulong bundleId,
        string version,
        string contentHash,
        IMicroBundleRepository repository,
        CancellationToken cancellationToken) =>
    {
        MicroBundleArtifactAddress address;
        try
        {
            address = new MicroBundleArtifactAddress(bundleId, version, contentHash);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var artifact = await repository.GetAsync(address, cancellationToken);
        if (artifact is null)
            return Results.NotFound();

        return Results.Ok(new MicroBundleArtifactDto(
            artifact.Address.BundleId,
            artifact.Address.Version,
            artifact.Address.ContentHash,
            Convert.ToBase64String(artifact.Content.ToArray())));
    });

app.MapPut(
    "/api/microbundles/{bundleId}/{version}/{contentHash}",
    async (
        ulong bundleId,
        string version,
        string contentHash,
        MicroBundleArtifactDto dto,
        IMicroBundleRepository repository,
        CancellationToken cancellationToken) =>
    {
        if (dto.BundleId != bundleId ||
            !string.Equals(dto.Version, version, StringComparison.Ordinal) ||
            !string.Equals(dto.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Route identity and artifact identity must match." });
        }

        try
        {
            var address = new MicroBundleArtifactAddress(bundleId, version, contentHash);
            var content = Convert.FromBase64String(dto.ContentBase64);
            await repository.PutAsync(new MicroBundleArtifact(address, content), cancellationToken);
            return Results.NoContent();
        }
        catch (FormatException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });

app.Run();
