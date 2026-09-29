using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Geo.Contracts.Dtos;
using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>Owns the bounded OpenStreetMap Overpass request used to find nearby places.</summary>
public sealed class OverpassClient(
    HttpClient http,
    GeoOptions options,
    OverpassRateLimiter limiter,
    ILogger<OverpassClient> logger)
{
    /// <summary>
    /// What Overpass is allowed to spend before it aborts. The transport timeout in
    /// <c>AddGeo</c> must stay above it, so a slow query fails as a logged 504 rather than as a
    /// client-side cancellation that says nothing about why.
    /// </summary>
    public const int QueryTimeoutSeconds = 25;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Finds places of every configured kind inside a bounding box.</summary>
    /// <remarks>
    /// A bounding box rather than <c>around</c> so results line up exactly with the tile grid the
    /// cache is keyed on; a radius would spill across tile edges and make an entry unusable for the
    /// tile beside it.
    /// </remarks>
    internal async Task<IReadOnlyList<OverpassElement>?> SearchBoundingBoxAsync(
        GeoViewport bounds,
        CancellationToken cancellationToken)
    {
        // One regex pass instead of a spatial scan per amenity: a scan per kind measured more than
        // twice as slow, and the public endpoint culls long queries first. The kinds are validated
        // at startup to bare tag values, so splicing them into the regex is safe.
        var kinds = string.Join('|', options.NearbyPlaceKinds);
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"[out:json][timeout:{QueryTimeoutSeconds}];nwr[\"amenity\"~\"^({kinds})$\"]({bounds.South},{bounds.West},{bounds.North},{bounds.East});out center;");

        try
        {
            using var response = await limiter.ExecuteAsync(
                token => http.PostAsync("api/interpreter", new FormUrlEncodedContent([new("data", query)]), token),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Overpass nearby-place search refused the request with {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            return (await response.Content.ReadFromJsonAsync<OverpassResponse>(SerializerOptions, cancellationToken))?.Elements;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "Overpass nearby-place search failed.");
            return null;
        }
    }
}
