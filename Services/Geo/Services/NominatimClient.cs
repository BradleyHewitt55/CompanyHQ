using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>
/// The only place CompanyHQ talks to Nominatim. It owns the HTTP concern — rate limiting, transport
/// failures, and deserialisation — and hands back plain results; caching and mapping live in
/// <see cref="GeoService"/>.
/// </summary>
public sealed class NominatimClient(
    HttpClient http,
    GeoOptions options,
    NominatimRateLimiter limiter,
    ILogger<NominatimClient> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Names the place at a coordinate, or null when Nominatim cannot or will not answer.</summary>
    internal async Task<NominatimResponse?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var route = string.Create(
            CultureInfo.InvariantCulture,
            $"reverse?format=jsonv2&addressdetails=1&zoom={options.AreaZoom}&lat={latitude}&lon={longitude}");

        return await SendAsync<NominatimResponse>(route, "reverse", cancellationToken);
    }

    /// <summary>Free-text place search for the manual area picker. Empty when the lookup failed.</summary>
    internal async Task<IReadOnlyList<NominatimResponse>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var route = string.Create(
            CultureInfo.InvariantCulture,
            $"search?format=jsonv2&addressdetails=1&limit={limit}&q={Uri.EscapeDataString(query)}");

        return await SendAsync<IReadOnlyList<NominatimResponse>>(route, "search", cancellationToken) ?? [];
    }

    /// <summary>
    /// Every failure — refusal, timeout, unreadable body — comes back as null. The route name is the
    /// only request detail logged: a coordinate in a log file is the location leak this service
    /// exists to avoid.
    /// </summary>
    private async Task<T?> SendAsync<T>(string route, string operation, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await limiter.ExecuteAsync(
                token => http.GetAsync(route, token),
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
