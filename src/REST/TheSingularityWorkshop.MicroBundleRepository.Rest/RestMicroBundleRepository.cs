using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Accesses a MicroBundle repository through the FSM_REST transport boundary.</summary>
public sealed class RestMicroBundleRepository : IMicroBundleIdentityRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRestTransport _transport;
    private readonly Uri _baseUri;
    private readonly string? _writeApiKey;

    public RestMicroBundleRepository(Uri baseUri, IRestTransport transport, string? writeApiKey = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(transport);
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The repository REST URI must be absolute.", nameof(baseUri));

        _baseUri = EnsureTrailingSlash(baseUri);
        _transport = transport;
        _writeApiKey = string.IsNullOrWhiteSpace(writeApiKey) ? null : writeApiKey;
    }

    public async ValueTask<MicroBundleArtifact?> GetAsync(
        MicroBundleArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        var response = await _transport.SendAsync(
            new RestRequest("GET", CreateArtifactUri(address)),
            cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response);

        var dto = JsonSerializer.Deserialize<MicroBundleArtifactDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty artifact response.");

        ValidateIdentity(dto, address);
        var content = Convert.FromBase64String(dto.ContentBase64);
        return new MicroBundleArtifact(address, content);
    }

    public async ValueTask PutAsync(
        MicroBundleArtifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var dto = new MicroBundleArtifactDto(
            artifact.Address.BundleId,
            artifact.Address.Version,
            artifact.Address.ContentHash,
            Convert.ToBase64String(artifact.Content.ToArray()));

        var response = await _transport.SendAsync(
            new RestRequest(
                "PUT",
                CreateArtifactUri(artifact.Address),
                Headers: CreateWriteHeaders(),
                ContentType: "application/json",
                Body: JsonSerializer.Serialize(dto, JsonOptions)),
            cancellationToken);

        EnsureSuccess(response);
    }

    /// <summary>Uploads bytes and resolves their durable portable identity binding.</summary>
    public async ValueTask<MicroBundleIdentityBinding> PutAsync(
        string ontology,
        string version,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var expectedHash = Convert.ToHexString(SHA256.HashData(content.Span)).ToLowerInvariant();
        var response = await _transport.SendAsync(
            new RestRequest(
                "PUT",
                new Uri(_baseUri, "api/microbundles/identity"),
                Headers: CreateWriteHeaders(),
                ContentType: "application/json",
                Body: JsonSerializer.Serialize(
                    new MicroBundleIdentityWriteDto(ontology, version, Convert.ToBase64String(content.ToArray())),
                    JsonOptions)),
            cancellationToken);
        EnsureSuccess(response);

        var dto = JsonSerializer.Deserialize<MicroBundleIdentityBindingDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty identity binding.");
        var binding = ToBinding(dto);
        if (!StringComparer.Ordinal.Equals(binding.Identity.Ontology, ontology) ||
            !StringComparer.Ordinal.Equals(binding.Identity.Version, version) ||
            !StringComparer.Ordinal.Equals(binding.Identity.ContentHash, expectedHash))
            throw new InvalidOperationException("The repository REST API returned a binding for a different portable identity.");

        await VerifyArtifactAsync(binding, content, cancellationToken);
        return binding;
    }

    /// <summary>Resolves a complete portable revision identity to its repository-local binding.</summary>
    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        MicroBundleArtifactIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var query = $"ontology={Uri.EscapeDataString(identity.Ontology)}&version={Uri.EscapeDataString(identity.Version)}&contentHash={identity.ContentHash}";
        var response = await _transport.SendAsync(
            new RestRequest("GET", new Uri(_baseUri, $"api/microbundles/identity?{query}")),
            cancellationToken);
        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;
        EnsureSuccess(response);

        var dto = JsonSerializer.Deserialize<MicroBundleIdentityBindingDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty identity binding.");
        var binding = ToBinding(dto);
        if (binding.Identity != identity)
            throw new InvalidOperationException("The repository REST API returned a different portable identity.");
        await VerifyArtifactAsync(binding, expectedContent: null, cancellationToken);
        return binding;
    }

    /// <summary>Resolves a repository-local numeric ID to its exact portable revision binding.</summary>
    public async ValueTask<MicroBundleIdentityBinding?> ResolveAsync(
        ulong bundleId,
        CancellationToken cancellationToken = default)
    {
        if (bundleId == 0)
            return null;

        var response = await _transport.SendAsync(
            new RestRequest("GET", new Uri(_baseUri, $"api/microbundles/identity/{bundleId}")),
            cancellationToken);
        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;
        EnsureSuccess(response);

        var dto = JsonSerializer.Deserialize<MicroBundleIdentityBindingDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty identity binding.");
        var binding = ToBinding(dto);
        if (binding.Address.BundleId != bundleId)
            throw new InvalidOperationException("The repository REST API returned a different repository-local ID.");
        await VerifyArtifactAsync(binding, expectedContent: null, cancellationToken);
        return binding;
    }

    private async Task VerifyArtifactAsync(
        MicroBundleIdentityBinding binding,
        ReadOnlyMemory<byte>? expectedContent,
        CancellationToken cancellationToken)
    {
        var artifact = await GetAsync(binding.Address, cancellationToken)
            ?? throw new InvalidDataException("The repository identity binding points to a missing artifact.");
        if (expectedContent is ReadOnlyMemory<byte> expected &&
            !artifact.Content.Span.SequenceEqual(expected.Span))
            throw new InvalidDataException("The repository returned artifact bytes that differ from the uploaded content.");
    }

    private static MicroBundleIdentityBinding ToBinding(MicroBundleIdentityBindingDto dto)
    {
        var identity = new MicroBundleArtifactIdentity(dto.Ontology, dto.Version, dto.ContentHash);
        var address = new MicroBundleArtifactAddress(dto.BundleId, dto.Version, dto.ContentHash);
        return new MicroBundleIdentityBinding(identity, address);
    }

    private Dictionary<string, string>? CreateWriteHeaders() =>
        _writeApiKey is null
            ? null
            : new Dictionary<string, string> { ["X-Workshop-Publisher-Key"] = _writeApiKey };

    private Uri CreateArtifactUri(MicroBundleArtifactAddress address) =>
        new(
            _baseUri,
            $"api/microbundles/{address.BundleId}/{Uri.EscapeDataString(address.Version)}/{address.ContentHash}");

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);

    private static void EnsureSuccess(RestResponse response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"MicroBundle Repository REST request failed with HTTP {response.StatusCode}: {response.Body}");
    }

    private static void ValidateIdentity(
        MicroBundleArtifactDto dto,
        MicroBundleArtifactAddress expected)
    {
        if (dto.BundleId != expected.BundleId ||
            !string.Equals(dto.Version, expected.Version, StringComparison.Ordinal) ||
            !string.Equals(dto.ContentHash, expected.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The repository REST API returned an artifact with a different identity.");
        }
    }
}
