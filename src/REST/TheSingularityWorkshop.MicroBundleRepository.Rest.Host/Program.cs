using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;

var builder = WebApplication.CreateBuilder(args);

var storageAccountUri = builder.Configuration["Repository:StorageAccountUri"];
if (!Uri.TryCreate(storageAccountUri, UriKind.Absolute, out var storageUri))
    throw new InvalidOperationException(
        "Repository:StorageAccountUri must be configured with an absolute Azure Storage account URI.");

var containerName = builder.Configuration["Repository:ContainerName"] ?? "microbundles";
var experienceContainerName = builder.Configuration["Repository:ExperienceContainerName"] ?? "experiences";

builder.Services.AddSingleton<IMicroBundleRepository>(_ =>
    new AzureMicroBundleRepository(new AzureMicroBundleRepositoryOptions
    {
        StorageAccountUri = storageUri,
        ContainerName = containerName
    }));

builder.Services.AddSingleton<IExperienceRepository>(_ =>
    new AzureExperienceRepository(new AzureExperienceRepositoryOptions
    {
        StorageAccountUri = storageUri,
        ContainerName = experienceContainerName
    }));

builder.Services.AddSingleton<IExperienceCatalog>(_ =>
    new AzureExperienceCatalog(new AzureExperienceRepositoryOptions
    {
        StorageAccountUri = storageUri,
        ContainerName = experienceContainerName
    }));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => new
{
    name = "TheSingularityWorkshop.MicroBundleRepository.Rest",
    role = "microbundle-repository",
    status = "alpha"
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet(
    "/api/microbundles",
    async (
        int? pageSize,
        string? continuationToken,
        ulong? bundleId,
        string? version,
        IMicroBundleRepository repository,
        CancellationToken cancellationToken) =>
    {
        var request = new MicroBundleArtifactListRequest(
            pageSize ?? 100,
            continuationToken,
            bundleId,
            version);

        try
        {
            var page = await repository.ListAsync(request, cancellationToken);
            return Results.Ok(new MicroBundleArtifactListDto(
                page.Items.Select(address => new MicroBundleArtifactAddressDto(
                    address.BundleId,
                    address.Version,
                    address.ContentHash)).ToArray(),
                page.ContinuationToken));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
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
            Convert.ToBase64String(artifact.Content.ToArray()),
            artifact.SemanticAddress));
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
            await repository.PutAsync(new MicroBundleArtifact(address, content, dto.SemanticAddress), cancellationToken);
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

app.MapGet(
    "/api/experiences",
    async (
        IExperienceCatalog catalog,
        CancellationToken cancellationToken) =>
    {
        var publications = await catalog.ListPublishedAsync(cancellationToken);
        return Results.Ok(publications.Select(publication => new
        {
            experienceId = publication.ExperienceId,
            version = publication.Address.Version,
            contentHash = publication.Address.ContentHash
        }));
    });

app.MapGet(
    "/api/experiences/{experienceId:long}",
    async (
        ulong experienceId,
        IExperienceCatalog catalog,
        CancellationToken cancellationToken) =>
    {
        var publication = await catalog.GetPublishedAsync(experienceId, cancellationToken);
        if (publication is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            experienceId = publication.ExperienceId,
            version = publication.Address.Version,
            contentHash = publication.Address.ContentHash
        });
    });

app.MapGet(
    "/api/experiences/{experienceId:long}/{version}/{contentHash}",
    async (
        ulong experienceId,
        string version,
        string contentHash,
        IExperienceRepository repository,
        CancellationToken cancellationToken) =>
    {
        ExperienceArtifactAddress address;
        try
        {
            address = new ExperienceArtifactAddress(experienceId, version, contentHash);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var artifact = await repository.GetAsync(address, cancellationToken);
        if (artifact is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            experienceId = artifact.Address.ExperienceId,
            version = artifact.Address.Version,
            contentHash = artifact.Address.ContentHash,
            contentBase64 = Convert.ToBase64String(artifact.Content.ToArray())
        });
    });

app.Run();
