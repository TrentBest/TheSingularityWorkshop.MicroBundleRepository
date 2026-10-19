using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
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

    // Experience artifacts and their mutable publication pointer use a separate
    // container from MicroBundle artifacts. They remain opaque to the REST host.
    var experienceOptions = new AzureExperienceRepositoryOptions
    {
        StorageAccountUri = storageUri,
        ContainerName = builder.Configuration["Repository:ExperienceContainerName"] ?? "experiences",
        MaxArtifactBytes = builder.Configuration.GetValue<long?>("Repository:MaxExperienceArtifactBytes") ?? 10 * 1024 * 1024
    };

    builder.Services.AddSingleton<AzureExperienceRepository>(_ =>
        new AzureExperienceRepository(experienceOptions));
    builder.Services.AddSingleton<IExperienceRepository>(services =>
        services.GetRequiredService<AzureExperienceRepository>());
    builder.Services.AddSingleton<AzureExperienceCatalog>(_ =>
        new AzureExperienceCatalog(experienceOptions));
    builder.Services.AddSingleton<IExperienceCatalog>(services =>
        services.GetRequiredService<AzureExperienceCatalog>());
}

// Cross-origin browser access is opt-in. Configure Repository:AllowedCorsOrigins
// with the exact trusted origins that host the consumer (for example, WebPage).
// An empty list intentionally grants no cross-origin browser access.
var allowedCorsOrigins = builder.Configuration
    .GetSection("Repository:AllowedCorsOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("RepositoryCors", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();
app.UseRouting();
app.UseCors("RepositoryCors");

// Writes are disabled unless the deployment explicitly configures a shared API key.
// This middleware runs before minimal-API body binding, so unauthenticated callers
// cannot force JSON/base64 request-body buffering on the write endpoint.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPut(context.Request.Method) &&
        context.Request.Path.StartsWithSegments("/api/microbundles"))
    {
        var configuredKey = context.RequestServices
            .GetRequiredService<IConfiguration>()["Repository:WriteApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "MicroBundle writes are disabled because write authorization is not configured."
            });
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Repository-Write-Key", out var suppliedKey) ||
            !KeysMatch(configuredKey, suppliedKey.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "A valid write authorization key is required." });
            return;
        }

        var maxRequestBytes = context.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue<long?>("Repository:MaxMicroBundleRequestBytes") ?? 16 * 1024 * 1024;

        if (maxRequestBytes <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { error = "The MicroBundle request-size limit is invalid." });
            return;
        }

        var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false })
            bodySizeFeature.MaxRequestBodySize = maxRequestBytes;
    }

    await next();
});

static bool KeysMatch(string expected, string supplied)
{
    var expectedBytes = Encoding.UTF8.GetBytes(expected);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
    return expectedBytes.Length == suppliedBytes.Length &&
        CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
}

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

app.MapGet(
    "/api/experiences/{experienceId}/publication",
    async (
        ulong experienceId,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
    {
        if (experienceId == 0)
            return Results.BadRequest(new { error = "Experience ID must be greater than zero." });

        // Local mode currently has no local Experience storage adapter. Return an
        // explicit service-unavailable response instead of a DI/startup failure.
        var catalog = services.GetService<IExperienceCatalog>();
        if (catalog is null)
            return Results.Problem(
                title: "Experience publication catalog is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var publication = await catalog.GetPublishedAsync(experienceId, cancellationToken);
        if (publication is null)
            return Results.NotFound(new { error = "No publication exists for this Experience." });

        if (publication.Address.ExperienceId != experienceId ||
            !IsValidExperienceAddress(publication.Address.Version, publication.Address.ContentHash))
        {
            return Results.Problem(
                title: "The stored Experience publication address is invalid.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new
        {
            experienceId = publication.Address.ExperienceId,
            version = publication.Address.Version,
            contentHash = publication.Address.ContentHash
        });
    });

app.MapGet(
    "/api/experiences/{experienceId}/{version}/{contentHash}",
    async (
        ulong experienceId,
        string version,
        string contentHash,
        IConfiguration configuration,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
    {
        if (experienceId == 0 || !IsValidExperienceAddress(version, contentHash))
            return Results.BadRequest(new { error = "The Experience artifact address is invalid." });

        var repository = services.GetService<IExperienceRepository>();
        if (repository is null)
            return Results.Problem(
                title: "Experience artifact storage is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        ExperienceArtifact? artifact;
        try
        {
            artifact = await repository.GetAsync(
                new ExperienceArtifactAddress(experienceId, version, contentHash),
                cancellationToken);
        }
        catch (ExperienceArtifactTooLargeException)
        {
            return Results.Problem(
                title: "Experience artifact exceeds the configured storage limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        catch (ArgumentException)
        {
            // The storage path existed, but its bytes did not match the requested
            // content identity. Never return corrupt bytes as a successful artifact.
            return Results.Problem(
                title: "Experience artifact integrity verification failed.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (artifact is null)
            return Results.NotFound();

        // Bound the response payload. Storage adapters should additionally enforce
        // a read limit before buffering when handling untrusted/large artifacts.
        var maxArtifactBytes = configuration.GetValue<long?>("Repository:MaxExperienceArtifactBytes") ?? 10 * 1024 * 1024;
        if (artifact.Content.Length > maxArtifactBytes)
            return Results.Problem(
                title: "Experience artifact exceeds the configured response limit.",
                statusCode: StatusCodes.Status413PayloadTooLarge);

        return Results.Ok(new
        {
            experienceId = artifact.Address.ExperienceId,
            version = artifact.Address.Version,
            contentHash = artifact.Address.ContentHash,
            contentBase64 = Convert.ToBase64String(artifact.Content.ToArray())
        });
    });

static bool IsValidExperienceAddress(string version, string contentHash)
{
    if (string.IsNullOrWhiteSpace(version) ||
        version.Contains('/') ||
        version.Contains('\\') ||
        contentHash is not { Length: 64 } ||
        !string.Equals(contentHash, contentHash.ToLowerInvariant(), StringComparison.Ordinal))
    {
        return false;
    }

    return contentHash.All(Uri.IsHexDigit);
}

app.Run();

public partial class Program { }
