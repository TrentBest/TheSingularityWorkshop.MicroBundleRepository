using System.Net.Http.Headers;
using System.Text;
using TheSingularityWorkshop.FSM_REST;

namespace TheSingularityWorkshop.MicroBundleRepository.Cli;

/// <summary>Small CLI-owned HTTP adapter for the FSM_REST transport contract.</summary>
internal sealed class HttpClientRestTransport(HttpClient client) : IRestTransport
{
    private readonly HttpClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<RestResponse> SendAsync(RestRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Uri);
        if (request.Headers is not null)
        {
            foreach (var header in request.Headers)
            {
                if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    throw new InvalidOperationException($"Unable to apply request header '{header.Key}'.");
            }
        }

        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, Encoding.UTF8);
            message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType ?? "application/json");
        }

        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var headers = response.Headers.Concat(response.Content.Headers)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.SelectMany(pair => pair.Value)), StringComparer.OrdinalIgnoreCase);
        return new RestResponse((int)response.StatusCode, response.ReasonPhrase, headers, body);
    }
}
