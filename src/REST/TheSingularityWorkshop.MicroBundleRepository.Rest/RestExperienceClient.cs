using System.Net;
using System.Text.Json;
using TheSingularityWorkshop.FSM_REST;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest;

/// <summary>
/// Reads Experience publication pointers and immutable Experience artifacts through the repository REST API.
/// This client does not publish, unpublish, parse Experience manifests, or compose MicroBundles.
/// </summary>
public sealed class RestExperienceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRestTransport _transport;
    private readonly Uri _baseUri;

    public RestExperienceClient(Uri baseUri, IRestTransport transport)
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

    /// <summary>Gets the current immutable publication address, or null when the Experience is unpublished.</summary>
    public async ValueTask<ExperienceArtifactAddress?> GetPublishedAddressAsync(
        ulong experienceId,
        CancellationToken cancellationToken = default)
    {
        if (experienceId == 0)
            throw new ArgumentOutOfRangeException(nameof(experienceId));

        var response = await _transport.SendAsync(
            new RestRequest("GET", new Uri(_baseUri, $"api/experiences/{experienceId}/publication")),
            cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, "Experience publication lookup");

        var dto = JsonSerializer.Deserialize<ExperiencePublicationDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty publication response.");

        if (dto.ExperienceId != experienceId || !IsValidAddress(dto.Version, dto.ContentHash))
            throw new InvalidOperationException("The repository REST API returned an invalid Experience publication address.");

        return new ExperienceArtifactAddress(dto.ExperienceId, dto.Version, dto.ContentHash);
    }

    /// <summary>Gets and verifies one immutable Experience artifact by its complete address.</summary>
    public async ValueTask<ExperienceArtifact?> GetArtifactAsync(
        ExperienceArtifactAddress address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.ExperienceId == 0 || !IsValidAddress(address.Version, address.ContentHash))
            throw new ArgumentException("The Experience artifact address is invalid.", nameof(address));

        var uri = new Uri(
            _baseUri,
            $"api/experiences/{address.ExperienceId}/{Uri.EscapeDataString(address.Version)}/{address.ContentHash}");
        var response = await _transport.SendAsync(new RestRequest("GET", uri), cancellationToken);

        if (response.StatusCode == (int)HttpStatusCode.NotFound)
            return null;

        EnsureSuccess(response, "Experience artifact retrieval");

        var dto = JsonSerializer.Deserialize<ExperienceArtifactDto>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("The repository REST API returned an empty Experience artifact response.");

        if (dto.ExperienceId != address.ExperienceId ||
            !string.Equals(dto.Version, address.Version, StringComparison.Ordinal) ||
            !string.Equals(dto.ContentHash, address.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The repository REST API returned an artifact with a different identity.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(dto.ContentBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The repository REST API returned invalid base64 artifact content.", ex);
        }

        // Construction recomputes SHA-256, so a transport response with corrupt bytes
        // cannot be materialized as a successful immutable artifact.
        return new ExperienceArtifact(address, content);
    }

    private static bool IsValidAddress(string version, string contentHash) =>
        !string.IsNullOrWhiteSpace(version) &&
        !version.Contains('/') &&
        !version.Contains('\\') &&
        contentHash.Length == 64 &&
        string.Equals(contentHash, contentHash.ToLowerInvariant(), StringComparison.Ordinal) &&
        contentHash.All(Uri.IsHexDigit);

    private static void EnsureSuccess(RestResponse response, string operation)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"{operation} failed with HTTP {response.StatusCode}: {response.Body}");
    }

    private sealed record ExperiencePublicationDto(ulong ExperienceId, string Version, string ContentHash);
    private sealed record ExperienceArtifactDto(ulong ExperienceId, string Version, string ContentHash, string ContentBase64);
}
