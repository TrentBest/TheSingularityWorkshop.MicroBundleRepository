using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Host.Tests;

public sealed class MicroBundleWriteSecurityTests
{
    [Fact]
    public async Task Put_ReturnsServiceUnavailableWhenWriteAuthorizationIsNotConfigured()
    {
        using var factory = new RestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.PutAsync(
            "/api/microbundles/42/1.0.0/" + new string('a', 64),
            new StringContent("{\"bundleId\":42,\"version\":\"1.0.0\",\"contentHash\":\""
                + new string('a', 64) + "\",\"contentBase64\":\"\"}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Put_RejectsMissingWriteKeyBeforeReadingRequestBody()
    {
        using var factory = new RestHostFactory(configureKey: true);
        using var client = factory.CreateClient();

        using var response = await client.PutAsync(
            "/api/microbundles/42/1.0.0/" + new string('a', 64),
            new StringContent("{\"bundleId\":42,\"version\":\"1.0.0\",\"contentHash\":\""
                + new string('a', 64) + "\",\"contentBase64\":\"\"}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }


    [Fact]
    public async Task Put_RejectsIncorrectWriteKey()
    {
        using var factory = new RestHostFactory(configureKey: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Repository-Write-Key", "incorrect-key");

        using var response = await client.PutAsync(
            "/api/microbundles/42/1.0.0/" + new string('a', 64),
            new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_AcceptsConfiguredWriteKeyAndStoresVerifiedArtifact()
    {
        using var factory = new RestHostFactory(configureKey: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Repository-Write-Key", "test-only-write-key");

        var bytes = Encoding.UTF8.GetBytes("authorized write integration test");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var contentBase64 = Convert.ToBase64String(bytes);
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            bundleId = 42,
            version = "1.0.0",
            contentHash = hash,
            contentBase64
        });

        using var response = await client.PutAsync(
            $"/api/microbundles/42/1.0.0/{hash}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var getResponse = await client.GetAsync($"/api/microbundles/42/1.0.0/{hash}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var responseBody = await getResponse.Content.ReadAsStringAsync();
        Assert.Contains(contentBase64, responseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cors_DoesNotGrantCrossOriginAccessWhenNoOriginsAreConfigured()
    {
        using var factory = new RestHostFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://untrusted.example");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_AllowsConfiguredOriginOnly()
    {
        using var factory = new RestHostFactory(allowedCorsOrigin: "https://webpage.example");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://webpage.example");

        using var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal("https://webpage.example", origins!.Single());
    }

    private sealed class RestHostFactory(bool configureKey = false, string? allowedCorsOrigin = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Repository:Mode"] = "Local",
                    ["Repository:LocalRoot"] = Path.Combine(Path.GetTempPath(), "mbrepo-write-tests"),
                    ["Repository:AllowedCorsOrigins:0"] = allowedCorsOrigin
                };
                if (configureKey)
                    settings["Repository:WriteApiKey"] = "test-only-write-key";
                configuration.AddInMemoryCollection(settings);
            });
        }
    }
}
