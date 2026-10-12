using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CompanyHQ.Services.OpenStreetMap.Contracts.Dtos;

namespace CompanyHQ.Services.OpenStreetMap.Services;

/// <summary>
/// The only place CompanyHQ talks to Nominatim. It owns the HTTP concern — rate limiting, transport
/// failures, and deserialisation — and hands back the places as Nominatim describes them.
/// </summary>
public sealed class NominatimClient(
    HttpClient http,
    NominatimRateLimiter limiter,
    ILogger<NominatimClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Names the place at a coordinate, or null when Nominatim cannot or will not answer.</summary>
    internal async Task<NominatimResponse?> ReverseAsync(
        double latitude,
        double longitude,
        int zoom,
        string language,
        CancellationToken cancellationToken)
    {
        var route = string.Create(
            CultureInfo.InvariantCulture,
            $"reverse?format=jsonv2&addressdetails=1&zoom={zoom}&lat={latitude}&lon={longitude}");

        return await SendAsync<NominatimResponse>(route, language, "reverse", cancellationToken);
    }

    /// <summary>Free-text place search for the manual area picker. Empty when the lookup failed.</summary>
    internal async Task<IReadOnlyList<NominatimResponse>> SearchAsync(
        string query,
        int limit,
        string language,
        CancellationToken cancellationToken)
    {
        var route = string.Create(
            CultureInfo.InvariantCulture,
            $"search?format=jsonv2&addressdetails=1&limit={limit}&q={Uri.EscapeDataString(query)}");

        return await SendAsync<IReadOnlyList<NominatimResponse>>(route, language, "search", cancellationToken) ?? [];
    }

    /// <summary>
    /// Every failure — refusal, timeout, unreadable body — comes back as null. The route name is the
    /// only request detail logged: a coordinate in a log file is the location leak Geo exists to
    /// avoid.
    /// </summary>
    private async Task<T?> SendAsync<T>(string route, string language, string operation, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await limiter.ExecuteAsync(
                token =>
                {
                    // Sent as Accept-Language, so place names come back in the language Geo asked for.
                    var request = new HttpRequestMessage(HttpMethod.Get, route);
                    request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

                    return http.SendAsync(request, token);
                },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Nominatim {Operation} refused the request with {StatusCode}.",
                    operation,
                    (int)response.StatusCode);

                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Nominatim {Operation} failed.", operation);

            return default;
        }
    }
}
