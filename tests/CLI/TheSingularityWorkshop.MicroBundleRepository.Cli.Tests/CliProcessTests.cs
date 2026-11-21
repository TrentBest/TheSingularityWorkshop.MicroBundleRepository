using Xunit;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace TheSingularityWorkshop.MicroBundleRepository.Cli.Tests;

public sealed class CliProcessTests
{
    [Fact]
    public async Task Publish_WithoutWorkshopCredential_FailsClosed()
    {
        var result = await RunAsync(null, "publish", "workshop/example", "1.0.0", "missing.bundle");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Publishing is disabled", result.StandardError);
        Assert.DoesNotContain("missing.bundle", result.StandardError);
    }

    [Fact]
    public async Task Put_RefusesLegacyCallerSelectedNumericIdentity()
    {
        var result = await RunAsync(null, "put", "2110", "1.0.0", "bundle.bin");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("numeric-ID put command is retired", result.StandardError);
    }

    [Fact]
    public async Task Url_WithUnsupportedScheme_FailsThroughCliErrorBoundary()
    {
        var result = await RunAsync(null, "--url", "file:///tmp/repository", "health");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ERROR:", result.StandardError);
        Assert.Contains("HTTP or HTTPS", result.StandardError);
        Assert.DoesNotContain("Unhandled exception", result.StandardError);
    }

    [Fact]
    public async Task Publish_RejectsRemoteHttpBeforeReadingInput()
    {
        var result = await RunAsync(
            "test-workshop-publisher-secret",
            "--url", "http://example.com/",
            "publish", "workshop/example", "1.0.0", "missing.bundle");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("requires HTTPS", result.StandardError);
        Assert.DoesNotContain("missing.bundle", result.StandardError);
    }

    [Fact]
    public async Task Publish_DoesNotFollowRedirectWithWorkshopCredential()
    {
        var credential = "test-workshop-publisher-secret";
        var inputPath = Path.Combine(Path.GetTempPath(), $"microbundle-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(inputPath, [1, 2, 3, 4]);

        using var source = new HttpListener();
        using var destination = new HttpListener();
        var sourcePort = GetAvailablePort();
        var destinationPort = GetAvailablePort();
        var sourceUrl = $"http://127.0.0.1:{sourcePort}/";
        var destinationUrl = $"http://127.0.0.1:{destinationPort}/";
        source.Prefixes.Add(sourceUrl);
        destination.Prefixes.Add(destinationUrl);
        source.Start();
        destination.Start();

        try
        {
            var redirect = HandleRedirectAsync(source, destinationUrl);
            var result = await RunAsync(
                credential,
                "--url", sourceUrl,
                "publish", "workshop/example", "1.0.0", inputPath);
            var sourceKey = await redirect;

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("HTTP 307", result.StandardError);
            Assert.Equal(credential, sourceKey);

            using var noRedirect = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => destination.GetContextAsync().WaitAsync(noRedirect.Token));
        }
        finally
        {
            source.Stop();
            destination.Stop();
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task Publish_UsesWorkshopCredentialAndVerifiesExactReadBack()
    {
        var content = new byte[] { 3, 1, 4, 1, 5, 9, 2, 6 };
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var credential = "test-workshop-publisher-secret";
        var inputPath = Path.Combine(Path.GetTempPath(), $"microbundle-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(inputPath, content);

        using var listener = new HttpListener();
        var portProbe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var baseUrl = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        try
        {
            var server = HandlePublishAsync(listener, content, hash, credential);
            var result = await RunAsync(
                credential,
                "--url", baseUrl,
                "publish", "workshop/example", "1.0.0", inputPath);
            var requests = await server;

            Assert.Equal(0, result.ExitCode);
            Assert.Contains($"Content SHA-256:    {hash}", result.StandardOutput);
            Assert.Contains("Bytes verified:     8", result.StandardOutput);
            Assert.DoesNotContain(credential, result.StandardOutput);
            Assert.DoesNotContain(credential, result.StandardError);
            Assert.Equal(2, requests.Count);
            Assert.Equal("PUT", requests[0].Method);
            Assert.Equal("GET", requests[1].Method);
            Assert.Equal(credential, requests[0].PublisherKey);
            Assert.Null(requests[1].PublisherKey);
            Assert.Equal("workshop/example", requests[0].Ontology);
            Assert.Equal(Convert.ToBase64String(content), requests[0].ContentBase64);
            Assert.Equal(hash, requests[0].ContentHash);
            Assert.Equal(hash, requests[1].ContentHash);
        }
        finally
        {
            listener.Stop();
            File.Delete(inputPath);
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string?> HandleRedirectAsync(HttpListener listener, string destinationUrl)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        var publisherKey = context.Request.Headers["X-Workshop-Publisher-Key"];
        context.Response.StatusCode = (int)HttpStatusCode.TemporaryRedirect;
        context.Response.RedirectLocation = destinationUrl;
        context.Response.Close();
        return publisherKey;
    }

    private static async Task<List<ObservedRequest>> HandlePublishAsync(
        HttpListener listener,
        byte[] content,
        string hash,
        string credential)
    {
        var requests = new List<ObservedRequest>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        for (var i = 0; i < 2; i++)
        {
            var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync(timeout.Token);
            var request = new ObservedRequest(
                context.Request.HttpMethod,
                context.Request.Url!.AbsolutePath,
                context.Request.Headers["X-Workshop-Publisher-Key"],
                null,
                null,
                null);

            if (i == 0)
            {
                using var document = JsonDocument.Parse(body);
                request = request with
                {
                    Ontology = document.RootElement.GetProperty("ontology").GetString(),
                    ContentBase64 = document.RootElement.GetProperty("contentBase64").GetString(),
                    ContentHash = hash
                };
                var binding = JsonSerializer.Serialize(new
                {
                    ontology = "workshop/example",
                    version = "1.0.0",
                    contentHash = hash,
                    bundleId = 73UL
                });
                await WriteResponseAsync(context, 200, binding);
            }
            else
            {
                request = request with { ContentHash = hash };
                var artifact = JsonSerializer.Serialize(new
                {
                    bundleId = 73UL,
                    version = "1.0.0",
                    contentHash = hash,
                    contentBase64 = Convert.ToBase64String(content)
                });
                await WriteResponseAsync(context, 200, artifact);
            }

            requests.Add(request);
        }

        return requests;
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, int statusCode, string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static async Task<ProcessResult> RunAsync(string? publisherCredential, params string[] arguments)
    {
        var repositoryRoot = FindRepositoryRoot();
        var cliProject = Path.Combine(
            repositoryRoot,
            "src",
            "CLI",
            "TheSingularityWorkshop.MicroBundleRepository.Cli",
            "TheSingularityWorkshop.MicroBundleRepository.Cli.csproj");
        Assert.True(File.Exists(cliProject), $"CLI project was not found: {cliProject}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in new[] { "run", "--no-build", "--configuration", "Release", "--project", cliProject, "--" })
            process.StartInfo.ArgumentList.Add(argument);
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.StartInfo.Environment.Remove("MICRO_BUNDLE_REPOSITORY_WORKSHOP_PUBLISHER_KEY");
        if (publisherCredential is not null)
            process.StartInfo.Environment["MICRO_BUNDLE_REPOSITORY_WORKSHOP_PUBLISHER_KEY"] = publisherCredential;
        process.StartInfo.Environment.Remove("MICRO_BUNDLE_REPOSITORY_URL");

        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The CLI process did not complete within 40 seconds.");
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TheSingularityWorkshop.MicroBundleRepository.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate TheSingularityWorkshop.MicroBundleRepository.slnx.");
    }

    private sealed record ObservedRequest(
        string Method,
        string Path,
        string? PublisherKey,
        string? Ontology,
        string? ContentBase64,
        string? ContentHash);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
