using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using CompanyHQ.Services.Geo.Contracts.Models;
using CompanyHQ.Services.OpenStreetMap.Contracts.Dtos;

namespace CompanyHQ.Services.OpenStreetMap.Services;

/// <summary>Owns the bounded OpenStreetMap Overpass request used to find places.</summary>
public sealed class OverpassClient(
    HttpClient http,
    OverpassRateLimiter limiter,
    ILogger<OverpassClient> logger)
{
    /// <summary>
    /// What Overpass is allowed to spend before it aborts. The transport timeout in
    /// <c>AddOpenStreetMap</c> must stay above it, so a slow query fails as a logged 504 rather than
    /// as a client-side cancellation that says nothing about why.
    /// </summary>
    public const int QueryTimeoutSeconds = 25;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Finds places of every given kind inside a bounding box. Null when the query failed.</summary>
    internal async Task<IReadOnlyList<OverpassElement>?> SearchBoundingBoxAsync(
        GeoViewport bounds,
        IReadOnlyList<string> kinds,
        CancellationToken cancellationToken)
    {
        // The kinds are spliced into a regex. Geo validates them at startup; this adapter does not
        // rely on that, because the text goes to a public endpoint under this deployment's name.
        if (kinds.Count == 0 || kinds.Any(kind => string.IsNullOrEmpty(kind)
            || kind.Any(character => !(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'))))
        {
            throw new ArgumentException("Place kinds must be lower-case OpenStreetMap amenity values (a-z, 0-9, _).", nameof(kinds));
        }

        // One regex pass instead of a spatial scan per amenity: a scan per kind measured more than
        // twice as slow, and the public endpoint culls long queries first.
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"[out:json][timeout:{QueryTimeoutSeconds}];nwr[\"amenity\"~\"^({string.Join('|', kinds)})$\"]({bounds.South},{bounds.West},{bounds.North},{bounds.East});out center;");

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
