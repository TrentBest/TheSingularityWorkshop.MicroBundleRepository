using System.Net;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Accesses a MicroBundle repository through the FSM_REST transport boundary.</summary>
public sealed class RestMicroBundleRepository : IMicroBundleRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRestTransport _transport;
    private readonly Uri _baseUri;

    public RestMicroBundleRepository(Uri baseUri, IRestTransport transport)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(transport);
        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The repository REST URI must be absolute.", nameof(baseUri));

        _baseUri = EnsureTrailingSlash(baseUri);
        _transport = transport;
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
                ContentType: "application/json",
                Body: JsonSerializer.Serialize(dto, JsonOptions)),
            cancellationToken);

        EnsureSuccess(response);
    }

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
