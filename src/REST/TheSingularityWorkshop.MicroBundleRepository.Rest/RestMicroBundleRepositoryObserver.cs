using System.Net;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>Observes MicroBundle artifact inventory through the repository REST boundary.</summary>
public sealed class RestMicroBundleRepositoryObserver : IMicroBundleRepositoryObserver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRestTransport _transport;
    private readonly Uri _baseUri;

    public RestMicroBundleRepositoryObserver(Uri baseUri, IRestTransport transport)
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

    public async ValueTask<IReadOnlyList<MicroBundleArtifactObservation>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _transport.SendAsync(
            new RestRequest("GET", new Uri(_baseUri, "api/microbundles")),
            cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return Array.Empty<MicroBundleArtifactObservation>();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"MicroBundle Repository inventory request failed with HTTP {response.StatusCode}: {response.Body}");

        var observations = JsonSerializer.Deserialize<List<MicroBundleArtifactObservationDto>>(
                               response.Body,
                               JsonOptions)
                           ?? new List<MicroBundleArtifactObservationDto>();

        return observations
            .Select(observation => new MicroBundleArtifactObservation(
                new MicroBundleArtifactAddress(
                    observation.BundleId,
                    observation.Version,
                    observation.ContentHash),
                observation.ContentLength,
                observation.LastModified))
            .ToArray();
    }
}
