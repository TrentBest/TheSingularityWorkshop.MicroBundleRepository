using System.IO;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Reads bounded, identity-only artifact pages over the repository REST boundary.</summary>
public sealed class RestMicroBundleArtifactCatalog : IMicroBundleArtifactCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRestTransport _transport;
    private readonly Uri _baseUri;

    public RestMicroBundleArtifactCatalog(Uri baseUri, IRestTransport transport)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(transport);

        if (!baseUri.IsAbsoluteUri)
            throw new ArgumentException("The repository REST URI must be absolute.", nameof(baseUri));

        _baseUri = baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseUri
            : new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
        _transport = transport;
    }

    public async ValueTask<MicroBundleArtifactListPage> ListPageAsync(
        MicroBundleArtifactListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new List<string> { $"pageSize={request.PageSize}" };
        if (request.ContinuationToken is not null)
            query.Add($"continuationToken={Uri.EscapeDataString(request.ContinuationToken)}");
        if (request.BundleId is ulong bundleId)
            query.Add($"bundleId={bundleId}");
        if (request.Version is not null)
            query.Add($"version={Uri.EscapeDataString(request.Version)}");

        var response = await _transport.SendAsync(
            new RestRequest("GET", new Uri(_baseUri, "api/microbundles/identities?" + string.Join("&", query))),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"MicroBundle Repository identity inventory request failed with HTTP {response.StatusCode}: {response.Body}");

        var page = JsonSerializer.Deserialize<MicroBundleArtifactListPageDto>(response.Body, JsonOptions)
            ?? throw new InvalidDataException("The repository returned an empty identity inventory response.");

        if (page.Items is null)
            throw new InvalidDataException("The repository identity inventory response did not contain an item list.");

        var addresses = page.Items.Select(item =>
            new MicroBundleArtifactAddress(item.BundleId, item.Version, item.ContentHash)).ToArray();

        return new MicroBundleArtifactListPage(addresses, page.ContinuationToken);
    }
}
