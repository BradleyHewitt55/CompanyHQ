using System.Globalization;
using System.Text.Json.Serialization;

namespace CompanyHQ.OpenStreetMap.Contracts.Dtos;

/// <summary>
/// The subset of a Nominatim <c>jsonv2</c> place that CompanyHQ reads. Internal: the wire shape of a
/// third party is not something the API should leak to its own callers.
/// </summary>
internal sealed record NominatimResponse
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    /// <summary>Nominatim serialises coordinates as strings, not numbers.</summary>
    [JsonPropertyName("lat")]
    public string? Latitude { get; init; }

    [JsonPropertyName("lon")]
    public string? Longitude { get; init; }

    [JsonPropertyName("address")]
    public NominatimAddress? Address { get; init; }

    /// <summary>
    /// Parses a Nominatim coordinate. Always invariant — the response is machine formatted, and a
    /// server running under a comma-decimal culture would otherwise read 1.5 as 15.
    /// </summary>
    public static double? ParseCoordinate(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}

/// <summary>
/// The address block of a Nominatim place. Which key holds the settlement depends on how the place
/// is tagged in OSM, so the caller has to fall back through them.
/// </summary>
internal sealed record NominatimAddress
{
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("town")]
    public string? Town { get; init; }

    [JsonPropertyName("village")]
    public string? Village { get; init; }

    [JsonPropertyName("suburb")]
    public string? Suburb { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; init; }

    public string? Locality => City ?? Town ?? Village ?? Suburb;
}
