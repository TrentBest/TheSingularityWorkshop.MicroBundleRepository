using System.Diagnostics;

namespace TheSingularityWorkshop.MicroBundleRepository.Cli.Tests;

public sealed class CliProcessTests
{
    [Fact]
    public async Task Publish_WithoutWorkshopCredential_FailsClosed()
    {
        var result = await RunAsync("publish", "workshop/example", "1.0.0", "missing.bundle");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Publishing is disabled", result.StandardError);
        Assert.DoesNotContain("missing.bundle", result.StandardError);
    }

    [Fact]
    public async Task Put_RefusesLegacyCallerSelectedNumericIdentity()
    {
        var result = await RunAsync("put", "2110", "1.0.0", "bundle.bin");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("numeric-ID put command is retired", result.StandardError);
    }

    [Fact]
    public async Task Url_WithUnsupportedScheme_FailsThroughCliErrorBoundary()
    {
        var result = await RunAsync("--url", "file:///tmp/repository", "health");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ERROR:", result.StandardError);
        Assert.Contains("HTTP or HTTPS", result.StandardError);
        Assert.DoesNotContain("Unhandled exception", result.StandardError);
    }

    private static async Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var cliAssembly = Path.Combine(
            AppContext.BaseDirectory,
            "TheSingularityWorkshop.MicroBundleRepository.Cli.dll");
        Assert.True(File.Exists(cliAssembly), $"CLI assembly was not copied to test output: {cliAssembly}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add(cliAssembly);
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        process.StartInfo.Environment.Remove("MICRO_BUNDLE_REPOSITORY_WORKSHOP_PUBLISHER_KEY");
        process.StartInfo.Environment.Remove("MICRO_BUNDLE_REPOSITORY_URL");

        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
