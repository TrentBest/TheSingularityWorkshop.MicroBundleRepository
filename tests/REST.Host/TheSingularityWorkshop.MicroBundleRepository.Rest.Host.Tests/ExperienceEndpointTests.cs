using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Host.Tests;

public sealed class ExperienceEndpointTests
{
    private static readonly byte[] Payload = "experience-contract-test"u8.ToArray();
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Payload)).ToLowerInvariant();
    private static readonly ExperienceArtifactAddress Address = new(42, "1.2.3", Hash);

    [Fact]
    public async Task Publication_ReturnsCurrentImmutableAddress()
    {
        using var factory = new RestHostFactory(
            new FakeCatalog(new ExperiencePublication(Address)),
            new FakeExperienceRepository(new ExperienceArtifact(Address, Payload)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/experiences/42/publication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(42, json.RootElement.GetProperty("experienceId").GetUInt64());
        Assert.Equal("1.2.3", json.RootElement.GetProperty("version").GetString());
        Assert.Equal(Hash, json.RootElement.GetProperty("contentHash").GetString());
    }

    [Fact]
    public async Task Publication_ReturnsNotFoundWhenExperienceIsUnpublished()
    {
        using var factory = new RestHostFactory(
            new FakeCatalog(null),
            new FakeExperienceRepository(null));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/experiences/42/publication");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Artifact_ReturnsExactBytesForValidImmutableAddress()
    {
        using var factory = new RestHostFactory(
            new FakeCatalog(new ExperiencePublication(Address)),
            new FakeExperienceRepository(new ExperienceArtifact(Address, Payload)));
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/experiences/42/{Address.Version}/{Hash}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actual = Convert.FromBase64String(json.RootElement.GetProperty("contentBase64").GetString()!);
        Assert.Equal(Payload, actual);
        Assert.Equal(Hash, json.RootElement.GetProperty("contentHash").GetString());
    }

    [Fact]
    public async Task Artifact_RejectsMalformedContentHashBeforeStorageAccess()
    {
        var repository = new FakeExperienceRepository(null);
        using var factory = new RestHostFactory(new FakeCatalog(null), repository);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/experiences/42/1.2.3/not-a-sha256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(repository.WasRead);
    }

    private sealed class RestHostFactory(
        IExperienceCatalog catalog,
        IExperienceRepository repository) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Repository:Mode"] = "Local",
                    ["Repository:LocalRoot"] = Path.Combine(Path.GetTempPath(), "mbrepo-host-tests", Guid.NewGuid().ToString("N"))
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExperienceCatalog>();
                services.RemoveAll<IExperienceRepository>();
                services.AddSingleton(catalog);
                services.AddSingleton(repository);
            });
        }
    }

    private sealed class FakeCatalog(ExperiencePublication? publication) : IExperienceCatalog
    {
        public ValueTask<ExperiencePublication?> GetPublishedAsync(
            ulong experienceId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(publication?.ExperienceId == experienceId ? publication : null);

        public ValueTask PublishAsync(
            ExperiencePublication publication,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask UnpublishAsync(
            ulong experienceId,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class FakeExperienceRepository(ExperienceArtifact? artifact) : IExperienceRepository
    {
        public bool WasRead { get; private set; }

        public ValueTask<ExperienceArtifact?> GetAsync(
            ExperienceArtifactAddress address,
            CancellationToken cancellationToken = default)
        {
            WasRead = true;
            return ValueTask.FromResult(
                artifact is not null && artifact.Address == address ? artifact : null);
        }

        public ValueTask PutAsync(
            ExperienceArtifact artifact,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }
}
