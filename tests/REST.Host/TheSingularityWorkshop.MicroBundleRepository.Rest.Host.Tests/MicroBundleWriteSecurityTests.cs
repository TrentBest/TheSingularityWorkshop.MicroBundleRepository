using System.Net;
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

    private sealed class RestHostFactory(bool configureKey = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Repository:Mode"] = "Local",
                    ["Repository:LocalRoot"] = Path.Combine(Path.GetTempPath(), "mbrepo-write-tests")
                };
                if (configureKey)
                    settings["Repository:WriteApiKey"] = "test-only-write-key";
                configuration.AddInMemoryCollection(settings);
            });
        }
    }
}
