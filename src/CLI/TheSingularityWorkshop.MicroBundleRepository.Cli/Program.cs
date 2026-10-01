using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.MicroBundleRepository.Core;

const string usage = """
The Singularity Workshop — MicroBundle Repository CLI

Usage:
  repo-cli [--url <repository-url>] health
  repo-cli [--url <repository-url>] list
  repo-cli [--url <repository-url>] get <bundle-id> <version> <sha256> <output-file>
  repo-cli [--url <repository-url>] put <bundle-id> <version> <input-file>

Environment:
  MICRO_BUNDLE_REPOSITORY_URL
    Default: http://localhost:5000/

Examples:
  repo-cli health
  repo-cli list
  repo-cli put 2110 1.0.0 ./bundle.bin
  repo-cli get 2110 1.0.0 <sha256> ./bundle.bin
""";

var parsed = ParseArguments(args);
if (parsed.Command is null)
{
    Console.WriteLine(usage);
    return 1;
}

using var http = new HttpClient
{
    BaseAddress = parsed.BaseUri
};

try
{
    return parsed.Command switch
    {
        "health" => await HealthAsync(http),
        "list" => await ListAsync(http),
        "get" => await GetAsync(http, parsed.Arguments),
        "put" => await PutAsync(http, parsed.Arguments),
        _ => Fail($"Unknown command '{parsed.Command}'.\n\n{usage}")
    };
}
catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or JsonException)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}

static (Uri BaseUri, string? Command, string[] Arguments) ParseArguments(string[] args)
{
    var url = Environment.GetEnvironmentVariable("MICRO_BUNDLE_REPOSITORY_URL")
              ?? "http://localhost:5000/";

    var remaining = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "--url", StringComparison.OrdinalIgnoreCase))
        {
            if (++i >= args.Length || !Uri.TryCreate(args[i], UriKind.Absolute, out var supplied))
                throw new ArgumentException("--url requires an absolute URI.");

            url = supplied.ToString();
            continue;
        }

        remaining.Add(args[i]);
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
        throw new ArgumentException($"Repository URL '{url}' is not a valid absolute URI.");

    if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);

    return (
        baseUri,
        remaining.Count == 0 ? null : remaining[0].ToLowerInvariant(),
        remaining.Skip(1).ToArray());
}

static async Task<int> HealthAsync(HttpClient http)
{
    using var response = await http.GetAsync("health");
    var body = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"Repository health check failed with HTTP {(int)response.StatusCode}: {body}");

    Console.WriteLine("Repository: HEALTHY");
    Console.WriteLine($"Endpoint:   {http.BaseAddress}");
    return 0;
}

static async Task<int> ListAsync(HttpClient http)
{
    var observations = await http.GetFromJsonAsync<List<Observation>>("api/microbundles")
                       ?? [];

    Console.WriteLine($"Repository: {http.BaseAddress}");
    Console.WriteLine($"Artifacts:  {observations.Count}");
    Console.WriteLine();

    if (observations.Count == 0)
    {
        Console.WriteLine("No MicroBundle artifacts are stored.");
        return 0;
    }

    foreach (var item in observations.OrderBy(x => x.BundleId).ThenBy(x => x.Version))
    {
        Console.WriteLine($"{item.BundleId}/{item.Version}/{item.ContentHash}  {item.ContentLength} bytes  {item.LastModified:u}");
    }

    return 0;
}

static async Task<int> GetAsync(HttpClient http, string[] args)
{
    if (args.Length != 4)
        return Fail("get requires: <bundle-id> <version> <sha256> <output-file>");

    if (!ulong.TryParse(args[0], out var bundleId))
        return Fail($"Invalid bundle ID '{args[0]}'.");

    var address = new MicroBundleArtifactAddress(bundleId, args[1], args[2]);

    using var response = await http.GetAsync(
        $"api/microbundles/{address.BundleId}/{Uri.EscapeDataString(address.Version)}/{address.ContentHash}");

    if (!response.IsSuccessStatusCode)
    {
        if ((int)response.StatusCode == 404)
            return Fail($"Artifact {address} was not found.");

        throw new HttpRequestException($"Repository GET failed with HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    var dto = await response.Content.ReadFromJsonAsync<ArtifactDto>()
              ?? throw new InvalidOperationException("Repository returned an empty artifact.");

    var content = Convert.FromBase64String(dto.ContentBase64);
    var artifact = new MicroBundleArtifact(address, content);

    await File.WriteAllBytesAsync(args[3], artifact.Content.ToArray());

    Console.WriteLine($"Retrieved: {artifact.Address}");
    Console.WriteLine($"Bytes:     {artifact.Content.Length}");
    Console.WriteLine($"Saved:     {Path.GetFullPath(args[3])}");
    return 0;
}

static async Task<int> PutAsync(HttpClient http, string[] args)
{
    if (args.Length != 3)
        return Fail("put requires: <bundle-id> <version> <input-file>");

    if (!ulong.TryParse(args[0], out var bundleId))
        return Fail($"Invalid bundle ID '{args[0]}'.");

    var content = await File.ReadAllBytesAsync(args[2]);
    var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    var address = new MicroBundleArtifactAddress(bundleId, args[1], hash);
    var artifact = new MicroBundleArtifact(address, content);

    var dto = new ArtifactDto(
        artifact.Address.BundleId,
        artifact.Address.Version,
        artifact.Address.ContentHash,
        Convert.ToBase64String(artifact.Content.ToArray()));

    using var response = await http.PutAsJsonAsync(
        $"api/microbundles/{address.BundleId}/{Uri.EscapeDataString(address.Version)}/{address.ContentHash}",
        dto);

    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"Repository PUT failed with HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    Console.WriteLine($"Published: {artifact.Address}");
    Console.WriteLine($"Bytes:     {artifact.Content.Length}");
    Console.WriteLine($"Source:    {Path.GetFullPath(args[2])}");
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

sealed record Observation(
    ulong BundleId,
    string Version,
    string ContentHash,
    long ContentLength,
    DateTimeOffset LastModified);

sealed record ArtifactDto(
    ulong BundleId,
    string Version,
    string ContentHash,
    string ContentBase64);
